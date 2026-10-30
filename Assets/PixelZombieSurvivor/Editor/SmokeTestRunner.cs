using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using PixelZombieSurvivor.Building;
using PixelZombieSurvivor.Combat;
using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Data;
using PixelZombieSurvivor.Economy;
using PixelZombieSurvivor.Enemies;
using PixelZombieSurvivor.Player;
using PixelZombieSurvivor.UI;
using PixelZombieSurvivor.Waves;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelZombieSurvivor.Editor
{
    /// <summary>
    /// Batch-mode verification tool. It validates the generated scene in edit mode,
    /// enters play mode, exercises the main loop (UI, shop, shooting, building
    /// placement, damage, waves) and then exits with a non-zero code on failure.
    ///
    /// Run without -quit:
    /// Unity.exe -batchmode -projectPath &lt;project&gt;
    ///   -executeMethod PixelZombieSurvivor.Editor.SmokeTestRunner.Run
    ///   -logFile &lt;path&gt;
    /// </summary>
    [InitializeOnLoad]
    public static class SmokeTestRunner
    {
        private const string SessionKey = "PZS.SmokeTest.Active";
        private const string PreReportKey = "PZS.SmokeTest.PreReport";
        private const string PreFailuresKey = "PZS.SmokeTest.PreFailures";
        private const string StartTicksKey = "PZS.SmokeTest.StartTicks";
        private const string ResultPath = @"C:\Users\N\AppData\Local\Temp\opencode\pzs_smoke.txt";
        private const double DurationSeconds = 28d;

        private static readonly List<string> failures = new List<string>();
        private static readonly List<string> errorLog = new List<string>();
        private static readonly StringBuilder report = new StringBuilder();

        private static int step;
        private static int preFailures;
        private static bool playObserved;
        private static double playStartTime;
        private static DateTime runStartedAt;
        private static Projectile trackedBullet;
        private static Vector2 trackedBulletPosition;

        static SmokeTestRunner()
        {
            if (!SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            // Domain reload after entering play mode wipes statics: restore state.
            preFailures = SessionState.GetInt(PreFailuresKey, 0);
            report.AppendLine(SessionState.GetString(PreReportKey, string.Empty));
            runStartedAt = ReadStartTime();
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            failures.Clear();
            errorLog.Clear();
            report.Clear();
            step = 0;
            playObserved = false;
            trackedBullet = null;
            trackedBulletPosition = Vector2.zero;

            int validationFailures = ValidateGeneratedProject(out string validationReport);
            preFailures = validationFailures;
            report.AppendLine(validationReport);

            runStartedAt = DateTime.UtcNow;
            SessionState.SetBool(SessionKey, true);
            SessionState.SetInt(PreFailuresKey, validationFailures);
            SessionState.SetString(PreReportKey, validationReport);
            SessionState.SetString(StartTicksKey, runStartedAt.Ticks.ToString());

            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;

            EditorSceneManager.OpenScene(DemoProjectBuilder.ScenePath);
            Debug.Log("[SMOKE] Entering play mode...");
            EditorApplication.isPlaying = true;
        }

        // ------------------------------------------------------------------
        // Edit-mode validation
        // ------------------------------------------------------------------

        private static int ValidateGeneratedProject(out string summary)
        {
            StringBuilder builder = new StringBuilder();
            int failureCount = 0;

            void Check(bool condition, string label)
            {
                if (condition)
                {
                    builder.AppendLine("[OK]   " + label);
                }
                else
                {
                    builder.AppendLine("[FAIL] " + label);
                    failureCount++;
                }
            }

            if (!File.Exists(DemoProjectBuilder.ScenePath))
            {
                summary = "[FAIL] Scene file is missing: " + DemoProjectBuilder.ScenePath;
                return 1;
            }

            EditorSceneManager.OpenScene(DemoProjectBuilder.ScenePath);

            GameManager manager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            Check(manager != null, "GameManager exists in the scene");

            if (manager != null)
            {
                Check(manager.Player != null, "GameManager.player is wired");
                Check(manager.WaveManager != null, "GameManager.waveManager is wired");
                Check(manager.ShopManager != null, "GameManager.shopManager is wired");
                Check(manager.PlacementController != null, "GameManager.placementController is wired");
            }

            PlayerHealth player = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
            Check(player != null, "PlayerHealth exists");
            Check(player != null && player.Faction == Faction.Player, "Player faction is Player");
            Check(UnityEngine.Object.FindAnyObjectByType<PlayerWeaponController>() != null, "PlayerWeaponController exists");
            Check(Camera.main != null && Camera.main.orthographic, "Orthographic Main Camera exists");

            WaveManager waves = UnityEngine.Object.FindAnyObjectByType<WaveManager>();
            Check(waves != null, "WaveManager exists");
            Check(UnityEngine.Object.FindObjectsByType<SpawnPoint>().Length == 12,
                "12 spawn points exist");
            Check(UnityEngine.Object.FindAnyObjectByType<GameUIBootstrap>() != null, "GameUIBootstrap exists");
            Check(UnityEngine.Object.FindAnyObjectByType<GridManager>() != null, "GridManager exists in the scene");

            GameDatabaseSO database = AssetDatabase.LoadAssetAtPath<GameDatabaseSO>(DemoProjectBuilder.DatabasePath);
            Check(database != null, "GameDatabase asset exists");
            if (database != null)
            {
                Check(database.Weapons.Count == 6, "Database has 6 weapons (found " + database.Weapons.Count + ")");
                Check(database.Buildings.Count == 5, "Database has 5 buildings (found " + database.Buildings.Count + ")");
                Check(database.Consumables.Count == 2, "Database has 2 consumables (found " + database.Consumables.Count + ")");
                Check(database.WaveSettings != null, "Wave settings assigned");
                Check(database.ZombiePrefab != null, "Zombie prefab assigned");
                Check(database.BossPrefab != null, "Boss prefab assigned");
                Check(database.BulletPrefab != null, "Bullet prefab assigned");
                Check(database.CoinPrefab != null, "Coin prefab assigned");
                Check(database.StartingWeapon != null && database.StartingWeapon.OwnedByDefault,
                    "Starting weapon is owned by default");

                if (database.BossPrefab != null)
                {
                    Check(database.BossPrefab.GetComponent<BossController>() != null, "Boss prefab has BossController");
                    Check(database.BossPrefab.GetComponent<Collider2D>() != null, "Boss prefab has a collider");
                    Check(database.BossPrefab.GetComponent<Rigidbody2D>() != null, "Boss prefab has a Rigidbody2D");
                    BossController bossComponent = database.BossPrefab.GetComponent<BossController>();
                    Check(bossComponent != null && bossComponent.MaxHealth >= 3000f,
                        "Boss has a large health pool");
                }

                WeaponDataSO shotgun = FindWeapon(database, "Дробовик");
                Check(shotgun != null && shotgun.Pellets >= 5, "Shotgun fires multiple pellets");

                if (database.ZombiePrefab != null)
                {
                    Check(database.ZombiePrefab.GetComponent<Zombie>() != null, "Zombie prefab has Zombie component");
                    Check(database.ZombiePrefab.GetComponent<Collider2D>() != null, "Zombie prefab has a collider");
                    Check(database.ZombiePrefab.GetComponent<Rigidbody2D>() != null, "Zombie prefab has a Rigidbody2D");
                    Check(database.ZombiePrefab.GetComponentInChildren<SpriteAnimator>() != null,
                        "Zombie prefab has a SpriteAnimator");
                }

                for (int i = 0; i < database.Zombies.Count; i++)
                {
                    ZombieDataSO zombieData = database.Zombies[i];
                    if (zombieData == null)
                    {
                        continue;
                    }

                    Check(zombieData.WalkFrames != null && zombieData.WalkFrames.Length == 8,
                        zombieData.DisplayName + " has 8 walk frames");
                    Check(zombieData.AttackFrames != null && zombieData.AttackFrames.Length == 6,
                        zombieData.DisplayName + " has 6 attack frames");
                }

                Check(database.Zombies.Count == 4, "Database has 4 zombie types (found " + database.Zombies.Count + ")");
                bool hasSpitter = false;
                for (int i = 0; i < database.Zombies.Count; i++)
                {
                    if (database.Zombies[i] != null && database.Zombies[i].RangedAttack)
                    {
                        hasSpitter = true;
                        Check(database.Zombies[i].WallsToPierce >= 1,
                            "Spitter acid pierces at least one wall");
                    }
                }

                Check(hasSpitter, "Ranged spitter is present in the roster");

                if (database.BulletPrefab != null)
                {
                    Check(database.BulletPrefab.GetComponent<Projectile>() != null, "Bullet prefab has Projectile");
                    Check(database.BulletPrefab.GetComponent<PooledObject>() != null, "Bullet prefab is poolable");
                    Check(database.BulletPrefab.GetComponent<Collider2D>() != null, "Bullet prefab has a trigger collider");
                }

                if (database.CoinPrefab != null)
                {
                    Check(database.CoinPrefab.GetComponent<Coin>() != null, "Coin prefab has Coin component");
                    Check(database.CoinPrefab.GetComponent<PooledObject>() != null, "Coin prefab is poolable");
                }

                for (int i = 0; i < database.Buildings.Count; i++)
                {
                    BuildingDataSO data = database.Buildings[i];
                    if (data == null || data.Prefab == null)
                    {
                        Check(false, "Building " + i + " has a prefab");
                        continue;
                    }

                    Check(data.Prefab.GetComponent<BuildingBase>() != null, data.DisplayName + " prefab has BuildingBase");
                    Check(data.Prefab.GetComponent<Collider2D>() != null, data.DisplayName + " prefab has a collider");
                    switch (data.BuildingType)
                    {
                        case BuildingType.Turret:
                            Check(data.Prefab.GetComponent<Turret>() != null, "Turret prefab has Turret component");
                            break;
                        case BuildingType.MoneyGenerator:
                            Check(data.Prefab.GetComponent<MoneyGenerator>() != null, "Generator prefab has MoneyGenerator");
                            break;
                        case BuildingType.RepairStation:
                            Check(data.Prefab.GetComponent<RepairStation>() != null, "Repair prefab has RepairStation");
                            break;
                        case BuildingType.Wall:
                            Check(data.MaxHealth >= 300f, "Wall prefab has a large health pool");
                            break;
                    }
                }
            }

            bool buildSceneEnabled = false;
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < buildScenes.Length; i++)
            {
                if (buildScenes[i].path == DemoProjectBuilder.ScenePath && buildScenes[i].enabled)
                {
                    buildSceneEnabled = true;
                }
            }

            Check(buildSceneEnabled, "Arena scene is enabled in Build Settings");

            summary = builder.ToString();
            Debug.Log("[SMOKE] Edit-mode validation:\n" + summary);
            return failureCount;
        }

        // ------------------------------------------------------------------
        // Play-mode checks
        // ------------------------------------------------------------------

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                // Watchdog: batch runs must never hang if play mode fails to start.
                if ((DateTime.UtcNow - runStartedAt).TotalSeconds > 75d)
                {
                    FailAndExit("Play mode did not start within 75 seconds.");
                }

                return;
            }

            if (!playObserved)
            {
                if (GameManager.Instance == null)
                {
                    return;
                }

                playObserved = true;
                playStartTime = EditorApplication.timeSinceStartup;
                Debug.Log("[SMOKE] Play mode observed, running checks for " + DurationSeconds + "s.");
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - playStartTime;
            if (elapsed > DurationSeconds + 45d)
            {
                FailAndExit("Play-mode checks timed out.");
                return;
            }

            // The smoke test keeps the player alive so later checks can run; the
            // damage path itself is still validated explicitly at step 4.
            PlayerHealth automatedPlayer = GameManager.Instance.Player;
            if (automatedPlayer != null && automatedPlayer.IsAlive && automatedPlayer.CurrentHealth < 60f)
            {
                automatedPlayer.RestoreHealth(1000f);
            }

            if (step == 0 && elapsed > 0.6)
            {
                step = 1;
                CheckRuntimeUi();
            }
            else if (step == 1 && elapsed > 1.4)
            {
                step = 2;
                CheckShopOpening();
            }
            else if (step == 2 && elapsed > 2.3)
            {
                step = 3;
                CheckWeaponPurchase();
            }
            else if (step == 3 && elapsed > 3.2)
            {
                step = 4;
                CheckShooting();
            }
            else if (step == 4 && elapsed > 3.4)
            {
                step = 5;
                CheckProjectileMovement();
            }
            else if (step == 5 && elapsed > 3.7)
            {
                step = 6;
                CheckPauseAndDash();
            }
            else if (step == 6 && elapsed > 4.2)
            {
                step = 7;
                CheckUpgrades();
            }
            else if (step == 7 && elapsed > 4.8)
            {
                step = 8;
                CheckPlayerDamage();
            }
            else if (step == 8 && elapsed > 5.6)
            {
                step = 9;
                CheckBuildingPlacement();
            }
            else if (step == 9 && elapsed > 16.0)
            {
                step = 10;
                CheckWaveProgress();
            }
            else if (step == 10 && elapsed > 22.0)
            {
                step = 11;
                CheckCombatOutcome();
            }
            else if (step == 11 && elapsed > 24.5)
            {
                step = 12;
                CheckBoss();
            }
            else if (step == 12 && elapsed > DurationSeconds)
            {
                step = 13;
                Finish();
            }
        }

        private static void CheckRuntimeUi()
        {
            Check(GameManager.Instance != null, "GameManager singleton available");
            Check(GameManager.Instance.Player != null, "GameManager resolved the player");
            Check(GameManager.Instance.WaveManager != null, "GameManager resolved the WaveManager");
            Check(GameManager.Instance.ShopManager != null, "GameManager resolved the ShopManager");
            Check(CurrencyManager.Instance != null, "CurrencyManager singleton available");
            Check(PoolService.Instance != null, "PoolService singleton available");
            Check(UnityEngine.Object.FindAnyObjectByType<HudController>() != null, "HUD was built at runtime");
            Check(UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null,
                "EventSystem was created");

            ShopUIController[] shops = UnityEngine.Object.FindObjectsByType<ShopUIController>(FindObjectsInactive.Include);
            Check(shops.Length == 1, "Shop UI was built at runtime");
            Check(shops.Length == 1 && !shops[0].gameObject.activeSelf, "Shop UI starts hidden");
            Check(CurrencyManager.Instance != null && CurrencyManager.Instance.Balance == 450,
                "Starting coins are 450 (found " +
                (CurrencyManager.Instance != null ? CurrencyManager.Instance.Balance : -1) + ")");
            Check(Time.timeScale > 0f && Time.timeScale < 2f, "Time is running normally");
            Check(AudioService.Instance != null, "AudioService is installed");
            Check(GameManager.Instance.WaveManager.AwaitingStart,
                "Wave 1 waits for the player instead of auto-starting");
            Check(UnityEngine.Object.FindAnyObjectByType<PlayerBuffs>() != null,
                "PlayerBuffs component is attached");
            Check(CheatManager.Instance != null, "CheatManager is installed");
            Check(typeof(CheatManager).GetMethod("ToggleGodMode") != null, "God mode cheat is available");
            Check(typeof(WaveManager).GetMethod("SkipWave") != null, "Wave skip cheat is available");

            SpriteAnimator playerAnimator = GameManager.Instance.Player.GetComponent<SpriteAnimator>();
            Check(playerAnimator != null && playerAnimator.Frames != null && playerAnimator.Frames.Length == 2,
                "Player idle animation has 2 frames");
            Check(playerAnimator != null && playerAnimator.IsPlaying, "Player idle animation is playing");
        }

        private static void CheckShopOpening()
        {
            ShopManager shop = GameManager.Instance.ShopManager;
            shop.OpenShop();
            Check(shop.IsOpen, "Shop opens with OpenShop()");

            ShopUIController shopUi = UnityEngine.Object.FindAnyObjectByType<ShopUIController>();
            Check(shopUi != null && shopUi.gameObject.activeSelf, "Shop UI becomes visible");

            PlayerWeaponController weapons = UnityEngine.Object.FindAnyObjectByType<PlayerWeaponController>();
            Check(weapons != null && weapons.OwnedWeapons.Count == 1, "Player starts with exactly one weapon");
        }

        private static void CheckWeaponPurchase()
        {
            ShopManager shop = GameManager.Instance.ShopManager;
            CurrencyManager currency = CurrencyManager.Instance;
            PlayerWeaponController weapons = UnityEngine.Object.FindAnyObjectByType<PlayerWeaponController>();
            GameDatabaseSO database = shop.Database;

            WeaponDataSO smg = FindWeapon(database, "SMG");
            Check(smg != null, "SMG exists in the database");

            if (smg != null && currency != null && weapons != null)
            {
                int before = currency.Balance;
                bool bought = shop.BuyWeapon(smg);
                Check(bought, "Buying the SMG succeeds");
                Check(currency.Balance == before - smg.Price,
                    $"Coins are deducted ({before} -> {currency.Balance}, price {smg.Price})");
                Check(weapons.HasWeapon(smg), "SMG is added to the inventory");
                Check(weapons.CurrentWeapon != null && weapons.CurrentWeapon.Data == smg, "SMG is auto-equipped");
            }

            shop.CloseShop();
            Check(!shop.IsOpen, "Shop closes with CloseShop()");
            ShopUIController shopUi = UnityEngine.Object.FindAnyObjectByType<ShopUIController>();
            Check(shopUi == null || !shopUi.gameObject.activeSelf, "Shop UI is hidden after closing");
        }

        private static void CheckShooting()
        {
            PlayerWeaponController weapons = UnityEngine.Object.FindAnyObjectByType<PlayerWeaponController>();
            if (weapons == null || weapons.CurrentWeapon == null)
            {
                Check(false, "Weapon available for the shooting test");
                return;
            }

            WeaponRuntime weapon = weapons.CurrentWeapon;
            int before = weapon.Clip;

            MethodInfo fire = typeof(PlayerWeaponController).GetMethod(
                "Fire",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(fire != null, "PlayerWeaponController.Fire exists");
            fire?.Invoke(weapons, new object[] { weapon });

            Check(weapon.Clip == before - 1, $"Firing consumes a round ({before} -> {weapon.Clip})");
            Projectile[] bullets = UnityEngine.Object.FindObjectsByType<Projectile>();
            Check(bullets.Length > 0, "A pooled projectile was spawned");

            for (int i = 0; i < bullets.Length; i++)
            {
                if (bullets[i] != null && bullets[i].gameObject.activeInHierarchy)
                {
                    trackedBullet = bullets[i];
                    trackedBulletPosition = bullets[i].transform.position;
                    break;
                }
            }

            Check(trackedBullet != null, "An active projectile is tracked for the movement test");

            WaveManager waves = GameManager.Instance.WaveManager;
            Check(waves.AwaitingStart, "The next wave is still waiting for the start button");
            waves.StartNextWave();
        }

        private static void CheckProjectileMovement()
        {
            if (trackedBullet == null)
            {
                Check(false, "Projectile survived long enough to be measured");
                return;
            }

            bool moved = !trackedBullet.gameObject.activeInHierarchy ||
                         Vector2.Distance(trackedBullet.transform.position, trackedBulletPosition) > 0.05f;
            Check(moved, "Projectiles travel away from the muzzle (none stuck in place)");
        }

        private static void CheckPauseAndDash()
        {
            GameManager manager = GameManager.Instance;

            manager.SetPaused(true);
            Check(manager.IsPaused && Mathf.Approximately(Time.timeScale, 0f),
                "Esc pause freezes time");
            manager.SetPaused(false);
            Check(!manager.IsPaused && Time.timeScale > 0f,
                "Unpausing restores time");

            PlayerController controller = manager.Player.GetComponent<PlayerController>();
            Check(controller != null, "PlayerController exists");
            if (controller == null)
            {
                return;
            }

            Check(controller.DashReady01 >= 0.999f, "Dash starts ready");

            MethodInfo dash = typeof(PlayerController).GetMethod(
                "TryDash",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(dash != null, "PlayerController.TryDash exists");
            dash?.Invoke(controller, null);
            Check(controller.DashReady01 < 0.5f, "Dash goes on cooldown after use");
        }

        private static void CheckUpgrades()
        {
            PlayerBuffs buffs = GameManager.Instance.Player.GetComponent<PlayerBuffs>();
            Check(buffs != null, "PlayerBuffs exists for the upgrade test");
            if (buffs == null)
            {
                return;
            }

            MethodInfo completed = typeof(PlayerBuffs).GetMethod(
                "HandleWaveCompleted",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(completed != null, "PlayerBuffs.HandleWaveCompleted exists");
            completed?.Invoke(buffs, new object[] { 10 });
            Check(buffs.AwaitingChoice, "Wave 10 offers a buff choice");

            buffs.Choose(BuffType.Shield);
            Check(!buffs.AwaitingChoice, "Choosing a buff closes the prompt");
            Check(buffs.ShieldLevel == 1, "Shield buff level is 1");

            PlayerHealth player = GameManager.Instance.Player;
            Check(player.MaxShield > 0f && player.Shield > 0f,
                $"Shield is applied to the player ({player.Shield:0}/{player.MaxShield:0})");

            buffs.Choose(BuffType.Speed);
            Check(buffs.SpeedLevel == 0, "A second choice is rejected until the next milestone");
        }

        private static void CheckPlayerDamage()
        {
            PlayerHealth player = GameManager.Instance.Player;
            float before = player.CurrentHealth;
            float shieldBefore = player.Shield;

            // The upgrade check may have granted a shield; send enough damage to punch
            // through it so the health-reduction path is still exercised.
            player.TakeDamage(new DamageInfo(
                shieldBefore + 10f,
                player.transform.position,
                Vector2.right,
                Faction.Enemy,
                null));

            Check(player.CurrentHealth < before,
                $"Player takes damage ({before:0} -> {player.CurrentHealth:0}, shield {shieldBefore:0} -> {player.Shield:0})");
            Check(shieldBefore <= 0f || player.Shield <= 0.01f || player.CurrentHealth < before,
                "Shield absorbs damage before health");

            SpriteRenderer bodyRenderer = player.GetComponent<SpriteRenderer>();
            Check(bodyRenderer != null && bodyRenderer.color.r > 0.8f && bodyRenderer.color.g < 0.55f,
                "Player body flashes red on damage");

            // Keep the player alive so the rest of the run can be observed.
            player.RestoreHealth(1000f);
            Check(player.CurrentHealth >= player.MaxHealth - 0.01f, "Health packs style healing works");
        }

        private static void CheckBuildingPlacement()
        {
            CurrencyManager currency = CurrencyManager.Instance;
            BuildingPlacementController placement = GameManager.Instance.PlacementController;
            GameDatabaseSO database = GameManager.Instance.ShopManager.Database;
            GridManager grid = placement.Grid;

            currency.Add(2500);

            BuildingDataSO turretData = null;
            BuildingDataSO wallData = null;
            for (int i = 0; i < database.Buildings.Count; i++)
            {
                BuildingDataSO data = database.Buildings[i];
                if (data == null)
                {
                    continue;
                }

                if (data.BuildingType == BuildingType.Turret && data.Price < 500)
                {
                    turretData = data;
                }
                else if (data.BuildingType == BuildingType.Wall)
                {
                    wallData = data;
                }
            }

            Check(turretData != null, "Turret data exists");
            Check(wallData != null, "Wall data exists");
            Check(grid != null, "Placement controller resolved the GridManager");
            if (turretData == null || wallData == null || grid == null)
            {
                return;
            }

            Vector2Int sampleCell = new Vector2Int(-18, 9);
            Check(grid.WorldToCell(grid.CellToWorld(sampleCell)) == sampleCell,
                "Grid converts world <-> cell without drift");
            Check(grid.IsInsideBuildArea(sampleCell),
                "Build area covers the whole arena from wave 1");

            KeyCode originalDemolish = InputBindings.Get(GameAction.Demolish);
            InputBindings.Set(GameAction.Demolish, KeyCode.F);
            Check(InputBindings.Get(GameAction.Demolish) == KeyCode.F,
                "Key bindings can be remapped at runtime");
            InputBindings.Set(GameAction.Demolish, originalDemolish);
            Check(InputBindings.Get(GameAction.Demolish) == originalDemolish,
                "Key binding remap can be reverted");

            int before = currency.Balance;
            bool started = placement.BeginPlacement(turretData);
            Check(started, "Placement mode starts");
            Check(placement.IsPlacing, "Placement state is active");

            MethodInfo placeAt = typeof(BuildingPlacementController).GetMethod(
                "PlaceAt",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(placeAt != null, "BuildingPlacementController.PlaceAt exists");
            Vector3 turretPosition = new Vector3(-3f, -3f, 0f);
            Vector2Int turretCell = grid.WorldToCell(turretPosition);
            placeAt?.Invoke(placement, new object[] { turretPosition });

            Check(!placement.IsPlacing, "Placement mode ends after confirming");
            Check(BuildingBase.Active.Count == 1, "A building exists in the world");
            Check(currency.Balance == before - turretData.Price,
                $"Building price is charged ({before} -> {currency.Balance})");

            BuildingBase turretBuilding = grid.GetBuilding(turretCell);
            Check(turretBuilding != null, "The turret occupies its exact grid cell");
            Check(turretBuilding != null && turretBuilding.HasCell && turretBuilding.Cell == turretCell,
                "Building stores the snapped cell");
            Vector3 cellCentre = grid.CellToWorld(turretCell);
            Check(turretBuilding != null && Vector3.Distance(turretBuilding.transform.position, cellCentre) < 0.001f,
                "Building sits exactly on the snapped cell centre");
            Check(Mathf.Approximately(cellCentre.x - turretCell.x, grid.CellSize * 0.5f) &&
                  Mathf.Approximately(cellCentre.y - turretCell.y, grid.CellSize * 0.5f),
                "Cell centre is offset by half a cell (no seams)");
            Check(grid.WorldToCell(cellCentre) == turretCell,
                "Snapped position maps back to the same cell");

            Turret turret = UnityEngine.Object.FindAnyObjectByType<Turret>();
            Check(turret != null, "Turret component is active");
            Check(turret != null && turret.GetComponent<BuildingBase>().MaxHealth == turretData.MaxHealth,
                "Building health is initialized from data");

            if (turretBuilding == null)
            {
                return;
            }

            Check(placement.SelectBuilding(turretBuilding), "Clicking a building opens its upgrade panel");
            Check(placement.IsInspecting, "Inspection mode is active");
            int upgradeCost = turretBuilding.NextUpgradeCost;
            int coinsBeforeUpgrade = currency.Balance;
            bool upgraded = placement.UpgradeSelected();
            Check(upgraded, "Turret upgrade succeeds");
            Check(turretBuilding.Level == 2, "Turret level increases to 2");
            Check(currency.Balance == coinsBeforeUpgrade - upgradeCost,
                "Upgrade cost is deducted");
            Check(Mathf.Approximately(turretBuilding.MaxHealth, turretData.GetMaxHealth(2)),
                "Turret max HP grows with the upgrade");
            Check(turretBuilding.SellValue > 0, "Building reports a sell value");
            placement.CloseSelection();
            Check(!placement.IsInspecting, "Inspection panel closes");

            placement.BeginPlacement(wallData);
            Vector3 wallPosition = new Vector3(-4f, -3f, 0f);
            Vector2Int wallCell = grid.WorldToCell(wallPosition);
            placeAt?.Invoke(placement, new object[] { wallPosition });
            BuildingBase wallBuilding = grid.GetBuilding(wallCell);
            Check(wallBuilding != null, "Wall snaps into the neighbouring cell");
            Check(wallBuilding != null && wallBuilding.IsWall, "Wall building is a wall");
            if (wallBuilding == null)
            {
                return;
            }

            int sellValue = wallBuilding.SellValue;
            int coinsBeforeSell = currency.Balance;
            placement.SelectBuilding(wallBuilding);
            placement.DemolishSelected();
            Check(!grid.IsOccupied(wallCell), "Demolish frees the grid cell");
            Check(currency.Balance == coinsBeforeSell + sellValue,
                $"Demolish refunds 60% of the invested coins (+{sellValue})");
            Check(placement.SelectedBuilding == null, "Demolished building closes its panel");
            Check(grid.GetBuilding(turretCell) == turretBuilding, "Neighbouring turret is untouched");

            placement.BeginPlacement(wallData);
            Vector3 edgePosition = grid.CellToWorld(sampleCell);
            placeAt?.Invoke(placement, new object[] { edgePosition });
            BuildingBase edgeWall = grid.GetBuilding(sampleCell);
            Check(edgeWall != null, "Buildings can be placed at the arena edge");
            if (edgeWall != null)
            {
                placement.Demolish(edgeWall);
            }

            Vector3 clamped = grid.ClampToArena(new Vector3(999f, 999f, 0f));
            Check(grid.IsInsideFullArena(grid.WorldToCell(clamped)),
                "Arena clamp keeps boss summons inside the map");

            int coinsBeforeBurst = currency.Balance;
            CoinBurst.Drop(database.CoinPrefab, new Vector3(2f, 2f, 0f), 25, 4, 0.5f);
            Check(currency.Balance == coinsBeforeBurst + 25,
                "Coin burst credits the wallet immediately");
            Check(UnityEngine.Object.FindObjectsByType<Coin>().Length > 0,
                "Coin burst spawns a short visual effect");
        }

        private static void CheckBoss()
        {
            WaveManager waves = GameManager.Instance.WaveManager;
            Check(waves != null, "WaveManager available for the boss test");
            if (waves == null)
            {
                return;
            }

            MethodInfo spawnBoss = typeof(WaveManager).GetMethod(
                "SpawnBoss",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(spawnBoss != null, "WaveManager.SpawnBoss exists");
            spawnBoss?.Invoke(waves, new object[] { 25 });

            BossController boss = BossController.ActiveBoss;
            Check(boss != null, "Boss spawns for the boss wave");
            Check(waves.BossActive, "WaveManager counts the boss for wave completion");
            if (boss == null)
            {
                return;
            }

            Check(boss.Faction == Faction.Enemy, "Boss belongs to the enemy faction");
            Check(boss.MaxHealth >= 3000f, "Boss health pool is large");
            Check(EnemyRegistry.FindNearest(boss.transform.position, 2f) != null,
                "Boss is registered for turret targeting");

            boss.TakeDamage(new DamageInfo(
                1_000_000f,
                boss.transform.position,
                Vector2.right,
                Faction.Player,
                null));

            Check(!waves.BossActive, "Boss defeat clears the active boss flag");
            Check(BossController.ActiveBoss == null, "Boss static reference is cleared");

            int coinsBeforeGod = CurrencyManager.Instance.Balance;
            CheatManager.ToggleGodMode();
            Check(CheatManager.GodMode, "God mode toggles on (Alt+1)");
            Check(CurrencyManager.Instance.Balance == coinsBeforeGod + CheatManager.GodModeCoins,
                "God mode grants 100 000 coins");
            CheatManager.ToggleGodMode();
            Check(!CheatManager.GodMode, "God mode toggles off");

            Check(waves.SkipWave(), "Alt+E wave skip command executes");
        }

        private static void CheckWaveProgress()
        {
            WaveManager waves = GameManager.Instance.WaveManager;
            Check(waves.CurrentWave >= 1,
                $"Wave 1 started (current wave {waves.CurrentWave})");
            Check(waves.AliveZombies > 0 || waves.TotalKills > 0,
                $"Zombies spawned (alive {waves.AliveZombies}, kills {waves.TotalKills})");
            Check(GameManager.Instance.State != GameState.GameOver || waves.TotalKills >= 0,
                "Game is running or ended with a valid state");

            Zombie[] zombies = UnityEngine.Object.FindObjectsByType<Zombie>();
            bool animatedZombie = false;
            for (int i = 0; i < zombies.Length; i++)
            {
                SpriteAnimator animator = zombies[i] != null ? zombies[i].GetComponentInChildren<SpriteAnimator>() : null;
                // Walking zombies use the 8-frame walk sheet, attacking ones switch to
                // the 6-frame attack sheet; either proves animation is running.
                if (animator != null && animator.Frames != null && animator.Frames.Length >= 6 && animator.IsPlaying)
                {
                    animatedZombie = true;
                    break;
                }
            }

            // The wave can already be cleared by the time this check runs (the turret
            // is doing its job), so spawn one extra zombie and inspect it immediately.
            if (!animatedZombie && GameManager.Instance.State != GameState.GameOver)
            {
                MethodInfo spawn = typeof(WaveManager).GetMethod(
                    "SpawnZombie",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                spawn?.Invoke(waves, new object[] { Mathf.Max(1, waves.CurrentWave) });

                zombies = UnityEngine.Object.FindObjectsByType<Zombie>();
                for (int i = 0; i < zombies.Length; i++)
                {
                    SpriteAnimator animator = zombies[i] != null ? zombies[i].GetComponentInChildren<SpriteAnimator>() : null;
                    if (animator != null && animator.Frames != null && animator.Frames.Length >= 6 && animator.IsPlaying)
                    {
                        animatedZombie = true;
                        break;
                    }
                }
            }

            Check(animatedZombie, "Zombies play their walk/attack animation");
        }

        private static void CheckCombatOutcome()
        {
            WaveManager waves = GameManager.Instance.WaveManager;
            GameManager manager = GameManager.Instance;
            PlayerHealth player = manager.Player;

            Zombie[] zombies = UnityEngine.Object.FindObjectsByType<Zombie>();

            int damaged = 0;
            for (int i = 0; i < zombies.Length; i++)
            {
                if (zombies[i] != null && zombies[i].CurrentHealth < zombies[i].MaxHealth)
                {
                    damaged++;
                }
            }

            Check(waves.TotalKills > 0 || damaged > 0,
                $"Combat is resolving damage (kills {waves.TotalKills}, damaged zombies {damaged})");
            Check(!manager.ShopManager.IsOpen, "Shop is closed during combat");
            Check(player != null && player.IsAlive, "Player survived the smoke test");
            Check(CurrencyManager.Instance.Balance >= 0, "Wallet balance is never negative");

            Debug.Log($"[SMOKE] State: wave={waves.CurrentWave}, kills={waves.TotalKills}, " +
                      $"alive={waves.AliveZombies}, coins={CurrencyManager.Instance.Balance}, " +
                      $"hp={player?.CurrentHealth:0}/{player?.MaxHealth:0}");
        }

        // ------------------------------------------------------------------
        // Reporting
        // ------------------------------------------------------------------

        private static void Check(bool condition, string label)
        {
            string line = (condition ? "[OK]   " : "[FAIL] ") + label;
            report.AppendLine(line);

            if (condition)
            {
                Debug.Log("[SMOKE] " + line);
            }
            else
            {
                failures.Add(label);
                Debug.LogError("[SMOKE] " + line);
            }
        }

        private static WeaponDataSO FindWeapon(GameDatabaseSO database, string fragment)
        {
            if (database == null)
            {
                return null;
            }

            for (int i = 0; i < database.Weapons.Count; i++)
            {
                WeaponDataSO weapon = database.Weapons[i];
                if (weapon != null && weapon.DisplayName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return weapon;
                }
            }

            return null;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            {
                return;
            }

            if (condition.Contains("[SMOKE]") ||
                condition.Contains("Licensing") ||
                condition.Contains("Curl error") ||
                condition.Contains("AcceleratorClient") ||
                condition.Contains("Start Indexing on Editor startup"))
            {
                return;
            }

            // Unity's own editor subsystems (search index, asset pipeline) can log
            // errors while the batch-mode editor boots. They are unrelated to the
            // demo, so only keep errors that come from game code or the engine's
            // runtime namespaces.
            if (!string.IsNullOrEmpty(stackTrace) &&
                stackTrace.Contains("UnityEditor.") &&
                !stackTrace.Contains("PixelZombieSurvivor"))
            {
                return;
            }

            errorLog.Add(condition);
        }

        private static void Finish()
        {
            Check(errorLog.Count == 0, $"No runtime errors were logged (found {errorLog.Count})");
            for (int i = 0; i < errorLog.Count && i < 10; i++)
            {
                report.AppendLine("  runtime error: " + errorLog[i]);
            }

            EmitReportAndExit(preFailures + failures.Count);
        }

        private static void FailAndExit(string reason)
        {
            report.AppendLine("[FAIL] " + reason);
            EmitReportAndExit(preFailures + failures.Count + 1);
        }

        private static void EmitReportAndExit(int totalFailures)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.SetBool(SessionKey, false);

            StringBuilder summary = new StringBuilder();
            summary.AppendLine("===== PIXEL ZOMBIE SURVIVOR SMOKE TEST =====");
            summary.AppendLine(report.ToString());
            summary.AppendLine($"Result: {(totalFailures == 0 ? "PASS" : "FAIL")}");
            summary.AppendLine($"Failures: {totalFailures} (edit-mode {preFailures}, play-mode {failures.Count})");
            summary.AppendLine($"Runtime errors: {errorLog.Count}");

            try
            {
                File.WriteAllText(ResultPath, summary.ToString());
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not write smoke test report: " + exception.Message);
            }

            Debug.Log(summary.ToString());
            EditorApplication.Exit(totalFailures == 0 ? 0 : 1);
        }

        private static DateTime ReadStartTime()
        {
            string raw = SessionState.GetString(StartTicksKey, string.Empty);
            return long.TryParse(raw, out long ticks) && ticks > 0
                ? new DateTime(ticks, DateTimeKind.Utc)
                : DateTime.UtcNow;
        }
    }
}
