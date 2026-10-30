using System;
using System.Collections;
using System.Collections.Generic;
using PixelZombieSurvivor.Building;
using PixelZombieSurvivor.Combat;
using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Data;
using PixelZombieSurvivor.Economy;
using UnityEngine;

namespace PixelZombieSurvivor.Player
{
    /// <summary>
    /// Aiming, firing, reloading and the owned-weapon inventory.
    /// Projectiles are pooled and initialized from immutable WeaponDataSO assets.
    /// </summary>
    public sealed class PlayerWeaponController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Transform weaponPivot;
        [SerializeField] private Transform muzzle;
        [SerializeField] private SpriteRenderer weaponVisual;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Camera aimCamera;

        [Header("Feel")]
        [SerializeField, Min(0f)] private float fireShakeStrength = 0.035f;

        private readonly List<WeaponRuntime> ownedWeapons = new List<WeaponRuntime>();

        private GameDatabaseSO database;
        private CameraFollow2D cameraFollow;
        private int currentIndex = -1;
        private float nextShotTime;
        private Coroutine reloadRoutine;
        private bool shopOpen;
        private bool placingBuilding;
        private bool buildModeActive;
        private bool inspecting;
        private bool gameOver;

        public event Action<WeaponRuntime> WeaponChanged;
        public event Action AmmoChanged;
        public event Action InventoryChanged;
        public event Action<bool> ReloadStateChanged;

        public IReadOnlyList<WeaponRuntime> OwnedWeapons => ownedWeapons;
        public int CurrentIndex => currentIndex;
        public WeaponRuntime CurrentWeapon =>
            currentIndex >= 0 && currentIndex < ownedWeapons.Count ? ownedWeapons[currentIndex] : null;
        public float CurrentMoveSpeedMultiplier => CurrentWeapon != null ? CurrentWeapon.Data.MoveSpeedMultiplier : 1f;
        public bool IsReloading => CurrentWeapon != null && CurrentWeapon.IsReloading;

        private bool CanUseWeapon =>
            !shopOpen &&
            !placingBuilding &&
            !buildModeActive &&
            !inspecting &&
            !gameOver &&
            GameManager.Instance != null &&
            GameManager.Instance.State != GameState.GameOver &&
            !GameManager.Instance.IsPaused;

        private void Awake()
        {
            if (weaponPivot == null) weaponPivot = transform.Find("WeaponPivot");
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (weaponVisual == null && weaponPivot != null) weaponVisual = weaponPivot.GetComponentInChildren<SpriteRenderer>();
            if (muzzle == null && weaponPivot != null) muzzle = weaponPivot.Find("Muzzle");
            if (aimCamera == null) aimCamera = Camera.main;

            database = GameDatabaseSO.Load();
            cameraFollow = aimCamera != null ? aimCamera.GetComponent<CameraFollow2D>() : null;
            Cursor.visible = true;
        }

        private void Start()
        {
            if (ShopManager.Instance != null)
            {
                shopOpen = ShopManager.Instance.IsOpen;
                ShopManager.Instance.ShopToggled += HandleShopToggled;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.StateChanged += HandleGameStateChanged;
                gameOver = GameManager.Instance.State == GameState.GameOver;
            }

            if (BuildingPlacementController.Instance != null)
            {
                placingBuilding = BuildingPlacementController.Instance.IsPlacing;
                buildModeActive = BuildingPlacementController.Instance.Mode != BuildInteractionMode.None;
                inspecting = BuildingPlacementController.Instance.IsInspecting;
                BuildingPlacementController.Instance.PlacementStateChanged += HandlePlacementStateChanged;
                BuildingPlacementController.Instance.ModeChanged += HandleBuildModeChanged;
                BuildingPlacementController.Instance.InspectionChanged += HandleInspectionChanged;
            }

            if (database != null)
            {
                UnlockWeapon(database.StartingWeapon, true);
            }
            else
            {
                Debug.LogError("GameDatabase was not loaded. Place GameDatabase.asset in Resources/PixelZombieSurvivor/.", this);
            }
        }

        private void Update()
        {
            Aim();

            if (!CanUseWeapon)
            {
                return;
            }

            HandleWeaponSwitching();

            WeaponRuntime weapon = CurrentWeapon;
            if (weapon == null)
            {
                return;
            }

            if (input.ReloadPressed)
            {
                BeginReload();
            }

            bool wantsToFire = weapon.Data.Automatic ? input.ShootHeld : input.ShootPressed;
            if (!wantsToFire || weapon.IsReloading || Time.time < nextShotTime)
            {
                return;
            }

            if (!weapon.CanFire)
            {
                BeginReload();
                return;
            }

            Fire(weapon);
        }

        /// <summary>Adds a weapon to the inventory. Returns false when it is already owned.</summary>
        public bool UnlockWeapon(WeaponDataSO data, bool equip = true)
        {
            if (data == null || HasWeapon(data))
            {
                return false;
            }

            WeaponRuntime runtime = new WeaponRuntime(data);
            ownedWeapons.Add(runtime);
            InventoryChanged?.Invoke();

            if (equip || currentIndex < 0)
            {
                SelectWeapon(ownedWeapons.Count - 1);
            }

            return true;
        }

        public bool HasWeapon(WeaponDataSO data)
        {
            if (data == null)
            {
                return false;
            }

            for (int i = 0; i < ownedWeapons.Count; i++)
            {
                if (ownedWeapons[i].Data == data)
                {
                    return true;
                }
            }

            return false;
        }

        public void SelectWeapon(int index)
        {
            if (index < 0 || index >= ownedWeapons.Count || index == currentIndex)
            {
                return;
            }

            CancelReload();
            currentIndex = index;
            nextShotTime = 0f;
            UpdateWeaponVisual();
            WeaponChanged?.Invoke(CurrentWeapon);
            AmmoChanged?.Invoke();
        }

        public bool AddAmmoToAll(int amount)
        {
            bool changed = false;
            for (int i = 0; i < ownedWeapons.Count; i++)
            {
                if (ownedWeapons[i].AddReserve(amount) > 0)
                {
                    changed = true;
                }
            }

            if (changed)
            {
                AmmoChanged?.Invoke();
            }

            return changed;
        }

        public bool HasAmmoCapacity()
        {
            for (int i = 0; i < ownedWeapons.Count; i++)
            {
                if (ownedWeapons[i].HasReserveCapacity)
                {
                    return true;
                }
            }

            return false;
        }

        private void Aim()
        {
            if (weaponPivot == null || aimCamera == null)
            {
                return;
            }

            Vector2 pointer = aimCamera.ScreenToWorldPoint(input.PointerScreenPosition);
            Vector2 direction = pointer - (Vector2)weaponPivot.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            weaponPivot.rotation = Quaternion.Euler(0f, 0f, angle);

            Vector3 scale = weaponPivot.localScale;
            scale.y = Mathf.Abs(scale.y) * (Mathf.Abs(angle) > 90f ? -1f : 1f);
            weaponPivot.localScale = scale;
        }

        private void HandleWeaponSwitching()
        {
            if (ownedWeapons.Count <= 1)
            {
                return;
            }

            int slot = input.GetWeaponSlotPressed();
            if (slot >= 0)
            {
                if (slot < ownedWeapons.Count)
                {
                    SelectWeapon(slot);
                }

                return;
            }

            float scroll = input.Scroll;
            if (Mathf.Abs(scroll) < 0.01f)
            {
                return;
            }

            int next = currentIndex + (scroll > 0f ? -1 : 1);
            if (next < 0) next = ownedWeapons.Count - 1;
            if (next >= ownedWeapons.Count) next = 0;
            SelectWeapon(next);
        }

        private void Fire(WeaponRuntime weapon)
        {
            if (muzzle == null || weapon == null || !weapon.ConsumeRound())
            {
                return;
            }

            nextShotTime = Time.time + weapon.Data.ShotInterval;
            AmmoChanged?.Invoke();

            if (database == null || database.BulletPrefab == null)
            {
                Debug.LogWarning("Bullet prefab is missing in GameDatabase.", this);
                return;
            }

            float baseAngle = weaponPivot.eulerAngles.z;
            float spread = weapon.Data.SpreadDegrees;
            for (int i = 0; i < weapon.Data.Pellets; i++)
            {
                float angle = baseAngle + UnityEngine.Random.Range(-spread * 0.5f, spread * 0.5f);
                Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

                GameObject bulletObject = PoolService.Instance != null
                    ? PoolService.Instance.Spawn(database.BulletPrefab, muzzle.position, Quaternion.Euler(0f, 0f, angle))
                    : Instantiate(database.BulletPrefab, muzzle.position, Quaternion.Euler(0f, 0f, angle));

                if (bulletObject == null)
                {
                    continue;
                }

                Projectile projectile = bulletObject.GetComponent<Projectile>();
                projectile?.Launch(
                    direction,
                    weapon.Data.Damage,
                    weapon.Data.ProjectileSpeed,
                    weapon.Data.Range,
                    Faction.Player,
                    gameObject,
                    weapon.Data.ProjectileColor);
            }

            cameraFollow?.Shake(fireShakeStrength, 0.055f);
            AudioService.Instance?.PlayShot(Mathf.Lerp(1.25f, 0.78f, Mathf.InverseLerp(5f, 30f, weapon.Data.Damage)));

            if (!weapon.CanFire)
            {
                BeginReload();
            }
        }

        private void BeginReload()
        {
            WeaponRuntime weapon = CurrentWeapon;
            if (weapon == null || weapon.IsReloading || weapon.Clip >= weapon.Data.ClipSize || weapon.Reserve <= 0)
            {
                return;
            }

            if (reloadRoutine != null)
            {
                StopCoroutine(reloadRoutine);
            }

            reloadRoutine = StartCoroutine(ReloadRoutine(weapon));
        }

        private IEnumerator ReloadRoutine(WeaponRuntime weapon)
        {
            weapon.IsReloading = true;
            ReloadStateChanged?.Invoke(true);
            yield return new WaitForSeconds(weapon.Data.ReloadTime);

            if (CurrentWeapon == weapon)
            {
                weapon.CompleteReload();
                weapon.IsReloading = false;
                reloadRoutine = null;
                ReloadStateChanged?.Invoke(false);
                AmmoChanged?.Invoke();
            }
        }

        private void CancelReload()
        {
            if (reloadRoutine != null)
            {
                StopCoroutine(reloadRoutine);
                reloadRoutine = null;
                if (CurrentWeapon != null)
                {
                    CurrentWeapon.IsReloading = false;
                }

                ReloadStateChanged?.Invoke(false);
            }
        }

        private void UpdateWeaponVisual()
        {
            WeaponRuntime weapon = CurrentWeapon;
            if (weaponVisual == null)
            {
                return;
            }

            weaponVisual.sprite = weapon != null ? weapon.Data.WeaponSprite : null;
            weaponVisual.enabled = weaponVisual.sprite != null;
        }

        private void HandleShopToggled(bool isOpen)
        {
            shopOpen = isOpen;
        }

        private void HandlePlacementStateChanged(bool isPlacing)
        {
            placingBuilding = isPlacing;
        }

        private void HandleBuildModeChanged(BuildInteractionMode mode)
        {
            buildModeActive = mode != BuildInteractionMode.None;
        }

        private void HandleInspectionChanged(bool isInspecting)
        {
            inspecting = isInspecting;
        }

        private void HandleGameStateChanged(GameState state)
        {
            gameOver = state == GameState.GameOver;
        }

        private void OnDestroy()
        {
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.ShopToggled -= HandleShopToggled;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.StateChanged -= HandleGameStateChanged;
            }

            if (BuildingPlacementController.Instance != null)
            {
                BuildingPlacementController.Instance.PlacementStateChanged -= HandlePlacementStateChanged;
                BuildingPlacementController.Instance.ModeChanged -= HandleBuildModeChanged;
                BuildingPlacementController.Instance.InspectionChanged -= HandleInspectionChanged;
            }
        }
    }
}
