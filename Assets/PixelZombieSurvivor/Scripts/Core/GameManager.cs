using System;
using PixelZombieSurvivor.Building;
using PixelZombieSurvivor.Economy;
using PixelZombieSurvivor.Player;
using PixelZombieSurvivor.Waves;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelZombieSurvivor.Core
{
    /// <summary>
    /// Coordinates the run: owns high-level state, reacts to player death and restarts
    /// the scene. It deliberately contains no wave spawning or UI code.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class GameManager : SingletonBehaviour<GameManager>
    {
        [Header("Scene References (auto-resolved when empty)")]
        [SerializeField] private PlayerHealth player;
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private ShopManager shopManager;
        [SerializeField] private BuildingPlacementController placementController;

        public event Action<GameState> StateChanged;

        /// <summary>Fired when the player pauses or resumes with Esc.</summary>
        public event Action<bool> PauseChanged;

        public GameState State { get; private set; } = GameState.Intermission;
        public bool IsPaused { get; private set; }
        public PlayerHealth Player => player;
        public WaveManager WaveManager => waveManager;
        public ShopManager ShopManager => shopManager;
        public BuildingPlacementController PlacementController => placementController;
        public int Kills { get; private set; }
        public bool IsRunning => State != GameState.GameOver;

        protected override void Awake()
        {
            base.Awake();
            Time.timeScale = 1f;
            Application.targetFrameRate = 120;
        }

        private void Start()
        {
            ResolveReferences();

            if (player != null)
            {
                player.Died += HandlePlayerDied;
            }

            if (waveManager != null)
            {
                waveManager.KillsChanged += HandleKillsChanged;
                SetState(GameState.Intermission, true);
                waveManager.BeginRun();
            }
            else
            {
                Debug.LogError("WaveManager is not assigned and could not be found.", this);
            }

            if (player == null)
            {
                Debug.LogError("PlayerHealth is not assigned and could not be found.", this);
            }
        }

        private void Update()
        {
            bool pausePressed = InputBindings.IsDown(GameAction.Pause);
            if (State == GameState.GameOver || !pausePressed || InputBindings.IsCapturing)
            {
                return;
            }

            // The pause key is shared: the shop and the placement ghost consume it first.
            if (shopManager != null && shopManager.IsOpen)
            {
                return;
            }

            if (placementController != null && placementController.IsBusy)
            {
                return;
            }

            TogglePause();
        }

        public void TogglePause()
        {
            SetPaused(!IsPaused);
        }

        public void SetPaused(bool paused)
        {
            if (State == GameState.GameOver || IsPaused == paused)
            {
                return;
            }

            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            PauseChanged?.Invoke(IsPaused);
        }

        public void NotifyWaveState(GameState state)
        {
            if (State == GameState.GameOver)
            {
                return;
            }

            SetState(state);
        }

        public void HandlePlayerDeath()
        {
            if (State == GameState.GameOver)
            {
                return;
            }

            SetState(GameState.GameOver);
            IsPaused = false;
            Time.timeScale = 0f;

            if (waveManager != null)
            {
                waveManager.StopRun();
            }

            if (shopManager != null)
            {
                shopManager.ForceClose();
            }

            if (placementController != null)
            {
                placementController.CancelAll();
            }
        }

        public void RestartGame()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.buildIndex >= 0)
            {
                SceneManager.LoadScene(activeScene.buildIndex);
            }
            else
            {
                SceneManager.LoadScene(activeScene.name);
            }
        }

        private void HandlePlayerDied(DamageableBehaviour _)
        {
            HandlePlayerDeath();
        }

        private void HandleKillsChanged(int totalKills)
        {
            Kills = totalKills;
        }

        private void SetState(GameState newState, bool force = false)
        {
            if (!force && State == newState)
            {
                return;
            }

            State = newState;
            StateChanged?.Invoke(State);
        }

        private void ResolveReferences()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerHealth>();
            }

            if (waveManager == null)
            {
                waveManager = FindAnyObjectByType<WaveManager>();
            }

            if (shopManager == null)
            {
                shopManager = FindAnyObjectByType<ShopManager>();
            }

            if (placementController == null)
            {
                placementController = FindAnyObjectByType<BuildingPlacementController>();
            }
        }

        protected override void OnDestroy()
        {
            if (player != null)
            {
                player.Died -= HandlePlayerDied;
            }

            if (waveManager != null)
            {
                waveManager.KillsChanged -= HandleKillsChanged;
            }

            base.OnDestroy();
        }
    }
}
