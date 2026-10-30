using System.Collections.Generic;
using UnityEngine;

namespace PixelZombieSurvivor.Data
{
    /// <summary>
    /// Central asset that connects all designer-authored data and runtime prefabs.
    /// It lives in Resources so a freshly generated scene works without manual wiring.
    /// </summary>
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "Pixel Zombie Survivor/Game Database")]
    public sealed class GameDatabaseSO : ScriptableObject
    {
        public const string ResourcePath = "PixelZombieSurvivor/GameDatabase";

        [Header("Shop Catalog")]
        [SerializeField] private List<WeaponDataSO> weapons = new List<WeaponDataSO>();
        [SerializeField] private List<BuildingDataSO> buildings = new List<BuildingDataSO>();
        [SerializeField] private List<ConsumableDataSO> consumables = new List<ConsumableDataSO>();

        [Header("Waves")]
        [SerializeField] private WaveSettingsSO waveSettings;
        [SerializeField] private List<ZombieDataSO> zombies = new List<ZombieDataSO>();

        [Header("Starting Loadout")]
        [SerializeField] private int startingCoins = 300;
        [SerializeField] private WeaponDataSO startingWeapon;

        [Header("Runtime Prefabs")]
        [SerializeField] private GameObject zombiePrefab;
        [SerializeField] private GameObject bossPrefab;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private GameObject coinPrefab;

        private static GameDatabaseSO cached;

        public IReadOnlyList<WeaponDataSO> Weapons => weapons;
        public IReadOnlyList<BuildingDataSO> Buildings => buildings;
        public IReadOnlyList<ConsumableDataSO> Consumables => consumables;
        public WaveSettingsSO WaveSettings => waveSettings;
        public IReadOnlyList<ZombieDataSO> Zombies => zombies;
        public int StartingCoins => startingCoins;
        public GameObject ZombiePrefab => zombiePrefab;
        public GameObject BossPrefab => bossPrefab;
        public GameObject BulletPrefab => bulletPrefab;
        public GameObject CoinPrefab => coinPrefab;
        public WeaponDataSO StartingWeapon
        {
            get
            {
                if (startingWeapon != null)
                {
                    return startingWeapon;
                }

                for (int i = 0; i < weapons.Count; i++)
                {
                    if (weapons[i] != null && weapons[i].OwnedByDefault)
                    {
                        return weapons[i];
                    }
                }

                return weapons.Count > 0 ? weapons[0] : null;
            }
        }

        public static GameDatabaseSO Load()
        {
            if (cached == null)
            {
                cached = Resources.Load<GameDatabaseSO>(ResourcePath);
            }

            return cached;
        }

#if UNITY_EDITOR
        public void EditorSetContents(
            List<WeaponDataSO> weaponList,
            List<BuildingDataSO> buildingList,
            List<ConsumableDataSO> consumableList,
            WaveSettingsSO settings,
            List<ZombieDataSO> zombieList,
            int coins,
            WeaponDataSO startWeapon,
            GameObject zombie,
            GameObject boss,
            GameObject bullet,
            GameObject coin)
        {
            weapons = weaponList;
            buildings = buildingList;
            consumables = consumableList;
            waveSettings = settings;
            zombies = zombieList;
            startingCoins = coins;
            startingWeapon = startWeapon;
            zombiePrefab = zombie;
            bossPrefab = boss;
            bulletPrefab = bullet;
            coinPrefab = coin;
        }
#endif
    }
}
