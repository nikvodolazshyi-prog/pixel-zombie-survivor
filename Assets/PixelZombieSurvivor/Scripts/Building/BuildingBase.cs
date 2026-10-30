using System;
using System.Collections.Generic;
using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Data;
using UnityEngine;

namespace PixelZombieSurvivor.Building
{
    /// <summary>
    /// Base for every deployable. Registers itself for zombie targeting and repair,
    /// owns HP, grid occupancy and the shared level/upgrade economy.
    /// </summary>
    public class BuildingBase : DamageableBehaviour
    {
        private static readonly List<BuildingBase> ActiveBuildings = new List<BuildingBase>();

        [SerializeField] private WorldHealthBar healthBar;
        [SerializeField] private BuildingDataSO data;

        public BuildingDataSO Data => data;
        public static IReadOnlyList<BuildingBase> Active => ActiveBuildings;

        public int Level { get; private set; } = 1;
        public int InvestedCoins { get; private set; }
        public Vector2Int Cell { get; private set; }
        public bool HasCell { get; private set; }

        public event Action<BuildingBase> Upgraded;
        public event Action<BuildingBase, int> Removed;

        public bool IsTurret => data != null && data.BuildingType == BuildingType.Turret;
        public bool IsWall => data != null && data.BuildingType == BuildingType.Wall;
        public bool IsMaxLevel => data == null || Level >= data.MaxLevel;
        public int NextUpgradeCost => data != null ? data.GetUpgradeCost(Level) : 0;
        public int SellValue => data != null ? data.GetSellValue(InvestedCoins) : 0;

        protected override void Awake()
        {
            base.Awake();
            if (healthBar == null)
            {
                healthBar = GetComponentInChildren<WorldHealthBar>(true);
            }
        }

        private void OnEnable()
        {
            if (!ActiveBuildings.Contains(this))
            {
                ActiveBuildings.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveBuildings.Remove(this);
        }

        public void Initialize(BuildingDataSO buildingData)
        {
            data = buildingData;
            Level = 1;
            InvestedCoins = data != null ? data.Price : 0;
            HasCell = false;
            SetMaxHealth(data != null ? data.GetMaxHealth(Level) : 100f, true);
            healthBar?.Bind(this);
        }

        /// <summary>Called by <see cref="GridManager"/> when the building occupies a cell.</summary>
        public void AssignCell(Vector2Int cell)
        {
            Cell = cell;
            HasCell = true;
        }

        public bool TryUpgrade()
        {
            if (data == null || !IsAlive || IsMaxLevel)
            {
                return false;
            }

            int cost = NextUpgradeCost;
            CurrencyManager currency = CurrencyManager.Instance;
            if (currency == null || !currency.TrySpend(cost))
            {
                return false;
            }

            float previousMax = MaxHealth;
            Level++;
            InvestedCoins += cost;

            float nextMax = data.GetMaxHealth(Level);
            SetMaxHealth(nextMax, false);
            Heal(Mathf.Max(0f, nextMax - previousMax));

            Upgraded?.Invoke(this);
            return true;
        }

        /// <summary>Removes the building and optionally refunds part of the invested coins.</summary>
        public void Demolish(bool refund)
        {
            if (!IsAlive)
            {
                return;
            }

            int refunded = refund ? SellValue : 0;
            if (refunded > 0)
            {
                CurrencyManager.Instance?.Add(refunded);
            }

            GridManager.Instance?.ReleaseBuilding(this);
            HasCell = false;
            Removed?.Invoke(this, refunded);
            Destroy(gameObject);
        }

        public static BuildingBase FindNearest(Vector2 position, float maxDistance = 100f)
        {
            BuildingBase nearest = null;
            float bestSqrDistance = maxDistance * maxDistance;

            for (int i = 0; i < ActiveBuildings.Count; i++)
            {
                BuildingBase building = ActiveBuildings[i];
                if (building == null || !building.IsAlive)
                {
                    continue;
                }

                float sqrDistance = ((Vector2)building.transform.position - position).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    nearest = building;
                }
            }

            return nearest;
        }

        protected override void OnDeath()
        {
            ActiveBuildings.Remove(this);
            GridManager.Instance?.ReleaseBuilding(this);
            HasCell = false;
            Removed?.Invoke(this, 0);
            Destroy(gameObject, 0.05f);
        }
    }
}
