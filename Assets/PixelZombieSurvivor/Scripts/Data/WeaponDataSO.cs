using UnityEngine;

namespace PixelZombieSurvivor.Data
{
    /// <summary>
    /// Designer-facing weapon definition. Create new weapons with
    /// Assets > Create > Pixel Zombie Survivor > Weapon Data.
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon_New", menuName = "Pixel Zombie Survivor/Weapon Data")]
    public sealed class WeaponDataSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "New Weapon";
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private Sprite weaponSprite;

        [Header("Economy")]
        [SerializeField, Min(0)] private int price = 500;
        [SerializeField] private bool ownedByDefault;

        [Header("Damage")]
        [SerializeField, Min(0.1f)] private float damage = 10f;
        [SerializeField, Min(0.1f)] private float fireRate = 4f;
        [SerializeField, Min(1)] private int clipSize = 12;
        [SerializeField, Min(0.1f)] private float reloadTime = 1.5f;
        [SerializeField, Range(0f, 45f)] private float spreadDegrees = 3f;
        [SerializeField, Min(0.5f)] private float range = 14f;

        [Header("Projectile")]
        [SerializeField, Min(0.1f)] private float projectileSpeed = 22f;
        [SerializeField, Min(1)] private int pellets = 1;
        [SerializeField] private bool automatic;
        [SerializeField] private Color projectileColor = new Color(1f, 0.82f, 0.2f, 1f);

        [Header("Ammo")]
        [SerializeField, Min(0)] private int startingReserveAmmo = 72;
        [SerializeField, Min(1)] private int maxReserveAmmo = 300;
        [SerializeField, Min(1)] private int ammoBoxAmount = 60;

        [Header("Movement")]
        [SerializeField, Range(0.5f, 1.2f)] private float moveSpeedMultiplier = 1f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public Sprite WeaponSprite => weaponSprite;
        public int Price => price;
        public bool OwnedByDefault => ownedByDefault;
        public float Damage => damage;
        public float FireRate => fireRate;
        public float ShotInterval => 1f / Mathf.Max(0.01f, fireRate);
        public int ClipSize => Mathf.Max(1, clipSize);
        public float ReloadTime => Mathf.Max(0.01f, reloadTime);
        public float SpreadDegrees => spreadDegrees;
        public float Range => range;
        public float ProjectileSpeed => projectileSpeed;
        public int Pellets => Mathf.Max(1, pellets);
        public bool Automatic => automatic;
        public Color ProjectileColor => projectileColor;
        public int StartingReserveAmmo => Mathf.Max(0, startingReserveAmmo);
        public int MaxReserveAmmo => Mathf.Max(1, maxReserveAmmo);
        public int AmmoBoxAmount => Mathf.Max(1, ammoBoxAmount);
        public float MoveSpeedMultiplier => moveSpeedMultiplier;

        /// <summary>Compact line used by the shop and pixel UI.</summary>
        public string GetStatsLine()
        {
            return $"УРОН {damage:0.#}  •  СКОРОСТР. {fireRate:0.#}/с  •  МАГ. {clipSize}\n" +
                   $"ПЕРЕЗАРЯДКА {reloadTime:0.##}с  •  РАЗБРОС {spreadDegrees:0.#}°  •  ДАЛЬНОСТЬ {range:0.#}";
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string newDisplayName,
            string newDescription,
            Sprite newIcon,
            Sprite newWeaponSprite,
            int newPrice,
            bool isOwnedByDefault,
            float newDamage,
            float newFireRate,
            int newClipSize,
            float newReloadTime,
            float newSpreadDegrees,
            float newRange,
            float newProjectileSpeed,
            int newPellets,
            bool isAutomatic,
            Color newProjectileColor,
            int newStartingReserveAmmo,
            int newMaxReserveAmmo,
            int newAmmoBoxAmount,
            float newMoveSpeedMultiplier)
        {
            displayName = newDisplayName;
            description = newDescription;
            icon = newIcon;
            weaponSprite = newWeaponSprite;
            price = newPrice;
            ownedByDefault = isOwnedByDefault;
            damage = newDamage;
            fireRate = newFireRate;
            clipSize = newClipSize;
            reloadTime = newReloadTime;
            spreadDegrees = newSpreadDegrees;
            range = newRange;
            projectileSpeed = newProjectileSpeed;
            pellets = newPellets;
            automatic = isAutomatic;
            projectileColor = newProjectileColor;
            startingReserveAmmo = newStartingReserveAmmo;
            maxReserveAmmo = newMaxReserveAmmo;
            ammoBoxAmount = newAmmoBoxAmount;
            moveSpeedMultiplier = newMoveSpeedMultiplier;
        }
#endif
    }
}
