using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelZombieSurvivor.Data
{
    [Serializable]
    public sealed class ZombieSpawnEntry
    {
        [SerializeField] private ZombieDataSO zombie;
        [SerializeField, Min(1)] private int minimumWave = 1;
        [SerializeField, Min(0.01f)] private float weight = 1f;

        public ZombieDataSO Zombie => zombie;
        public int MinimumWave => minimumWave;
        public float Weight => weight;

#if UNITY_EDITOR
        public ZombieSpawnEntry(ZombieDataSO zombieData, int wave, float spawnWeight)
        {
            zombie = zombieData;
            minimumWave = wave;
            weight = spawnWeight;
        }
#endif
    }

    /// <summary>
    /// All numbers that shape the difficulty curve. Keeping them in a
    /// ScriptableObject makes balancing possible without touching code.
    /// </summary>
    [CreateAssetMenu(fileName = "WaveSettings", menuName = "Pixel Zombie Survivor/Wave Settings")]
    public sealed class WaveSettingsSO : ScriptableObject
    {
        [Header("Timing")]
        [SerializeField, Min(1f)] private float initialIntermission = 8f;
        [SerializeField, Min(1f)] private float intermissionDuration = 10f;
        [SerializeField, Min(0.05f)] private float spawnInterval = 0.55f;

        [Header("Wave Size")]
        [SerializeField, Min(1)] private int baseZombiesPerWave = 5;
        [SerializeField, Min(0)] private int zombiesAddedPerWave = 2;
        [SerializeField, Min(1)] private int maxZombiesPerWave = 85;
        [SerializeField, Min(1)] private int maxAliveAtOnce = 14;

        [Header("Stat Growth Per Wave")]
        [SerializeField, Min(0f)] private float healthGrowthPerWave = 0.17f;
        [SerializeField, Min(0f)] private float damageGrowthPerWave = 0.1f;
        [SerializeField, Min(0f)] private float speedGrowthPerWave = 0.03f;
        [SerializeField, Min(1f)] private float maxSpeedMultiplier = 1.35f;
        [SerializeField, Min(0f)] private float coinGrowthPerWave = 0.09f;

        [Header("Enemy Roster")]
        [SerializeField] private List<ZombieSpawnEntry> roster = new List<ZombieSpawnEntry>();

        public float InitialIntermission => initialIntermission;
        public float IntermissionDuration => intermissionDuration;
        public float SpawnInterval => spawnInterval;
        public int BaseZombiesPerWave => baseZombiesPerWave;
        public int ZombiesAddedPerWave => zombiesAddedPerWave;
        public int MaxZombiesPerWave => maxZombiesPerWave;
        public int MaxAliveAtOnce => maxAliveAtOnce;
        public float HealthGrowthPerWave => healthGrowthPerWave;
        public float DamageGrowthPerWave => damageGrowthPerWave;
        public float SpeedGrowthPerWave => speedGrowthPerWave;
        public float MaxSpeedMultiplier => maxSpeedMultiplier;
        public float CoinGrowthPerWave => coinGrowthPerWave;
        public IReadOnlyList<ZombieSpawnEntry> Roster => roster;

        public int GetZombieCount(int waveNumber)
        {
            int waveIndex = Mathf.Max(0, waveNumber - 1);
            return Mathf.Min(MaxZombiesPerWave, baseZombiesPerWave + zombiesAddedPerWave * waveIndex);
        }

        public int GetMaxAlive(int waveNumber)
        {
            // Hard ceiling on simultaneous enemies so late waves stay readable and
            // dodgeable instead of turning into an unavoidable wall of bodies.
            return Mathf.Min(24, maxAliveAtOnce + waveNumber / 3);
        }

        public float GetHealthMultiplier(int waveNumber) => 1f + healthGrowthPerWave * Mathf.Max(0, waveNumber - 1);
        public float GetDamageMultiplier(int waveNumber) => 1f + damageGrowthPerWave * Mathf.Max(0, waveNumber - 1);
        public float GetSpeedMultiplier(int waveNumber) =>
            Mathf.Min(maxSpeedMultiplier, 1f + speedGrowthPerWave * Mathf.Max(0, waveNumber - 1));
        public float GetCoinMultiplier(int waveNumber) => 1f + coinGrowthPerWave * Mathf.Max(0, waveNumber - 1);

        public ZombieDataSO PickZombie(int waveNumber)
        {
            float totalWeight = 0f;
            for (int i = 0; i < roster.Count; i++)
            {
                ZombieSpawnEntry entry = roster[i];
                if (entry != null && entry.Zombie != null && entry.MinimumWave <= waveNumber)
                {
                    totalWeight += Mathf.Max(0f, entry.Weight);
                }
            }

            if (totalWeight <= 0f)
            {
                for (int i = 0; i < roster.Count; i++)
                {
                    if (roster[i] != null && roster[i].Zombie != null)
                    {
                        return roster[i].Zombie;
                    }
                }

                return null;
            }

            float roll = UnityEngine.Random.value * totalWeight;
            for (int i = 0; i < roster.Count; i++)
            {
                ZombieSpawnEntry entry = roster[i];
                if (entry == null || entry.Zombie == null || entry.MinimumWave > waveNumber)
                {
                    continue;
                }

                roll -= Mathf.Max(0f, entry.Weight);
                if (roll <= 0f)
                {
                    return entry.Zombie;
                }
            }

            return roster[roster.Count - 1] != null ? roster[roster.Count - 1].Zombie : null;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            float newInitialIntermission,
            float newIntermissionDuration,
            float newSpawnInterval,
            int newBaseZombiesPerWave,
            int newZombiesAddedPerWave,
            int newMaxZombiesPerWave,
            int newMaxAliveAtOnce,
            float newHealthGrowth,
            float newDamageGrowth,
            float newSpeedGrowth,
            float newMaxSpeedMultiplier,
            float newCoinGrowth,
            List<ZombieSpawnEntry> newRoster)
        {
            initialIntermission = newInitialIntermission;
            intermissionDuration = newIntermissionDuration;
            spawnInterval = newSpawnInterval;
            baseZombiesPerWave = newBaseZombiesPerWave;
            zombiesAddedPerWave = newZombiesAddedPerWave;
            maxZombiesPerWave = newMaxZombiesPerWave;
            maxAliveAtOnce = newMaxAliveAtOnce;
            healthGrowthPerWave = newHealthGrowth;
            damageGrowthPerWave = newDamageGrowth;
            speedGrowthPerWave = newSpeedGrowth;
            maxSpeedMultiplier = newMaxSpeedMultiplier;
            coinGrowthPerWave = newCoinGrowth;
            roster = newRoster;
        }
#endif
    }
}
