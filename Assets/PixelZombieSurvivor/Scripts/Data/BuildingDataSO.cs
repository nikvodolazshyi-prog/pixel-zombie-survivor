using UnityEngine;

namespace PixelZombieSurvivor.Data
{
    public enum BuildingType
    {
        Turret = 0,
        MoneyGenerator = 1,
        RepairStation = 2,
        Wall = 3
    }

    /// <summary>
    /// Data for every deployable. Turret, generator, repair and wall fields share one
    /// asset so a designer can create a variant without touching a prefab.
    /// Upgrade multipliers are applied per level by the runtime building components.
    /// </summary>
    [CreateAssetMenu(fileName = "Building_New", menuName = "Pixel Zombie Survivor/Building Data")]
    public sealed class BuildingDataSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "New Building";
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private BuildingType buildingType;
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject prefab;

        [Header("Common")]
        [SerializeField, Min(0)] private int price = 250;
        [SerializeField, Min(1f)] private float maxHealth = 120f;
        [SerializeField, Min(0.1f)] private float placementRadius = 0.5f;
        [SerializeField, Min(0.1f)] private float placementDistanceFromPlayer = 1.2f;

        [Header("Turret")]
        [SerializeField, Min(0f)] private float turretDamage = 8f;
        [SerializeField, Min(0.1f)] private float turretFireRate = 2.2f;
        [SerializeField, Min(0.5f)] private float turretRange = 8f;
        [SerializeField, Min(0.1f)] private float turretProjectileSpeed = 16f;
        [SerializeField, Min(1f)] private float turretRotationSpeed = 540f;
        [SerializeField] private Color turretProjectileColor = new Color(0.35f, 1f, 0.75f, 1f);

        [Header("Money Generator")]
        [SerializeField, Min(1)] private int generatorCoinAmount = 8;
        [SerializeField, Min(0.5f)] private float generatorInterval = 6f;
        [SerializeField, Min(1)] private int generatorBurstSize = 4;

        [Header("Repair Station")]
        [SerializeField, Min(1f)] private float repairPerSecond = 12f;
        [SerializeField, Min(0.5f)] private float repairRadius = 4f;

        [Header("Upgrades")]
        [SerializeField, Min(1)] private int maxLevel = 5;
        [SerializeField, Min(1)] private int upgradeBaseCost = 120;
        [SerializeField, Min(1f)] private float upgradeCostGrowth = 1.55f;
        [SerializeField, Min(0f)] private float damagePerLevel = 0.25f;
        [SerializeField, Min(0f)] private float fireRatePerLevel = 0.12f;
        [SerializeField, Min(0f)] private float rangePerLevel = 0.06f;
        [SerializeField, Min(0f)] private float healthPerLevel = 0.25f;
        [SerializeField, Min(0f)] private float generatorPerLevel = 0.3f;
        [SerializeField, Min(0f)] private float repairPerLevel = 0.25f;
        [SerializeField, Range(0.1f, 1f)] private float sellRefund = 0.6f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public BuildingType BuildingType => buildingType;
        public Sprite Icon => icon;
        public GameObject Prefab => prefab;
        public int Price => price;
        public float MaxHealth => maxHealth;
        public float PlacementRadius => placementRadius;
        public float PlacementDistanceFromPlayer => placementDistanceFromPlayer;
        public float TurretDamage => turretDamage;
        public float TurretFireRate => turretFireRate;
        public float TurretShotInterval => 1f / Mathf.Max(0.01f, turretFireRate);
        public float TurretRange => turretRange;
        public float TurretProjectileSpeed => turretProjectileSpeed;
        public float TurretRotationSpeed => turretRotationSpeed;
        public Color TurretProjectileColor => turretProjectileColor;
        public int GeneratorCoinAmount => generatorCoinAmount;
        public float GeneratorInterval => generatorInterval;
        public int GeneratorBurstSize => generatorBurstSize;
        public float RepairPerSecond => repairPerSecond;
        public float RepairRadius => repairRadius;
        public int MaxLevel => Mathf.Max(1, maxLevel);
        public float SellRefund => Mathf.Clamp(sellRefund, 0.1f, 1f);

        public float GetMaxHealth(int level)
        {
            return Mathf.Max(1f, maxHealth * (1f + healthPerLevel * Mathf.Max(0, level - 1)));
        }

        public float GetTurretDamage(int level)
        {
            return Mathf.Max(0f, turretDamage * (1f + damagePerLevel * Mathf.Max(0, level - 1)));
        }

        public float GetTurretFireRate(int level)
        {
            return Mathf.Max(0.1f, turretFireRate * (1f + fireRatePerLevel * Mathf.Max(0, level - 1)));
        }

        public float GetTurretShotInterval(int level)
        {
            return 1f / GetTurretFireRate(level);
        }

        public float GetTurretRange(int level)
        {
            return Mathf.Max(0.5f, turretRange * (1f + rangePerLevel * Mathf.Max(0, level - 1)));
        }

        public int GetGeneratorCoinAmount(int level)
        {
            int amount = Mathf.RoundToInt(generatorCoinAmount * (1f + generatorPerLevel * Mathf.Max(0, level - 1)));
            return Mathf.Max(1, amount);
        }

        public float GetRepairPerSecond(int level)
        {
            return Mathf.Max(1f, repairPerSecond * (1f + repairPerLevel * Mathf.Max(0, level - 1)));
        }

        public float GetRepairRadius(int level)
        {
            return Mathf.Max(0.5f, repairRadius * (1f + rangePerLevel * Mathf.Max(0, level - 1)));
        }

        /// <summary>Cost to go from <paramref name="currentLevel"/> to the next one.</summary>
        public int GetUpgradeCost(int currentLevel)
        {
            int safeLevel = Mathf.Max(1, currentLevel);
            return Mathf.Max(1, Mathf.RoundToInt(upgradeBaseCost * Mathf.Pow(upgradeCostGrowth, safeLevel - 1)));
        }

        public int GetSellValue(int investedCoins)
        {
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, investedCoins) * SellRefund));
        }

        public string GetStatsLine()
        {
            switch (buildingType)
            {
                case BuildingType.Turret:
                    return $"HP {maxHealth:0}  •  УРОН {turretDamage:0.#}  •  ТЕМП {turretFireRate:0.#}/с  •  РАДИУС {turretRange:0.#}";
                case BuildingType.MoneyGenerator:
                    return $"HP {maxHealth:0}  •  {generatorCoinAmount} МОНЕТ / {generatorInterval:0.#}с";
                case BuildingType.RepairStation:
                    return $"HP {maxHealth:0}  •  РЕМОНТ {repairPerSecond:0.#} HP/с  •  РАДИУС {repairRadius:0.#}";
                case BuildingType.Wall:
                    return $"HP {maxHealth:0}  •  БЛОКИРУЕТ ЗОМБИ  •  ДО {MaxLevel} УР.";
                default:
                    return $"HP {maxHealth:0}";
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string newDisplayName,
            string newDescription,
            BuildingType newBuildingType,
            Sprite newIcon,
            GameObject newPrefab,
            int newPrice,
            float newMaxHealth,
            float newPlacementRadius,
            float newPlacementDistanceFromPlayer,
            float newTurretDamage,
            float newTurretFireRate,
            float newTurretRange,
            float newTurretProjectileSpeed,
            float newTurretRotationSpeed,
            Color newTurretProjectileColor,
            int newGeneratorCoinAmount,
            float newGeneratorInterval,
            int newGeneratorBurstSize,
            float newRepairPerSecond,
            float newRepairRadius,
            int newMaxLevel,
            int newUpgradeBaseCost,
            float newUpgradeCostGrowth,
            float newDamagePerLevel,
            float newFireRatePerLevel,
            float newRangePerLevel,
            float newHealthPerLevel,
            float newGeneratorPerLevel,
            float newRepairPerLevel)
        {
            displayName = newDisplayName;
            description = newDescription;
            buildingType = newBuildingType;
            icon = newIcon;
            prefab = newPrefab;
            price = newPrice;
            maxHealth = newMaxHealth;
            placementRadius = newPlacementRadius;
            placementDistanceFromPlayer = newPlacementDistanceFromPlayer;
            turretDamage = newTurretDamage;
            turretFireRate = newTurretFireRate;
            turretRange = newTurretRange;
            turretProjectileSpeed = newTurretProjectileSpeed;
            turretRotationSpeed = newTurretRotationSpeed;
            turretProjectileColor = newTurretProjectileColor;
            generatorCoinAmount = newGeneratorCoinAmount;
            generatorInterval = newGeneratorInterval;
            generatorBurstSize = newGeneratorBurstSize;
            repairPerSecond = newRepairPerSecond;
            repairRadius = newRepairRadius;
            maxLevel = newMaxLevel;
            upgradeBaseCost = newUpgradeBaseCost;
            upgradeCostGrowth = newUpgradeCostGrowth;
            damagePerLevel = newDamagePerLevel;
            fireRatePerLevel = newFireRatePerLevel;
            rangePerLevel = newRangePerLevel;
            healthPerLevel = newHealthPerLevel;
            generatorPerLevel = newGeneratorPerLevel;
            repairPerLevel = newRepairPerLevel;
        }
#endif
    }
}
