using PixelZombieSurvivor.Building;
using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Economy;
using PixelZombieSurvivor.Player;
using PixelZombieSurvivor.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelZombieSurvivor.Core
{
    /// <summary>
    /// Creates the Canvas, EventSystem, HUD and shop at runtime. This keeps the
    /// generated scene minimal and makes references explicit in one place.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class GameUIBootstrap : MonoBehaviour
    {
        private void Start()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                Debug.LogError("GameUIBootstrap requires a GameManager in the scene.", this);
                return;
            }

            EnsureEventSystem();

            // Cheats live in their own object so the arena scene stays untouched.
            CheatManager.EnsureInstalled();

            Canvas canvas = CreateCanvas();
            HudController hud = CreateHud(canvas.transform, manager);
            ShopUIController shop = CreateShop(canvas.transform, manager);

            if (hud == null || shop == null)
            {
                Debug.LogError("UI could not be initialized because scene references are missing.", this);
            }
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject(
                "GameCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private HudController CreateHud(Transform canvas, GameManager manager)
        {
            RectTransform root = UIFactory.CreateUIObject("HUD", canvas);
            UIFactory.MakeFullScreen(root);

            HudController hud = root.gameObject.AddComponent<HudController>();
            PlayerWeaponController weapons = FindAnyObjectByType<PlayerWeaponController>();

            hud.Initialize(
                manager,
                manager.WaveManager,
                manager.Player,
                CurrencyManager.Instance,
                weapons,
                manager.ShopManager,
                manager.PlacementController);

            return hud;
        }

        private ShopUIController CreateShop(Transform canvas, GameManager manager)
        {
            RectTransform root = UIFactory.CreateUIObject("Shop", canvas);
            UIFactory.MakeFullScreen(root);

            ShopUIController shop = root.gameObject.AddComponent<ShopUIController>();
            PlayerWeaponController weapons = FindAnyObjectByType<PlayerWeaponController>();
            BuildingPlacementController placement = FindAnyObjectByType<BuildingPlacementController>();

            shop.Initialize(
                manager.ShopManager,
                manager.Player,
                weapons,
                CurrencyManager.Instance);

            return shop;
        }
    }
}
