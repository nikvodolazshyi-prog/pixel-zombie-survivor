using System;
using PixelZombieSurvivor.Building;
using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Data;
using PixelZombieSurvivor.Player;
using UnityEngine;

namespace PixelZombieSurvivor.Economy
{
    /// <summary>
    /// Owns shop rules and purchase validation. It never builds UI and never touches
    /// HP/ammo directly: those operations are delegated to the relevant systems.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public sealed class ShopManager : SingletonBehaviour<ShopManager>
    {
        [SerializeField] private GameDatabaseSO database;

        private CurrencyManager currency;
        private PlayerHealth player;
        private PlayerWeaponController weapons;
        private BuildingPlacementController placement;

        public event Action<bool> ShopToggled;
        public event Action<string, bool> NotificationRequested;

        public bool IsOpen { get; private set; }
        public GameDatabaseSO Database => database;

        private void Start()
        {
            if (database == null)
            {
                database = GameDatabaseSO.Load();
            }

            currency = CurrencyManager.Instance;
            player = FindAnyObjectByType<PlayerHealth>();
            weapons = FindAnyObjectByType<PlayerWeaponController>();
            placement = FindAnyObjectByType<BuildingPlacementController>();

            if (placement != null)
            {
                placement.PlacementMessage += HandlePlacementMessage;
                placement.BuildingPlaced += HandleBuildingPlaced;
                placement.BuildingDemolished += HandleBuildingDemolished;
            }
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
            {
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            {
                return;
            }

            if (InputBindings.IsDown(GameAction.Shop) || Input.GetKeyDown(KeyCode.Tab))
            {
                if (IsOpen)
                {
                    CloseShop();
                }
                else
                {
                    OpenShop();
                }

                return;
            }

            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseShop();
            }
        }

        public void OpenShop()
        {
            if (IsOpen || (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver))
            {
                return;
            }

            placement?.CancelPlacement();
            IsOpen = true;
            ShopToggled?.Invoke(true);
        }

        public void CloseShop()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            ShopToggled?.Invoke(false);
        }

        public void ForceClose()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            ShopToggled?.Invoke(false);
        }

        public bool BuyWeapon(WeaponDataSO weapon)
        {
            if (weapon == null || weapons == null)
            {
                return Fail("Оружие недоступно.");
            }

            if (weapons.HasWeapon(weapon))
            {
                return Fail("Это оружие уже куплено.");
            }

            if (weapon.Price > 0 && (currency == null || !currency.TrySpend(weapon.Price)))
            {
                return Fail("Недостаточно монет.");
            }

            weapons.UnlockWeapon(weapon);
            Notify($"Куплено: {weapon.DisplayName}.", true);
            return true;
        }

        public bool BuyBuilding(BuildingDataSO building)
        {
            if (building == null || placement == null)
            {
                return Fail("Постройка недоступна.");
            }

            if (currency == null || currency.Balance < building.Price)
            {
                return Fail($"Не хватает монет: нужно {building.Price}.");
            }

            if (!placement.BeginPlacement(building))
            {
                return false;
            }

            CloseShop();
            Notify($"Разместите «{building.DisplayName}» на карте.", true);
            return true;
        }

        public bool BuyConsumable(ConsumableDataSO consumable)
        {
            if (consumable == null || currency == null || player == null || weapons == null)
            {
                return Fail("Расходник недоступен.");
            }

            if (consumable.ConsumableType == ConsumableType.HealthPack)
            {
                if (player.CurrentHealth >= player.MaxHealth)
                {
                    return Fail("Здоровье уже полное.");
                }

                if (!currency.TrySpend(consumable.Price))
                {
                    return Fail("Недостаточно монет.");
                }

                float restored = player.RestoreHealth(consumable.HealthRestore);
                Notify($"Восстановлено {restored:0} HP.", true);
                return true;
            }

            if (!weapons.HasAmmoCapacity())
            {
                return Fail("Все запасы патронов уже полны.");
            }

            if (!currency.TrySpend(consumable.Price))
            {
                return Fail("Недостаточно монет.");
            }

            weapons.AddAmmoToAll(consumable.AmmoRestore);
            Notify($"Боезапас пополнен на {consumable.AmmoRestore}.", true);
            return true;
        }

        public bool IsWeaponOwned(WeaponDataSO weapon)
        {
            return weapons != null && weapons.HasWeapon(weapon);
        }

        private bool Fail(string message)
        {
            Notify(message, false);
            return false;
        }

        private void Notify(string message, bool success)
        {
            NotificationRequested?.Invoke(message, success);
        }

        private void HandlePlacementMessage(string message)
        {
            Notify(message, false);
        }

        private void HandleBuildingPlaced(BuildingDataSO data, BuildingBase building)
        {
            Notify($"Построено: {data.DisplayName}.", true);
        }

        private void HandleBuildingDemolished(BuildingBase building, int refund)
        {
            string buildingName = building != null && building.Data != null ? building.Data.DisplayName : "Постройка";
            Notify($"{buildingName} снесена. Возврат: {refund} монет.", true);
        }

        protected override void OnDestroy()
        {
            if (placement != null)
            {
                placement.PlacementMessage -= HandlePlacementMessage;
                placement.BuildingPlaced -= HandleBuildingPlaced;
                placement.BuildingDemolished -= HandleBuildingDemolished;
            }

            base.OnDestroy();
        }
    }
}
