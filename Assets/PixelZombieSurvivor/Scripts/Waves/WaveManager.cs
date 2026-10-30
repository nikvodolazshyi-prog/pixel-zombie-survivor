using System;
using System.Collections;
using System.Collections.Generic;
using PixelZombieSurvivor.Building;
using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Data;
using PixelZombieSurvivor.Economy;
using PixelZombieSurvivor.Enemies;
using PixelZombieSurvivor.Player;
using UnityEngine;

namespace PixelZombieSurvivor.Waves
{
    /// <summary>
    /// Endless wave loop: intermission -> spawn/fight -> intermission.
    /// Scaling is read from WaveSettingsSO, while individual zombie stats come
    /// from ZombieDataSO. WaveManager is the only owner of alive enemy bookkeeping.
    /// Wave 25 spawns the boss; its summoned minions are tracked in the same list.
    /// </summary>
    public sealed class WaveManager : MonoBehaviour
    {
        [Header("Scene References (auto-resolved when empty)")]
        [SerializeField] private PlayerHealth player;
        [SerializeField] private List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
        [SerializeField] private WaveSettingsSO overrideSettings;

        [Header("Spawning")]
        [SerializeField, Min(0f)] private float spawnPointJitter = 0.65f;
        [SerializeField] private Transform zombieParent;

        [Header("Boss")]
        [SerializeField, Min(1)] private int bossWave = 25;

        private readonly List<Zombie> aliveZombies = new List<Zombie>();
        private GameDatabaseSO database;
        private WaveSettingsSO settings;
        private Coroutine runRoutine;
        private int pendingZombies;
        private int totalKills;
        private bool startRequested;
        private BossController activeBoss;

        public event Action<int> WaveStarted;
        public event Action<int> WaveCompleted;
        public event Action CountsChanged;
        public event Action<int> CountdownChanged;
        public event Action<int> KillsChanged;

        /// <summary>Raised once the previous wave is fully cleared and the next one waits for input.</summary>
        public event Action<int> ReadyForNextWave;

        public int CurrentWave { get; private set; }
        public int AliveZombies => aliveZombies.Count;
        public int PendingZombies => pendingZombies;
        public int RemainingZombies => aliveZombies.Count + pendingZombies + (BossActive ? 1 : 0);
        public int TotalKills => totalKills;
        public float SecondsUntilNextWave { get; private set; }
        public bool IsWaveActive { get; private set; }
        public bool IsRunning => runRoutine != null;
        public int BossWave => bossWave;
        public bool BossActive => activeBoss != null && activeBoss.IsAlive;
        public BossController ActiveBoss => activeBoss;

        /// <summary>True between waves: the shop is open for business and the player decides when to continue.</summary>
        public bool AwaitingStart { get; private set; }
        public int NextWaveNumber { get; private set; }

        private void Awake()
        {
            database = GameDatabaseSO.Load();
            settings = overrideSettings != null ? overrideSettings : database != null ? database.WaveSettings : null;

            if (player == null)
            {
                player = FindAnyObjectByType<PlayerHealth>();
            }

            if (spawnPoints.Count == 0)
            {
                spawnPoints.AddRange(GetComponentsInChildren<SpawnPoint>(true));
            }
        }

        public void BeginRun()
        {
            if (runRoutine != null)
            {
                return;
            }

            if (settings == null)
            {
                Debug.LogError("WaveSettingsSO is missing. Assign it on the GameDatabase asset.", this);
                return;
            }

            if (database == null || database.ZombiePrefab == null)
            {
                Debug.LogError("Zombie prefab or GameDatabase is missing.", this);
                return;
            }

            runRoutine = StartCoroutine(RunLoop());
        }

        public void StopRun()
        {
            if (runRoutine != null)
            {
                StopCoroutine(runRoutine);
                runRoutine = null;
            }

            IsWaveActive = false;
            AwaitingStart = false;
            startRequested = false;
            pendingZombies = 0;
            SecondsUntilNextWave = 0f;
            RaiseCountsChanged();
        }

        /// <summary>
        /// Starts the wave that is currently waiting for the player. Called by the HUD
        /// button, so the shopping break lasts as long as the player wants.
        /// </summary>
        public void StartNextWave()
        {
            if (!AwaitingStart)
            {
                return;
            }

            startRequested = true;
        }

        /// <summary>
        /// Cheat helper used by Alt+E. During an intermission it launches the next
        /// wave; during a fight it removes the remaining zombies (and the boss) so the
        /// wave completes immediately.
        /// </summary>
        public bool SkipWave()
        {
            if (!IsRunning || (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver))
            {
                return false;
            }

            if (AwaitingStart)
            {
                StartNextWave();
                return true;
            }

            pendingZombies = 0;
            SecondsUntilNextWave = 0f;

            for (int i = aliveZombies.Count - 1; i >= 0; i--)
            {
                Zombie zombie = aliveZombies[i];
                if (zombie != null)
                {
                    zombie.Died -= HandleZombieDied;
                    Destroy(zombie.gameObject);
                }
            }

            aliveZombies.Clear();

            if (activeBoss != null)
            {
                activeBoss.Died -= HandleBossDied;
                Destroy(activeBoss.gameObject);
                activeBoss = null;
            }

            RaiseCountsChanged();
            return true;
        }

        private IEnumerator RunLoop()
        {
            // Let the UI subscribe before the first "ready" event goes out.
            yield return null;

            while (GameManager.Instance == null || GameManager.Instance.State != GameState.GameOver)
            {
                yield return WaitForStart(CurrentWave + 1);

                CurrentWave++;
                IsWaveActive = true;
                GameManager.Instance?.NotifyWaveState(GameState.WaveActive);
                WaveStarted?.Invoke(CurrentWave);

                yield return WaveRoutine(CurrentWave);

                IsWaveActive = false;
                if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
                {
                    break;
                }

                CollectDroppedCoins();
                WaveCompleted?.Invoke(CurrentWave);
            }

            runRoutine = null;
        }

        private IEnumerator WaitForStart(int waveNumber)
        {
            AwaitingStart = true;
            NextWaveNumber = waveNumber;
            startRequested = false;
            SecondsUntilNextWave = 0f;
            GameManager.Instance?.NotifyWaveState(GameState.Intermission);
            ReadyForNextWave?.Invoke(waveNumber);
            RaiseCountsChanged();

            while (!startRequested)
            {
                if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
                {
                    AwaitingStart = false;
                    yield break;
                }

                yield return null;
            }

            AwaitingStart = false;
            startRequested = false;
        }

        /// <summary>Pulls every coin still lying on the arena into the wallet.</summary>
        private void CollectDroppedCoins()
        {
            Coin[] coins = FindObjectsByType<Coin>();
            for (int i = 0; i < coins.Length; i++)
            {
                if (coins[i] != null)
                {
                    coins[i].ForceCollect();
                }
            }
        }

        private IEnumerator WaveRoutine(int waveNumber)
        {
            int zombieCount = settings.GetZombieCount(waveNumber);
            int maxAlive = settings.GetMaxAlive(waveNumber);
            bool isBossWave = waveNumber == bossWave && database != null && database.BossPrefab != null;
            if (isBossWave)
            {
                // The boss is the star of wave 25: thin out the regular spawns so
                // the fight stays readable and the boss bar does not share the stage
                // with a full wall of zombies.
                zombieCount = Mathf.Max(6, zombieCount / 2);
                maxAlive += 4;
            }

            pendingZombies = zombieCount;
            aliveZombies.Clear();
            RaiseCountsChanged();

            if (isBossWave)
            {
                SpawnBoss(waveNumber);
            }

            while (pendingZombies > 0 || aliveZombies.Count > 0 || BossActive)
            {
                if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
                {
                    yield break;
                }

                if (GameManager.Instance != null && GameManager.Instance.IsPaused)
                {
                    yield return null;
                    continue;
                }

                if (pendingZombies > 0 && aliveZombies.Count < maxAlive)
                {
                    SpawnZombie(waveNumber);
                    pendingZombies--;
                    RaiseCountsChanged();

                    if (pendingZombies > 0)
                    {
                        yield return new WaitForSeconds(settings.SpawnInterval);
                    }

                    continue;
                }

                yield return null;
            }
        }

        private void SpawnBoss(int waveNumber)
        {
            Vector3 position = ClampToArena(GetSpawnPosition());

            // The boss has hand-authored HP; scaling it by the endless wave
            // multiplier would turn the fight into a bullet sponge.
            GameObject instance = Instantiate(database.BossPrefab, position, Quaternion.identity, zombieParent);
            BossController boss = instance.GetComponent<BossController>();
            if (boss == null)
            {
                Debug.LogError("Boss prefab is missing the BossController component.", this);
                Destroy(instance);
                return;
            }

            boss.Initialize(
                player,
                this,
                1f,
                settings.GetDamageMultiplier(waveNumber),
                settings.GetSpeedMultiplier(waveNumber),
                settings.GetCoinMultiplier(waveNumber));

            boss.Died += HandleBossDied;
            activeBoss = boss;
            RaiseCountsChanged();
        }

        /// <summary>Called by the boss to summon regular zombies around itself.</summary>
        public void SpawnReinforcements(Vector3 origin, int count)
        {
            if (settings == null || database == null || database.ZombiePrefab == null || count <= 0)
            {
                return;
            }

            int waveNumber = Mathf.Max(1, CurrentWave);
            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(1.2f, 2.6f);
                SpawnZombieAt(waveNumber, ClampToArena(origin + (Vector3)offset));
            }

            RaiseCountsChanged();
        }

        private void SpawnZombie(int waveNumber)
        {
            SpawnZombieAt(waveNumber, GetSpawnPosition());
        }

        private void SpawnZombieAt(int waveNumber, Vector3 position)
        {
            ZombieDataSO zombieData = settings.PickZombie(waveNumber);
            if (zombieData == null)
            {
                Debug.LogWarning("No compatible zombie found in WaveSettings roster.", this);
                return;
            }

            position = ClampToArena(position);
            GameObject instance = Instantiate(database.ZombiePrefab, position, Quaternion.identity, zombieParent);
            Zombie zombie = instance.GetComponent<Zombie>();
            if (zombie == null)
            {
                Debug.LogError("Zombie prefab is missing the Zombie component.", this);
                Destroy(instance);
                return;
            }

            zombie.Initialize(
                player,
                this,
                zombieData,
                settings.GetHealthMultiplier(waveNumber),
                settings.GetDamageMultiplier(waveNumber),
                settings.GetSpeedMultiplier(waveNumber),
                settings.GetCoinMultiplier(waveNumber));

            zombie.Died += HandleZombieDied;
            aliveZombies.Add(zombie);
            RaiseCountsChanged();
        }

        private Vector3 GetSpawnPosition()
        {
            if (spawnPoints.Count > 0)
            {
                int startIndex = UnityEngine.Random.Range(0, spawnPoints.Count);
                for (int i = 0; i < spawnPoints.Count; i++)
                {
                    SpawnPoint point = spawnPoints[(startIndex + i) % spawnPoints.Count];
                    if (point == null)
                    {
                        continue;
                    }

                    Vector2 offset = UnityEngine.Random.insideUnitCircle * spawnPointJitter;
                    return ClampToArena(point.transform.position + (Vector3)offset);
                }
            }

            Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
            return ClampToArena(direction * 12f);
        }

        /// <summary>
        /// Keeps every spawn (including boss summons at the arena edge) inside the
        /// fence, so enemies never appear behind the map.
        /// </summary>
        private static Vector3 ClampToArena(Vector3 position)
        {
            GridManager grid = GridManager.Instance;
            return grid != null ? grid.ClampToArena(position, 0.6f) : position;
        }

        private void HandleZombieDied(DamageableBehaviour deadZombie)
        {
            if (deadZombie is Zombie zombie)
            {
                zombie.Died -= HandleZombieDied;
                aliveZombies.Remove(zombie);
            }

            totalKills++;
            KillsChanged?.Invoke(totalKills);
            RaiseCountsChanged();
        }

        private void HandleBossDied(DamageableBehaviour deadBoss)
        {
            if (!(deadBoss is BossController boss))
            {
                return;
            }

            boss.Died -= HandleBossDied;
            if (activeBoss == boss)
            {
                activeBoss = null;
            }

            totalKills++;
            KillsChanged?.Invoke(totalKills);
            RaiseCountsChanged();
        }

        private void RaiseCountsChanged()
        {
            CountsChanged?.Invoke();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < aliveZombies.Count; i++)
            {
                if (aliveZombies[i] != null)
                {
                    aliveZombies[i].Died -= HandleZombieDied;
                }
            }

            if (activeBoss != null)
            {
                activeBoss.Died -= HandleBossDied;
            }
        }
    }
}
