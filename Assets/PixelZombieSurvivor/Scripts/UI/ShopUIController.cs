using System;
using System.Collections.Generic;
using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Data;
using PixelZombieSurvivor.Economy;
using PixelZombieSurvivor.Player;
using UnityEngine;
using UnityEngine.UI;

namespace PixelZombieSurvivor.UI
{
    /// <summary>
    /// Data-driven shop overlay. Categories and cards are rebuilt from the
    /// GameDatabase's ScriptableObject lists, sorted by price.
    /// </summary>
    public sealed class ShopUIController : MonoBehaviour
    {
        private sealed class ShopRow
        {
            public Button Button;
            public Text Label;
            public int Price;
            public bool Owned;
        }

        private ShopManager shop;
        private PlayerHealth player;
        private PlayerWeaponController weapons;
        private CurrencyManager currency;

        private RectTransform content;
        private ScrollRect scrollRect;
        private Text walletText;
        private Text statusText;
        private RectTransform panelRect;
        private readonly List<ShopRow> rows = new List<ShopRow>();
        private bool initialized;

        public void Initialize(
            ShopManager shopManager,
            PlayerHealth playerHealth,
            PlayerWeaponController weaponController,
            CurrencyManager currencyManager)
        {
            shop = shopManager;
            player = playerHealth;
            weapons = weaponController;
            currency = currencyManager;

            Build();
            Subscribe();
            initialized = true;
            gameObject.SetActive(false);
        }

        private void Build()
        {
            MakeFullScreen();
            Image dimmer = gameObject.GetComponent<Image>();
            if (dimmer == null)
            {
                dimmer = gameObject.AddComponent<Image>();
            }

            dimmer.color = new Color(0.01f, 0.015f, 0.02f, 0.86f);
            dimmer.raycastTarget = true;

            Image border = UIFactory.CreatePanel("PanelBorder", transform, UiTheme.AccentDark);
            panelRect = UIFactory.Anchor(
                border.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1350f, 860f));

            Image panel = UIFactory.CreatePanel("Panel", border.transform, UiTheme.Panel);
            UIFactory.Stretch(panel.rectTransform, 4f, 4f, 4f, 4f);

            Text title = UIFactory.CreateText("Title", panel.transform, "МАГАЗИН", 38, UiTheme.Accent);
            UIFactory.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -18f), new Vector2(480f, 52f));

            Text subtitle = UIFactory.CreateText("Subtitle", panel.transform, "ОРУЖИЕ  •  ПОСТРОЙКИ  •  РАСХОДНИКИ", 13, UiTheme.Muted);
            UIFactory.Anchor(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -66f), new Vector2(560f, 22f));

            walletText = UIFactory.CreateText("Wallet", panel.transform, "0 МОНЕТ", 22, UiTheme.Gold, TextAnchor.MiddleRight);
            UIFactory.Anchor(walletText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-82f, -24f), new Vector2(360f, 40f));

            Button close = UIFactory.CreateButton("Close", panel.transform, "×", UiTheme.PanelSoft, UiTheme.Text, 26);
            UIFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(46f, 46f));
            close.onClick.AddListener(() => shop.CloseShop());

            RectTransform scrollArea = UIFactory.CreateUIObject("ScrollArea", panel.transform);
            UIFactory.Stretch(scrollArea, 28f, 74f, 28f, 98f);

            RectTransform viewport = UIFactory.CreateUIObject("Viewport", scrollArea);
            UIFactory.Stretch(viewport);
            Image viewportRaycast = viewport.gameObject.AddComponent<Image>();
            viewportRaycast.color = new Color(0f, 0f, 0f, 0.01f);
            viewportRaycast.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            content = UIFactory.CreateUIObject("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 9f;
            layout.padding = new RectOffset(10, 10, 8, 12);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect = scrollArea.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 42f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.12f;

            statusText = UIFactory.CreateText("Status", panel.transform, "Время не останавливается — следите за зомби!", 13, UiTheme.Danger, TextAnchor.MiddleCenter);
            UIFactory.Anchor(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1240f, 32f));

            Text footer = UIFactory.CreateText("Footer", panel.transform, "TAB / ESC — ЗАКРЫТЬ", 12, UiTheme.Muted, TextAnchor.MiddleRight);
            UIFactory.Anchor(footer.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 24f), new Vector2(260f, 24f));
        }

        private void Subscribe()
        {
            if (shop != null)
            {
                shop.ShopToggled += HandleShopToggled;
                shop.NotificationRequested += HandleNotification;
            }

            if (currency != null)
            {
                currency.CoinsChanged += HandleCoinsChanged;
            }
        }

        private void HandleShopToggled(bool isOpen)
        {
            gameObject.SetActive(isOpen);
            if (!isOpen)
            {
                return;
            }

            Rebuild();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }

            SetStatus("Время не останавливается — следите за зомби!", false);
        }

        private void HandleNotification(string message, bool success)
        {
            if (initialized && gameObject.activeSelf)
            {
                SetStatus(message, success);
            }
        }

        private void HandleCoinsChanged(int balance)
        {
            if (!initialized)
            {
                return;
            }

            RefreshWallet();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Owned)
                {
                    continue;
                }

                bool canAfford = balance >= rows[i].Price;
                rows[i].Button.interactable = canAfford;
                rows[i].Label.text = canAfford ? $"КУПИТЬ\n{rows[i].Price}" : $"НЕ ХВАТАЕТ\n{rows[i].Price}";
            }
        }

        private void Rebuild()
        {
            if (shop == null || shop.Database == null)
            {
                return;
            }

            ClearContent();
            rows.Clear();
            RefreshWallet();

            GameDatabaseSO database = shop.Database;

            AddSection("ОРУЖИЕ");
            List<WeaponDataSO> weaponList = new List<WeaponDataSO>(database.Weapons);
            weaponList.Sort((a, b) =>
            {
                if (a == null) return 1;
                if (b == null) return -1;
                return a.Price.CompareTo(b.Price);
            });

            for (int i = 0; i < weaponList.Count; i++)
            {
                AddWeaponCard(weaponList[i]);
            }

            AddSection("ПОСТРОЙКИ");
            List<BuildingDataSO> buildingList = new List<BuildingDataSO>(database.Buildings);
            buildingList.Sort((a, b) =>
            {
                if (a == null) return 1;
                if (b == null) return -1;
                return a.Price.CompareTo(b.Price);
            });

            for (int i = 0; i < buildingList.Count; i++)
            {
                AddBuildingCard(buildingList[i]);
            }

            AddSection("РАСХОДНИКИ");
            List<ConsumableDataSO> consumableList = new List<ConsumableDataSO>(database.Consumables);
            consumableList.Sort((a, b) =>
            {
                if (a == null) return 1;
                if (b == null) return -1;
                return a.Price.CompareTo(b.Price);
            });

            for (int i = 0; i < consumableList.Count; i++)
            {
                AddConsumableCard(consumableList[i]);
            }
        }

        private void AddWeaponCard(WeaponDataSO weapon)
        {
            if (weapon == null)
            {
                return;
            }

            bool owned = shop.IsWeaponOwned(weapon);
            string description = string.IsNullOrWhiteSpace(weapon.Description) ? "Огнестрельное оружие." : weapon.Description;
            AddCard(
                weapon.Icon,
                weapon.DisplayName,
                weapon.GetStatsLine() + $"\n{description}",
                weapon.Price,
                owned,
                () =>
                {
                    if (shop.BuyWeapon(weapon))
                    {
                        Rebuild();
                    }
                });
        }

        private void AddBuildingCard(BuildingDataSO building)
        {
            if (building == null)
            {
                return;
            }

            string description = string.IsNullOrWhiteSpace(building.Description) ? "Постройка для обороны." : building.Description;
            AddCard(
                building.Icon,
                building.DisplayName,
                building.GetStatsLine() + $"\n{description}",
                building.Price,
                false,
                () =>
                {
                    if (shop.BuyBuilding(building) && shop.IsOpen)
                    {
                        Rebuild();
                    }
                });
        }

        private void AddConsumableCard(ConsumableDataSO consumable)
        {
            if (consumable == null)
            {
                return;
            }

            string description = string.IsNullOrWhiteSpace(consumable.Description) ? "Одноразовый предмет." : consumable.Description;
            AddCard(
                consumable.Icon,
                consumable.DisplayName,
                consumable.GetStatsLine() + $"\n{description}",
                consumable.Price,
                false,
                () =>
                {
                    if (shop.BuyConsumable(consumable))
                    {
                        Rebuild();
                    }
                });
        }

        private void AddSection(string title)
        {
            RectTransform header = UIFactory.CreateUIObject($"Section_{title}", content);
            LayoutElement layout = header.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 36f;
            layout.minHeight = 36f;

            Text label = UIFactory.CreateText("Label", header, $"■  {title}", 20, UiTheme.Accent);
            UIFactory.Stretch(label.rectTransform, 2f, 4f, 2f, 2f);
        }

        private void AddCard(
            Sprite iconSprite,
            string title,
            string details,
            int price,
            bool owned,
            Action purchaseAction)
        {
            RectTransform card = UIFactory.CreateUIObject("Card", content);
            Image background = card.gameObject.AddComponent<Image>();
            background.color = UiTheme.PanelSoft;
            background.raycastTarget = true;

            LayoutElement cardLayout = card.gameObject.AddComponent<LayoutElement>();
            cardLayout.preferredHeight = 116f;
            cardLayout.minHeight = 116f;
            cardLayout.flexibleWidth = 1f;

            HorizontalLayoutGroup cardGroup = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            cardGroup.padding = new RectOffset(12, 12, 10, 10);
            cardGroup.spacing = 14f;
            cardGroup.childAlignment = TextAnchor.MiddleLeft;
            cardGroup.childControlWidth = true;
            cardGroup.childControlHeight = true;
            cardGroup.childForceExpandWidth = false;
            cardGroup.childForceExpandHeight = false;

            Image iconBackground = UIFactory.CreatePanel("IconBackground", card, new Color(0.07f, 0.09f, 0.12f, 1f));
            LayoutElement iconLayout = iconBackground.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 78f;
            iconLayout.minWidth = 78f;
            iconLayout.preferredHeight = 78f;
            iconLayout.minHeight = 78f;

            if (iconSprite != null)
            {
                Image icon = UIFactory.CreateImage("Icon", iconBackground.transform, iconSprite, Color.white);
                UIFactory.Stretch(icon.rectTransform, 5f, 5f, 5f, 5f);
            }

            Text info = UIFactory.CreateText(
                "Info",
                card,
                $"<size=18><b>{title}</b></size>\n<size=12><color=#{UiTheme.ColorToHex(UiTheme.Text)}>{details}</color></size>",
                14,
                UiTheme.Text,
                TextAnchor.MiddleLeft);
            LayoutElement textLayout = info.gameObject.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 1f;
            textLayout.minWidth = 300f;
            textLayout.preferredHeight = 92f;

            bool canAfford = currency != null && currency.Balance >= price;
            string buttonText = owned
                ? "КУПЛЕНО"
                : canAfford ? $"КУПИТЬ\n{price}" : $"НЕ ХВАТАЕТ\n{price}";
            Button buyButton = UIFactory.CreateButton(
                "Buy",
                card,
                buttonText,
                owned ? new Color(0.25f, 0.29f, 0.32f, 1f) : UiTheme.AccentDark,
                owned ? UiTheme.Muted : UiTheme.Text,
                15);
            LayoutElement buttonLayout = buyButton.gameObject.AddComponent<LayoutElement>();
            buttonLayout.preferredWidth = 148f;
            buttonLayout.minWidth = 148f;
            buttonLayout.preferredHeight = 68f;
            buttonLayout.minHeight = 68f;
            buyButton.interactable = !owned && canAfford;
            if (!owned)
            {
                buyButton.onClick.AddListener(() => purchaseAction?.Invoke());
            }

            rows.Add(new ShopRow
            {
                Button = buyButton,
                Label = buyButton.GetComponentInChildren<Text>(),
                Price = price,
                Owned = owned
            });
        }

        private void ClearContent()
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Destroy(content.GetChild(i).gameObject);
            }
        }

        private void RefreshWallet()
        {
            if (walletText != null && currency != null)
            {
                walletText.text = $"{currency.Balance:N0} МОНЕТ";
            }
        }

        private void SetStatus(string message, bool success)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = message;
            statusText.color = success ? UiTheme.Success : UiTheme.Danger;
        }

        private void MakeFullScreen()
        {
            RectTransform rect = GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = gameObject.AddComponent<RectTransform>();
            }

            UIFactory.MakeFullScreen(rect);
        }

        private void OnDestroy()
        {
            if (shop != null)
            {
                shop.ShopToggled -= HandleShopToggled;
                shop.NotificationRequested -= HandleNotification;
            }

            if (currency != null)
            {
                currency.CoinsChanged -= HandleCoinsChanged;
            }
        }
    }
}
