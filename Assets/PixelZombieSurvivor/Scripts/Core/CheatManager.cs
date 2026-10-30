using System;
using PixelZombieSurvivor.Enemies;
using PixelZombieSurvivor.Player;
using PixelZombieSurvivor.Waves;
using UnityEngine;

namespace PixelZombieSurvivor.Core
{
    /// <summary>
    /// Optional developer/cheat layer. Alt+1 toggles god mode (immortality, a one-off
    /// 100 000 coins and enemies dying within a second); Alt+E skips the current wave
    /// or launches the next one from an intermission.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CheatManager : MonoBehaviour
    {
        public const int GodModeCoins = 100000;

        private const float EnemyTickInterval = 0.2f;
        private const float EnemyKillSeconds = 1f;

        private static CheatManager instance;
        private static float nextEnemyDamageTime;

        public static CheatManager Instance => instance;
        public static bool GodMode { get; private set; }

        /// <summary>Raised so the HUD can show a toast when a cheat is toggled.</summary>
        public static event Action<string, bool> Notification;

        public static CheatManager EnsureInstalled()
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject root = new GameObject("[Cheats]");
            return root.AddComponent<CheatManager>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            nextEnemyDamageTime = 0f;
        }

        private void Update()
        {
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (alt && Input.GetKeyDown(KeyCode.Alpha1))
            {
                ToggleGodMode();
            }
            else if (alt && Input.GetKeyDown(KeyCode.E))
            {
                SkipWaveNow();
            }

            if (GodMode)
            {
                ApplyGodDamage();
            }
        }

        public static void ToggleGodMode()
        {
            GodMode = !GodMode;

            if (GodMode)
            {
                CurrencyManager.Instance?.Add(GodModeCoins);

                PlayerHealth player = GameManager.Instance != null ? GameManager.Instance.Player : null;
                if (player != null)
                {
                    player.RestoreHealth(player.MaxHealth);
                }

                Notification?.Invoke(
                    $"РЕЖИМ БОГА: бессмертие, +{GodModeCoins:N0} монет, зомби гибнут за 1 секунду",
                    true);
            }
            else
            {
                Notification?.Invoke("РЕЖИМ БОГА ВЫКЛЮЧЕН", false);
            }

            AudioService.Instance?.PlayCoin();
        }

        public static bool SkipWaveNow()
        {
            WaveManager waves = GameManager.Instance != null ? GameManager.Instance.WaveManager : null;
            if (waves == null)
            {
                return false;
            }

            bool wasAwaitingStart = waves.AwaitingStart;
            if (!waves.SkipWave())
            {
                Notification?.Invoke("Сейчас нечего пропускать.", false);
                return false;
            }

            Notification?.Invoke(
                wasAwaitingStart ? "Следующая волна запущена." : "Волна пропущена.",
                true);
            return true;
        }

        private static void ApplyGodDamage()
        {
            if (Time.time < nextEnemyDamageTime)
            {
                return;
            }

            nextEnemyDamageTime = Time.time + EnemyTickInterval;

            // Five ticks per second means a regular zombie dies in about one second.
            float amount = EnemyTickInterval / EnemyKillSeconds;
            var enemies = EnemyRegistry.Active;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                DamageableBehaviour enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive || enemy.Faction != Faction.Enemy)
                {
                    continue;
                }

                enemy.TakeDamage(new DamageInfo(
                    enemy.MaxHealth * amount,
                    enemy.transform.position,
                    Vector2.zero,
                    Faction.Player,
                    instance != null ? instance.gameObject : null));
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                GodMode = false;
            }
        }
    }
}
