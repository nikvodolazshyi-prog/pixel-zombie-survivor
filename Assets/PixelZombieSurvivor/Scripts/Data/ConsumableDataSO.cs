using UnityEngine;

namespace PixelZombieSurvivor.Data
{
    public enum ConsumableType
    {
        HealthPack = 0,
        AmmoBox = 1
    }

    [CreateAssetMenu(fileName = "Consumable_New", menuName = "Pixel Zombie Survivor/Consumable Data")]
    public sealed class ConsumableDataSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "New Consumable";
        [SerializeField, TextArea(2, 3)] private string description;
        [SerializeField] private ConsumableType consumableType;
        [SerializeField] private Sprite icon;

        [Header("Economy & Effect")]
        [SerializeField, Min(0)] private int price = 100;
        [SerializeField, Min(1f)] private float healthRestore = 40f;
        [SerializeField, Min(1)] private int ammoRestore = 90;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public ConsumableType ConsumableType => consumableType;
        public Sprite Icon => icon;
        public int Price => price;
        public float HealthRestore => healthRestore;
        public int AmmoRestore => ammoRestore;

        public string GetStatsLine()
        {
            return consumableType == ConsumableType.HealthPack
                ? $"МГНОВЕННО ВОССТАНАВЛИВАЕТ {healthRestore:0} HP"
                : $"ДОБАВЛЯЕТ {ammoRestore} ПАТРОНОВ ВСЕМУ АРСЕНАЛУ";
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string newDisplayName,
            string newDescription,
            ConsumableType newType,
            Sprite newIcon,
            int newPrice,
            float newHealthRestore,
            int newAmmoRestore)
        {
            displayName = newDisplayName;
            description = newDescription;
            consumableType = newType;
            icon = newIcon;
            price = newPrice;
            healthRestore = newHealthRestore;
            ammoRestore = newAmmoRestore;
        }
#endif
    }
}
