using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Data;
using PixelZombieSurvivor.Economy;
using UnityEngine;

namespace PixelZombieSurvivor.Building
{
    /// <summary>Periodically drops collectible coins around itself.</summary>
    [RequireComponent(typeof(BuildingBase))]
    public sealed class MoneyGenerator : MonoBehaviour
    {
        [SerializeField] private float spawnRadius = 0.55f;

        private BuildingBase building;
        private GameDatabaseSO database;
        private float nextGenerationTime;

        private void Awake()
        {
            building = GetComponent<BuildingBase>();
            database = GameDatabaseSO.Load();
        }

        private void Update()
        {
            BuildingDataSO data = building != null ? building.Data : null;
            if (data == null || data.BuildingType != BuildingType.MoneyGenerator || !building.IsAlive)
            {
                return;
            }

            if (nextGenerationTime <= 0f)
            {
                nextGenerationTime = Time.time + data.GeneratorInterval;
                return;
            }

            if (Time.time < nextGenerationTime)
            {
                return;
            }

            nextGenerationTime = Time.time + data.GeneratorInterval;
            GenerateCoins(data);
        }

        private void GenerateCoins(BuildingDataSO data)
        {
            if (database == null || database.CoinPrefab == null)
            {
                return;
            }

            int totalValue = Mathf.Max(1, data.GetGeneratorCoinAmount(building.Level));
            CoinBurst.Drop(database.CoinPrefab, transform.position, totalValue, 8, spawnRadius);
        }
    }
}
