using System.Collections.Generic;
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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PixelZombieSurvivor.Editor
{
    /// <summary>
    /// One-click project generator: pixel sprites, ScriptableObjects, prefabs,
    /// an arena scene and build settings. Run from Tools > Pixel Zombie Survivor.
    /// </summary>
    public static class DemoProjectBuilder
    {
        public const string RootFolder = "Assets/PixelZombieSurvivor";
        public const string DataFolder = RootFolder + "/Data";
        public const string PrefabFolder = RootFolder + "/Prefabs";
        public const string SceneFolder = RootFolder + "/Scenes";
        public const string ScenePath = SceneFolder + "/Arena.unity";
        public const string DatabaseFolder = RootFolder + "/Resources/PixelZombieSurvivor";
        public const string DatabasePath = DatabaseFolder + "/GameDatabase.asset";

        private static readonly Vector2 CameraBounds = new Vector2(19.5f, 11.5f);

        [MenuItem("Tools/Pixel Zombie Survivor/Build Demo Project", priority = 10)]
        public static void BuildAll()
        {
            EnsureFolders();

            Dictionary<string, Sprite> sprites = PixelArtGenerator.GenerateAll();

            GameObject playerPrefab = CreatePlayerPrefab(sprites);
            GameObject zombiePrefab = CreateZombiePrefab(sprites);
            GameObject bossPrefab = CreateBossPrefab(sprites);
            GameObject bulletPrefab = CreateBulletPrefab(sprites);
            GameObject coinPrefab = CreateCoinPrefab(sprites);
            GameObject turretPrefab = CreateTurretPrefab(sprites);
            GameObject heavyTurretPrefab = CreateHeavyTurretPrefab(sprites);
            GameObject wallPrefab = CreateWallBuildingPrefab(sprites);
            GameObject generatorPrefab = CreateGeneratorPrefab(sprites);
            GameObject repairPrefab = CreateRepairPrefab(sprites);

            WaveSettingsSO waveSettings = CreateWaveSettings();
            WeaponDataSO pistol = CreatePistol(sprites);
            WeaponDataSO smg = CreateSmg(sprites);
            WeaponDataSO ak = CreateAk(sprites);
            WeaponDataSO m4 = CreateM4(sprites);
            WeaponDataSO lmg = CreateLmg(sprites);
            WeaponDataSO shotgun = CreateShotgun(sprites);

            ZombieDataSO walker = CreateWalker(sprites);
            ZombieDataSO runner = CreateRunner(sprites);
            ZombieDataSO brute = CreateBrute(sprites);
            ZombieDataSO spitter = CreateSpitter(sprites);

            BuildingDataSO turretData = CreateTurretData(sprites, turretPrefab);
            BuildingDataSO heavyTurretData = CreateHeavyTurretData(sprites, heavyTurretPrefab);
            BuildingDataSO wallData = CreateWallData(sprites, wallPrefab);
            BuildingDataSO generatorData = CreateGeneratorData(sprites, generatorPrefab);
            BuildingDataSO repairData = CreateRepairData(sprites, repairPrefab);

            ConsumableDataSO healthPack = CreateHealthPack(sprites);
            ConsumableDataSO ammoBox = CreateAmmoBox(sprites);

            ConfigureWaveSettings(waveSettings, walker, runner, brute, spitter);

            GameDatabaseSO database = CreateDatabase(
                new List<WeaponDataSO> { pistol, smg, ak, m4, lmg, shotgun },
                new List<BuildingDataSO> { wallData, turretData, heavyTurretData, generatorData, repairData },
                new List<ConsumableDataSO> { healthPack, ammoBox },
                waveSettings,
                new List<ZombieDataSO> { walker, runner, brute, spitter },
                pistol,
                zombiePrefab,
                bossPrefab,
                bulletPrefab,
                coinPrefab);

            CreateScene(
                sprites,
                database,
                waveSettings,
                playerPrefab,
                healthPack,
                ammoBox);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            if (!Application.isBatchMode)
            {
                EditorSceneManager.OpenScene(ScenePath);
                EditorUtility.DisplayDialog(
                    "Pixel Zombie Survivor",
                    "Проект собран. Откройте Scenes/Arena.unity и нажмите Play.",
                    "Отлично");
            }

            Debug.Log("[Pixel Zombie Survivor] Demo project generated successfully.");
        }

        [MenuItem("Tools/Pixel Zombie Survivor/Open Arena Scene", priority = 20)]
        public static void OpenArenaScene()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                BuildAll();
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        private static void EnsureFolders()
        {
            PixelArtGenerator.EnsureFolder(RootFolder);
            PixelArtGenerator.EnsureFolder(DataFolder);
            PixelArtGenerator.EnsureFolder(PrefabFolder);
            PixelArtGenerator.EnsureFolder(SceneFolder);
            PixelArtGenerator.EnsureFolder(DatabaseFolder);
        }

        private static GameObject CreatePlayerPrefab(Dictionary<string, Sprite> sprites)
        {
            GameObject root = new GameObject("Player");
            SpriteRenderer bodyRenderer = root.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = sprites["player"];
            bodyRenderer.sortingOrder = 10;

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CapsuleCollider2D collider = root.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.5f, 0.62f);
            collider.offset = new Vector2(0f, -0.04f);

            root.AddComponent<PlayerInputReader>();
            PlayerHealth health = root.AddComponent<PlayerHealth>();
            PlayerWeaponController weaponController = root.AddComponent<PlayerWeaponController>();
            root.AddComponent<PlayerController>();

            SpriteAnimator animator = root.AddComponent<SpriteAnimator>();
            PlayerVisualController visuals = root.AddComponent<PlayerVisualController>();
            SetSpriteArray(animator, "frames", CollectFrames(sprites, "player_idle", 2));
            SetFloat(animator, "framesPerSecond", 3f);
            SetSpriteArray(visuals, "idleFrames", CollectFrames(sprites, "player_idle", 2));
            SetSpriteArray(visuals, "walkFrames", CollectFrames(sprites, "player_walk", 8));
            SetFloat(visuals, "idleFps", 3f);
            SetFloat(visuals, "walkFps", 10f);
            SetObject(visuals, "animator", animator);

            GameObject shadow = new GameObject("Shadow");
            shadow.transform.SetParent(root.transform, false);
            shadow.transform.localPosition = new Vector3(0f, -0.34f, 0f);
            SpriteRenderer shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = sprites["player_shadow"];
            shadowRenderer.sortingOrder = 9;

            GameObject pivot = new GameObject("WeaponPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 0.06f, 0f);

            GameObject weaponVisual = new GameObject("WeaponVisual");
            weaponVisual.transform.SetParent(pivot.transform, false);
            SpriteRenderer weaponRenderer = weaponVisual.AddComponent<SpriteRenderer>();
            weaponRenderer.sprite = sprites["weapon_pistol"];
            weaponRenderer.sortingOrder = 12;

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(pivot.transform, false);
            muzzle.transform.localPosition = new Vector3(0.64f, 0f, 0f);

            SetInt(health, "faction", (int)Faction.Player);
            SetFloat(health, "maxHealth", 100f);
            SetObject(weaponController, "weaponPivot", pivot.transform);
            SetObject(weaponController, "muzzle", muzzle.transform);
            SetObject(weaponController, "weaponVisual", weaponRenderer);

            GameObject prefab = SavePrefab(root, $"{PrefabFolder}/Player.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateZombiePrefab(Dictionary<string, Sprite> sprites)
        {
            GameObject root = new GameObject("Zombie");
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CapsuleCollider2D collider = root.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.5f, 0.72f);
            collider.offset = new Vector2(0f, -0.04f);

            GameObject visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(root.transform, false);
            SpriteRenderer visual = visualObject.AddComponent<SpriteRenderer>();
            visual.sprite = sprites["zombie_walker"];
            visual.sortingOrder = 8;

            SpriteAnimator animator = visualObject.AddComponent<SpriteAnimator>();
            SetObject(animator, "target", visual);
            SetSpriteArray(animator, "frames", CollectFrames(sprites, "zombie_walker_walk", 8));
            SetFloat(animator, "framesPerSecond", 9f);

            GameObject shadow = new GameObject("Shadow");
            shadow.transform.SetParent(root.transform, false);
            shadow.transform.localPosition = new Vector3(0f, -0.34f, 0f);
            SpriteRenderer shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = sprites["player_shadow"];
            shadowRenderer.color = new Color(1f, 1f, 1f, 0.7f);
            shadowRenderer.sortingOrder = 7;

            Zombie zombie = root.AddComponent<Zombie>();
            SetInt(zombie, "faction", (int)Faction.Enemy);
            SetObject(zombie, "visual", visual);
            SetObject(zombie, "body", body);
            SetObject(zombie, "bodyCollider", collider);

            GameObject prefab = SavePrefab(root, $"{PrefabFolder}/Zombie.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateBossPrefab(Dictionary<string, Sprite> sprites)
        {
            GameObject root = new GameObject("Boss");
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites["boss"];
            renderer.sortingOrder = 9;

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.78f;
            collider.offset = new Vector2(0f, -0.12f);

            GameObject shadow = new GameObject("Shadow");
            shadow.transform.SetParent(root.transform, false);
            shadow.transform.localPosition = new Vector3(0f, -0.62f, 0f);
            shadow.transform.localScale = new Vector3(2f, 1.4f, 1f);
            SpriteRenderer shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = sprites["player_shadow"];
            shadowRenderer.color = new Color(1f, 1f, 1f, 0.65f);
            shadowRenderer.sortingOrder = 7;

            BossController boss = root.AddComponent<BossController>();
            SetInt(boss, "faction", (int)Faction.Enemy);
            SetFloat(boss, "maxHealth", 4200f);
            SetObject(boss, "visual", renderer);
            SetObject(boss, "body", body);
            SetObject(boss, "bodyCollider", collider);

            GameObject prefab = SavePrefab(root, $"{PrefabFolder}/Boss.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateBulletPrefab(Dictionary<string, Sprite> sprites)
        {
            GameObject root = new GameObject("Bullet");
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites["bullet"];
            renderer.sortingOrder = 20;

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.09f;
            collider.isTrigger = true;

            root.AddComponent<Projectile>();
            root.AddComponent<PooledObject>();

            GameObject prefab = SavePrefab(root, $"{PrefabFolder}/Bullet.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateCoinPrefab(Dictionary<string, Sprite> sprites)
        {
            GameObject root = new GameObject("Coin");
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.22f;
            collider.isTrigger = true;

            GameObject visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(root.transform, false);
            SpriteRenderer visual = visualObject.AddComponent<SpriteRenderer>();
            visual.sprite = sprites["coin"];
            visual.sortingOrder = 15;

            Coin coin = root.AddComponent<Coin>();
            SetObject(coin, "visual", visualObject.transform);
            root.AddComponent<PooledObject>();

            GameObject prefab = SavePrefab(root, $"{PrefabFolder}/Coin.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateTurretPrefab(Dictionary<string, Sprite> sprites)
        {
            GameObject root = CreateBuildingRoot("Turret", sprites["turret_base"], out SpriteRenderer _);
            root.AddComponent<Turret>();

            GameObject barrel = new GameObject("Barrel");
            barrel.transform.SetParent(root.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            SpriteRenderer barrelRenderer = barrel.AddComponent<SpriteRenderer>();
            barrelRenderer.sprite = sprites["turret_barrel"];
            barrelRenderer.sortingOrder = 5;

            Turret turret = root.GetComponent<Turret>();
            SetObject(turret, "barrel", barrel.transform);

            GameObject prefab = SavePrefab(root, $"{PrefabFolder}/Turret.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateHeavyTurretPrefab(Dictionary<string, Sprite> sprites)
        {
            GameObject root = CreateBuildingRoot("HeavyTurret", sprites["turret_heavy"], out SpriteRenderer _);
            root.AddComponent<Turret>();

            GameObject barrel = new GameObject("Barrel");
            barrel.transform.SetParent(root.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            SpriteRenderer barrelRenderer = barrel.AddComponent<SpriteRenderer>();
            barrelRenderer.sprite = sprites["turret_barrel"];
            barrelRenderer.color = new Color(1f, 0.72f, 0.58f, 1f);
            barrelRenderer.sortingOrder = 5;
            barrel.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            Turret turret = root.GetComponent<Turret>();
            SetObject(turret, "barrel", barrel.transform);

            GameObject prefab = SavePrefab(root, $"{PrefabFolder}/HeavyTurret.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateWallBuildingPrefab(Dictionary<string, Sprite> sprites)
        {
            GameObject root = CreateBuildingRoot("Wall", sprites["wall"], out SpriteRenderer renderer);
            renderer.sortingOrder = 3;

            GameObject prefab = SavePrefab(root, $"{PrefabFolder}/Wall.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateGeneratorPrefab(Dictionary<string, Sprite> sprites)
        {
            GameObject root = CreateBuildingRoot("MoneyGenerator", sprites["money_generator"], out SpriteRenderer _);
            root.AddComponent<MoneyGenerator>();

            GameObject prefab = SavePrefab(root, $"{PrefabFolder}/MoneyGenerator.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateRepairPrefab(Dictionary<string, Sprite> sprites)
        {
            GameObject root = CreateBuildingRoot("RepairStation", sprites["repair_station"], out SpriteRenderer _);
            root.AddComponent<RepairStation>();

            GameObject prefab = SavePrefab(root, $"{PrefabFolder}/RepairStation.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateBuildingRoot(string objectName, Sprite sprite, out SpriteRenderer renderer)
        {
            GameObject root = new GameObject(objectName);
            renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 4;

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.05f, 1.05f);

            BuildingBase building = root.AddComponent<BuildingBase>();
            SetInt(building, "faction", (int)Faction.Player);

            WorldHealthBar healthBar = root.AddComponent<WorldHealthBar>();
            SetVector3(healthBar, "offset", new Vector3(0f, 0.82f, 0f));
            SetVector2(healthBar, "barSize", new Vector2(1.25f, 0.11f));

            return root;
        }

        private static WaveSettingsSO CreateWaveSettings()
        {
            WaveSettingsSO asset = CreateAsset<WaveSettingsSO>($"{DataFolder}/WaveSettings.asset");
            return asset;
        }

        private static void ConfigureWaveSettings(
            WaveSettingsSO settings,
            ZombieDataSO walker,
            ZombieDataSO runner,
            ZombieDataSO brute,
            ZombieDataSO spitter)
        {
            settings.EditorConfigure(
                8f,
                10f,
                0.62f,
                6,
                1,
                70,
                10,
                0.12f,
                0.07f,
                0.025f,
                1.25f,
                0.10f,
                new List<ZombieSpawnEntry>
                {
                    new ZombieSpawnEntry(walker, 1, 1f),
                    new ZombieSpawnEntry(runner, 2, 0.5f),
                    new ZombieSpawnEntry(brute, 4, 0.22f),
                    new ZombieSpawnEntry(spitter, 6, 0.3f)
                });
            MarkAssetDirty(settings);
        }

        private static WeaponDataSO CreatePistol(Dictionary<string, Sprite> sprites)
        {
            WeaponDataSO asset = CreateAsset<WeaponDataSO>($"{DataFolder}/Weapon_Pistol.asset");
            asset.EditorConfigure(
                "Пистолет", "Надёжный стартовый пистолет. Бесплатный и всегда с вами.",
                sprites["weapon_pistol"], sprites["weapon_pistol"], 0, true,
                14f, 3.2f, 12, 1.35f, 3f, 14f, 24f, 1, false,
                Colour(0xFFD166), 120, 360, 80, 1.05f);
            MarkAssetDirty(asset);
            return asset;
        }

        private static WeaponDataSO CreateSmg(Dictionary<string, Sprite> sprites)
        {
            WeaponDataSO asset = CreateAsset<WeaponDataSO>($"{DataFolder}/Weapon_SMG.asset");
            asset.EditorConfigure(
                "SMG", "Скорострельный пистолет-пулемёт. Малый урон, отличная плотность огня.",
                sprites["weapon_smg"], sprites["weapon_smg"], 300, false,
                7f, 10f, 30, 1.65f, 7.5f, 11.5f, 22f, 1, true,
                Colour(0xFFC857), 150, 420, 120, 1f);
            MarkAssetDirty(asset);
            return asset;
        }

        private static WeaponDataSO CreateAk(Dictionary<string, Sprite> sprites)
        {
            WeaponDataSO asset = CreateAsset<WeaponDataSO>($"{DataFolder}/Weapon_AK47.asset");
            asset.EditorConfigure(
                "AK-47", "Тяжёлый автомат с высоким уроном и заметной отдачей.",
                sprites["weapon_ak47"], sprites["weapon_ak47"], 900, false,
                24f, 6f, 30, 2.05f, 5.5f, 16f, 26f, 1, true,
                Colour(0xFFB347), 180, 480, 140, 0.95f);
            MarkAssetDirty(asset);
            return asset;
        }

        private static WeaponDataSO CreateM4(Dictionary<string, Sprite> sprites)
        {
            WeaponDataSO asset = CreateAsset<WeaponDataSO>($"{DataFolder}/Weapon_M4A1.asset");
            asset.EditorConfigure(
                "M4A1", "Точный автомат с хорошим уроном и контролируемым разбросом.",
                sprites["weapon_m4a1"], sprites["weapon_m4a1"], 1250, false,
                18f, 8.5f, 30, 1.75f, 2.2f, 18f, 28f, 1, true,
                Colour(0xFFE29A), 180, 480, 140, 0.97f);
            MarkAssetDirty(asset);
            return asset;
        }

        private static WeaponDataSO CreateLmg(Dictionary<string, Sprite> sprites)
        {
            WeaponDataSO asset = CreateAsset<WeaponDataSO>($"{DataFolder}/Weapon_LMG.asset");
            asset.EditorConfigure(
                "LMG", "Пулемёт для настоящего ада. Огромный магазин, высокий урон, долгая перезарядка.",
                sprites["weapon_lmg"], sprites["weapon_lmg"], 2100, false,
                30f, 8f, 100, 4.4f, 7f, 17f, 24f, 1, true,
                Colour(0xFFAA55), 260, 600, 180, 0.82f);
            MarkAssetDirty(asset);
            return asset;
        }

        private static WeaponDataSO CreateShotgun(Dictionary<string, Sprite> sprites)
        {
            WeaponDataSO asset = CreateAsset<WeaponDataSO>($"{DataFolder}/Weapon_Shotgun.asset");
            asset.EditorConfigure(
                "Дробовик", "Семь картечин за выстрел. Уничтожает толпу в упор, бесполезен на дистанции.",
                sprites["weapon_shotgun"], sprites["weapon_shotgun"], 1400, false,
                11f, 1.4f, 6, 2.3f, 18f, 10f, 15f, 7, false,
                Colour(0xFFB066), 36, 90, 24, 0.94f);
            MarkAssetDirty(asset);
            return asset;
        }

        private static ZombieDataSO CreateWalker(Dictionary<string, Sprite> sprites)
        {
            ZombieDataSO asset = CreateAsset<ZombieDataSO>($"{DataFolder}/Zombie_Walker.asset");
            asset.EditorConfigure("Ходок", sprites["zombie_walker"], Color.white, 1f, 32f, 2f, 8f, 0.85f, 0.8f, 10,
                false, 9f, 0, Color.white);
            asset.EditorSetAnimation(
                CollectFrames(sprites, "zombie_walker_walk", 8),
                CollectFrames(sprites, "zombie_walker_attack", 6),
                9f,
                11f);
            MarkAssetDirty(asset);
            return asset;
        }

        private static ZombieDataSO CreateRunner(Dictionary<string, Sprite> sprites)
        {
            ZombieDataSO asset = CreateAsset<ZombieDataSO>($"{DataFolder}/Zombie_Runner.asset");
            asset.EditorConfigure("Бегун", sprites["zombie_runner"], Color.white, 0.9f, 20f, 3.4f, 6f, 0.6f, 0.75f, 9,
                false, 9f, 0, Color.white);
            asset.EditorSetAnimation(
                CollectFrames(sprites, "zombie_runner_walk", 8),
                CollectFrames(sprites, "zombie_runner_attack", 6),
                12f,
                14f);
            MarkAssetDirty(asset);
            return asset;
        }

        private static ZombieDataSO CreateBrute(Dictionary<string, Sprite> sprites)
        {
            ZombieDataSO asset = CreateAsset<ZombieDataSO>($"{DataFolder}/Zombie_Brute.asset");
            asset.EditorConfigure("Громила", sprites["zombie_brute"], Color.white, 1.35f, 110f, 1.5f, 18f, 1.2f, 0.95f, 35,
                false, 9f, 0, Color.white);
            asset.EditorSetAnimation(
                CollectFrames(sprites, "zombie_brute_walk", 8),
                CollectFrames(sprites, "zombie_brute_attack", 6),
                7f,
                9f);
            MarkAssetDirty(asset);
            return asset;
        }

        private static ZombieDataSO CreateSpitter(Dictionary<string, Sprite> sprites)
        {
            ZombieDataSO asset = CreateAsset<ZombieDataSO>($"{DataFolder}/Zombie_Spitter.asset");
            asset.EditorConfigure("Плевальщик", sprites["zombie_spitter"], Color.white, 1f, 46f, 1.8f, 11f, 1.7f, 6.5f, 16,
                true, 8.5f, 1, Colour(0xB6FF4B));
            asset.EditorSetAnimation(
                CollectFrames(sprites, "zombie_spitter_walk", 8),
                CollectFrames(sprites, "zombie_spitter_attack", 6),
                8f,
                10f);
            MarkAssetDirty(asset);
            return asset;
        }

        private static BuildingDataSO CreateTurretData(Dictionary<string, Sprite> sprites, GameObject prefab)
        {
            BuildingDataSO asset = CreateAsset<BuildingDataSO>($"{DataFolder}/Building_Turret.asset");
            asset.EditorConfigure(
                "Боевая турель", "Автоматически наводится и стреляет по зомби в радиусе. Основная защита базы.",
                BuildingType.Turret, sprites["turret_base"], prefab, 250, 160f, 0.7f, 1.4f,
                9f, 2.2f, 8.5f, 18f, 540f, Colour(0x5CFFC2),
                8, 6f, 4, 12f, 4f,
                5, 110, 1.5f, 0.25f, 0.12f, 0.06f, 0.25f, 0.3f, 0.25f);
            MarkAssetDirty(asset);
            return asset;
        }

        private static BuildingDataSO CreateHeavyTurretData(Dictionary<string, Sprite> sprites, GameObject prefab)
        {
            BuildingDataSO asset = CreateAsset<BuildingDataSO>($"{DataFolder}/Building_HeavyTurret.asset");
            asset.EditorConfigure(
                "Тяжёлая турель", "Крупнокалиберная турель: больше урона, темпа и радиуса. Дорогая, но держит поздние волны.",
                BuildingType.Turret, sprites["turret_heavy"], prefab, 900, 330f, 0.82f, 1.6f,
                22f, 3.2f, 11f, 22f, 420f, Colour(0xFF7A3D),
                8, 6f, 4, 12f, 4f,
                5, 260, 1.6f, 0.3f, 0.1f, 0.05f, 0.22f, 0.3f, 0.25f);
            MarkAssetDirty(asset);
            return asset;
        }

        private static BuildingDataSO CreateWallData(Dictionary<string, Sprite> sprites, GameObject prefab)
        {
            BuildingDataSO asset = CreateAsset<BuildingDataSO>($"{DataFolder}/Building_Wall.asset");
            asset.EditorConfigure(
                "Стена", "Прочная баррикада: задерживает зомби и прикрывает турели. Ставится вплотную без щелей.",
                BuildingType.Wall, sprites["wall"], prefab, 45, 500f, 0.5f, 1f,
                0f, 1f, 1f, 1f, 1f, Color.white,
                1, 1f, 1, 1f, 1f,
                5, 40, 1.45f, 0f, 0f, 0f, 0.5f, 0f, 0f);
            MarkAssetDirty(asset);
            return asset;
        }

        private static BuildingDataSO CreateGeneratorData(Dictionary<string, Sprite> sprites, GameObject prefab)
        {
            BuildingDataSO asset = CreateAsset<BuildingDataSO>($"{DataFolder}/Building_MoneyGenerator.asset");
            asset.EditorConfigure(
                "Раздатчик денег", "Каждые несколько секунд выбрасывает монеты рядом с собой. Монеты сразу летят в кошелёк.",
                BuildingType.MoneyGenerator, sprites["money_generator"], prefab, 320, 140f, 0.65f, 1.5f,
                0f, 1f, 1f, 1f, 1f, Color.white,
                10, 5.5f, 4, 1f, 1f,
                5, 100, 1.5f, 0f, 0f, 0f, 0.2f, 0.3f, 0.2f);
            MarkAssetDirty(asset);
            return asset;
        }

        private static BuildingDataSO CreateRepairData(Dictionary<string, Sprite> sprites, GameObject prefab)
        {
            BuildingDataSO asset = CreateAsset<BuildingDataSO>($"{DataFolder}/Building_RepairStation.asset");
            asset.EditorConfigure(
                "Ремонтный модуль", "Постепенно чинит все постройки рядом, включая себя.",
                BuildingType.RepairStation, sprites["repair_station"], prefab, 400, 160f, 0.72f, 1.5f,
                0f, 1f, 1f, 1f, 1f, Color.white,
                1, 1f, 1, 12f, 4.5f,
                5, 140, 1.55f, 0f, 0f, 0.05f, 0.2f, 0f, 0.3f);
            MarkAssetDirty(asset);
            return asset;
        }

        private static ConsumableDataSO CreateHealthPack(Dictionary<string, Sprite> sprites)
        {
            ConsumableDataSO asset = CreateAsset<ConsumableDataSO>($"{DataFolder}/Consumable_HealthPack.asset");
            asset.EditorConfigure(
                "Аптечка", "Мгновенно восстанавливает здоровье игрока.",
                ConsumableType.HealthPack, sprites["health_pack"], 100, 50f, 0);
            MarkAssetDirty(asset);
            return asset;
        }

        private static ConsumableDataSO CreateAmmoBox(Dictionary<string, Sprite> sprites)
        {
            ConsumableDataSO asset = CreateAsset<ConsumableDataSO>($"{DataFolder}/Consumable_AmmoBox.asset");
            asset.EditorConfigure(
                "Ящик патронов", "Пополняет запас патронов для всего купленного оружия.",
                ConsumableType.AmmoBox, sprites["ammo_box"], 55, 0f, 150);
            MarkAssetDirty(asset);
            return asset;
        }

        private static GameDatabaseSO CreateDatabase(
            List<WeaponDataSO> weapons,
            List<BuildingDataSO> buildings,
            List<ConsumableDataSO> consumables,
            WaveSettingsSO waveSettings,
            List<ZombieDataSO> zombies,
            WeaponDataSO startingWeapon,
            GameObject zombiePrefab,
            GameObject bossPrefab,
            GameObject bulletPrefab,
            GameObject coinPrefab)
        {
            GameDatabaseSO database = CreateAsset<GameDatabaseSO>(DatabasePath);
            database.EditorSetContents(
                weapons,
                buildings,
                consumables,
                waveSettings,
                zombies,
                450,
                startingWeapon,
                zombiePrefab,
                bossPrefab,
                bulletPrefab,
                coinPrefab);
            MarkAssetDirty(database);
            return database;
        }

        private static void CreateScene(
            Dictionary<string, Sprite> sprites,
            GameDatabaseSO database,
            WaveSettingsSO waveSettings,
            GameObject playerPrefab,
            ConsumableDataSO healthPack,
            ConsumableDataSO ammoBox)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateArena(sprites);

            GameObject playerObject = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            playerObject.name = "Player";
            playerObject.transform.position = new Vector3(0f, 0.35f, 0f);
            PlayerHealth playerHealth = playerObject.GetComponent<PlayerHealth>();

            Camera camera = CreateCamera(playerObject.transform);

            GameObject buildingsRoot = new GameObject("[Buildings]");

            GameObject gridObject = new GameObject("Grid Manager");
            GridManager gridManager = gridObject.AddComponent<GridManager>();

            GameObject systemsObject = new GameObject("Game Systems");
            CurrencyManager currency = systemsObject.AddComponent<CurrencyManager>();
            GameManager gameManager = systemsObject.AddComponent<GameManager>();
            ShopManager shopManager = systemsObject.AddComponent<ShopManager>();
            systemsObject.AddComponent<GameUIBootstrap>();
            SetObject(shopManager, "database", database);

            GameObject poolObject = new GameObject("Pool Service");
            poolObject.AddComponent<PoolService>();

            GameObject placementObject = new GameObject("Building Placement");
            BuildingPlacementController placement = placementObject.AddComponent<BuildingPlacementController>();
            SetObject(placement, "buildingParent", buildingsRoot.transform);
            SetObject(placement, "grid", gridManager);

            GameObject waveObject = new GameObject("Wave Manager");
            WaveManager waveManager = waveObject.AddComponent<WaveManager>();
            SetObject(waveManager, "player", playerHealth);
            SetObject(waveManager, "overrideSettings", waveSettings);

            GameObject spawnRoot = new GameObject("Spawn Points");
            spawnRoot.transform.SetParent(waveObject.transform, false);
            Vector2[] spawnPositions =
            {
                new Vector2(-17f, -9.2f), new Vector2(-8.5f, -9.8f), new Vector2(0f, -9.8f),
                new Vector2(8.5f, -9.8f), new Vector2(17f, -9.2f), new Vector2(17f, 0f),
                new Vector2(17f, 9.2f), new Vector2(8.5f, 9.8f), new Vector2(0f, 9.8f),
                new Vector2(-8.5f, 9.8f), new Vector2(-17f, 9.2f), new Vector2(-17f, 0f)
            };

            for (int i = 0; i < spawnPositions.Length; i++)
            {
                GameObject spawnPointObject = new GameObject($"SpawnPoint_{i + 1:00}");
                spawnPointObject.transform.SetParent(spawnRoot.transform, false);
                spawnPointObject.transform.position = spawnPositions[i];
                spawnPointObject.AddComponent<SpawnPoint>();
            }

            SetObject(gameManager, "player", playerHealth);
            SetObject(gameManager, "waveManager", waveManager);
            SetObject(gameManager, "shopManager", shopManager);
            SetObject(gameManager, "placementController", placement);

            CameraFollow2D follow = camera.GetComponent<CameraFollow2D>();
            follow.Configure(playerObject.transform, Vector2.zero, CameraBounds);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = playerObject;

            Debug.Log($"[Pixel Zombie Survivor] Created scene at {ScenePath}. Shop stock: {healthPack.DisplayName}, {ammoBox.DisplayName}.");
        }

        private static void CreateArena(Dictionary<string, Sprite> sprites)
        {
            GameObject arena = new GameObject("Arena");

            GameObject ground = new GameObject("Ground");
            ground.transform.SetParent(arena.transform, false);
            SpriteRenderer groundRenderer = ground.AddComponent<SpriteRenderer>();
            groundRenderer.sprite = sprites["ground"];
            groundRenderer.drawMode = SpriteDrawMode.Tiled;
            groundRenderer.tileMode = SpriteTileMode.Continuous;
            groundRenderer.size = new Vector2(40f, 24f);
            groundRenderer.sortingOrder = -100;

            CreateWall(arena.transform, "Wall_Top", new Vector2(0f, 11.5f), new Vector2(40f, 1f), sprites["wall"]);
            CreateWall(arena.transform, "Wall_Bottom", new Vector2(0f, -11.5f), new Vector2(40f, 1f), sprites["wall"]);
            CreateWall(arena.transform, "Wall_Left", new Vector2(-19.5f, 0f), new Vector2(1f, 24f), sprites["wall"]);
            CreateWall(arena.transform, "Wall_Right", new Vector2(19.5f, 0f), new Vector2(1f, 24f), sprites["wall"]);
        }

        private static void CreateWall(Transform parent, string objectName, Vector2 position, Vector2 size, Sprite sprite)
        {
            GameObject wall = new GameObject(objectName);
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;

            SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = size;
            renderer.sortingOrder = -20;

            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.size = size;
            wall.AddComponent<ProjectileBlocker>();
        }

        private static Camera CreateCamera(Transform target)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.045f, 1f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            cameraObject.AddComponent<AudioListener>();

            CameraFollow2D follow = cameraObject.AddComponent<CameraFollow2D>();
            follow.Configure(target, Vector2.zero, CameraBounds);
            return camera;
        }

        private static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            AssetDatabase.DeleteAsset(path);
            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>
        /// Marks an asset dirty and flushes it to disk right away. Batch-mode runs end
        /// with -quit, so the deferred AssetDatabase.SaveAssets pass alone can leave
        /// the freshly configured values only in memory while the .asset file keeps
        /// its default field values.
        /// </summary>
        private static void MarkAssetDirty(Object target)
        {
            if (target == null)
            {
                return;
            }

            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            return prefab;
        }

        private static Sprite[] CollectFrames(Dictionary<string, Sprite> sprites, string prefix, int count)
        {
            Sprite[] frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                if (!sprites.TryGetValue($"{prefix}_{i}", out Sprite sprite))
                {
                    Debug.LogError($"Missing generated animation frame '{prefix}_{i}'.");
                    continue;
                }

                frames[i] = sprite;
            }

            return frames;
        }

        private static Color Colour(int rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                1f);
        }

        private static SerializedProperty FindProperty(Component component, string propertyName)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"Property '{propertyName}' was not found on {component.GetType().Name}.", component);
                return null;
            }

            return property;
        }

        private static void SetObject(Component component, string propertyName, Object value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogError($"Property '{propertyName}' was not found on {component.GetType().Name}.", component);
            }
        }

        private static void SetInt(Component component, string propertyName, int value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogError($"Property '{propertyName}' was not found on {component.GetType().Name}.", component);
            }
        }

        private static void SetFloat(Component component, string propertyName, float value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogError($"Property '{propertyName}' was not found on {component.GetType().Name}.", component);
            }
        }

        private static void SetVector2(Component component, string propertyName, Vector2 value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.vector2Value = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogError($"Property '{propertyName}' was not found on {component.GetType().Name}.", component);
            }
        }

        private static void SetVector3(Component component, string propertyName, Vector3 value)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.vector3Value = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogError($"Property '{propertyName}' was not found on {component.GetType().Name}.", component);
            }
        }

        private static void SetSpriteArray(Object target, string propertyName, Sprite[] values)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"Property '{propertyName}' was not found on {target.GetType().Name}.", target);
                return;
            }

            property.arraySize = values != null ? values.Length : 0;
            for (int i = 0; i < property.arraySize; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
