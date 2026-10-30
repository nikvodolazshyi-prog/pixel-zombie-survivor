using System.Collections;
using System.Collections.Generic;
using System.Text;
using PixelZombieSurvivor.Building;
using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Data;
using PixelZombieSurvivor.Economy;
using PixelZombieSurvivor.Enemies;
using PixelZombieSurvivor.Player;
using PixelZombieSurvivor.Waves;
using UnityEngine;
using UnityEngine.UI;

namespace PixelZombieSurvivor.UI
{
    /// <summary>
    /// Builds and updates the combat HUD. It only reads public manager properties and
    /// subscribes to their events, keeping gameplay completely UI-agnostic.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        private static readonly Color ShieldColor = new Color(0.4f, 0.78f, 1f, 1f);

        private GameManager gameManager;
        private WaveManager waveManager;
        private PlayerHealth player;
        private CurrencyManager currency;
        private PlayerWeaponController weapons;
        private ShopManager shop;
        private BuildingPlacementController placement;
        private PlayerBuffs buffs;
        private PlayerController playerController;

        private Text healthText;
        private Image healthFill;
        private Image shieldFill;
        private Text shieldText;
        private Text buffText;
        private Text waveTitle;
        private Text remainingText;
        private Text countdownText;
        private Text coinsText;
        private Text weaponNameText;
        private Text ammoText;
        private Text reloadText;
        private Text inventoryText;
        private Text toastText;
        private Text bannerText;
        private Text buildHintText;
        private Text gameOverStatsText;
        private RectTransform crosshair;
        private CanvasGroup toastGroup;
        private CanvasGroup bannerGroup;
        private GameObject gameOverPanel;
        private GameObject buildHintPanel;
        private Text hintsText;
        private GameObject cheatBadge;
        private Text rebindHintText;
        private readonly Dictionary<GameAction, Text> bindLabels = new Dictionary<GameAction, Text>();
        private GameAction? pendingRebind;
        private GameObject startWavePanel;
        private Text startWaveLabel;
        private GameObject buffPanel;
        private Text buffSubtitle;
        private GameObject pausePanel;
        private Image dashFill;
        private Text dashLabel;

        private GameObject inspectPanel;
        private Text inspectTitle;
        private Text inspectLevel;
        private Text inspectStats;
        private Text inspectUpgradeLabel;
        private Button inspectUpgradeButton;
        private Text inspectSellLabel;
        private BuildingBase inspectedBuilding;

        private GameObject bossPanel;
        private Image bossFill;
        private Text bossNameText;
        private Text bossPhaseText;
        private BossController bossTarget;

        private Coroutine toastRoutine;
        private Coroutine bannerRoutine;
        private bool initialized;
        private bool shopOpen;

        public void Initialize(
            GameManager manager,
            WaveManager waves,
            PlayerHealth playerHealth,
            CurrencyManager currencyManager,
            PlayerWeaponController weaponController,
            ShopManager shopManager,
            BuildingPlacementController placementController)
        {
            gameManager = manager;
            waveManager = waves;
            player = playerHealth;
            currency = currencyManager;
            weapons = weaponController;
            shop = shopManager;
            placement = placementController;
            buffs = playerHealth != null ? playerHealth.GetComponent<PlayerBuffs>() : null;
            if (buffs == null && playerHealth != null)
            {
                // Safety net: PlayerBuffs installs itself, but never let the late-game
                // upgrade UI silently disappear if that hook did not run.
                buffs = playerHealth.gameObject.AddComponent<PlayerBuffs>();
            }

            playerController = playerHealth != null ? playerHealth.GetComponent<PlayerController>() : null;

            Build();
            Subscribe();
            RefreshAll();
            initialized = true;
        }

        private void Update()
        {
            if (!initialized || crosshair == null)
            {
                return;
            }

            HandleRebindInput();

            bool canShow = !shopOpen &&
                           (placement == null || !placement.IsBusy) &&
                           gameManager != null &&
                           gameManager.State != GameState.GameOver &&
                           !gameManager.IsPaused &&
                           (buffs == null || !buffs.AwaitingChoice);
            crosshair.gameObject.SetActive(canShow);

            if (canShow)
            {
                crosshair.position = Input.mousePosition;
            }

            RefreshDash();
            RefreshIntermission();
            RefreshBossBar();
            RefreshCheatBadge();
        }

        private void Build()
        {
            if (player == null || waveManager == null || weapons == null || currency == null)
            {
                return;
            }

            BuildHealthPanel();
            BuildWavePanel();
            BuildCoinPanel();
            BuildWeaponPanel();
            BuildDashPanel();
            BuildHints();
            BuildCrosshair();
            BuildToast();
            BuildBanner();
            BuildBuildHint();
            BuildCheatBadge();
            BuildInspectionPanel();
            BuildBossBar();
            BuildStartWavePanel();
            BuildBuffPanel();
            BuildPausePanel();
            BuildGameOverPanel();
        }

        private void BuildHealthPanel()
        {
            Image panel = UIFactory.CreatePanel("HealthPanel", transform, UiTheme.Panel);
            UIFactory.Anchor(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(334f, 124f));

            Text label = UIFactory.CreateText("Label", panel.transform, "ЗДОРОВЬЕ", 13, UiTheme.Muted);
            UIFactory.Anchor(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -9f), new Vector2(280f, 20f));

            Image background = UIFactory.CreatePanel("BarBackground", panel.transform, new Color(0.03f, 0.04f, 0.055f, 1f));
            UIFactory.Anchor(background.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -38f), new Vector2(302f, 30f));

            // Filled images need a sprite; otherwise uGUI falls back to a plain quad
            // and ignores fillAmount.
            Image fill = UIFactory.CreateImage("Fill", background.transform, RuntimeSprites.White, UiTheme.Health, false);
            UIFactory.Stretch(fill.rectTransform, 3f, 3f, 3f, 3f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            healthFill = fill;

            healthText = UIFactory.CreateText("Value", background.transform, "100 / 100", 17, UiTheme.Text, TextAnchor.MiddleCenter);
            UIFactory.Stretch(healthText.rectTransform, 4f, 2f, 4f, 2f);

            // Thin shield bar: hidden behind an empty fill until the shield buff is picked.
            Image shieldBackground = UIFactory.CreatePanel("ShieldBackground", panel.transform, new Color(0.03f, 0.04f, 0.055f, 1f));
            UIFactory.Anchor(shieldBackground.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -72f), new Vector2(302f, 10f));

            Image shield = UIFactory.CreateImage("ShieldFill", shieldBackground.transform, RuntimeSprites.White, ShieldColor, false);
            UIFactory.Stretch(shield.rectTransform, 2f, 2f, 2f, 2f);
            shield.type = Image.Type.Filled;
            shield.fillMethod = Image.FillMethod.Horizontal;
            shield.fillOrigin = (int)Image.OriginHorizontal.Left;
            shield.fillAmount = 0f;
            shieldFill = shield;

            shieldText = UIFactory.CreateText("Shield", panel.transform, "ЩИТ —", 11, ShieldColor);
            UIFactory.Anchor(shieldText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -85f), new Vector2(150f, 16f));

            buffText = UIFactory.CreateText("Buffs", panel.transform, string.Empty, 11, UiTheme.Success, TextAnchor.MiddleRight);
            UIFactory.Anchor(buffText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -85f), new Vector2(302f, 16f));
        }

        private void BuildWavePanel()
        {
            Image panel = UIFactory.CreatePanel("WavePanel", transform, UiTheme.Panel);
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(440f, 96f));

            waveTitle = UIFactory.CreateText("Title", panel.transform, "ПОДГОТОВКА", 24, UiTheme.Accent, TextAnchor.MiddleCenter);
            UIFactory.Anchor(waveTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -7f), new Vector2(420f, 32f));

            remainingText = UIFactory.CreateText("Remaining", panel.transform, "Осталось зомби: 0", 15, UiTheme.Text, TextAnchor.MiddleCenter);
            UIFactory.Anchor(remainingText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(420f, 22f));

            countdownText = UIFactory.CreateText("Countdown", panel.transform, "Следующая волна...", 14, UiTheme.Muted, TextAnchor.MiddleCenter);
            UIFactory.Anchor(countdownText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -65f), new Vector2(420f, 22f));
        }

        private void BuildCoinPanel()
        {
            Image panel = UIFactory.CreatePanel("CoinPanel", transform, UiTheme.Panel);
            UIFactory.Anchor(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(250f, 62f));

            Text icon = UIFactory.CreateText("Icon", panel.transform, "●", 23, UiTheme.Gold, TextAnchor.MiddleCenter);
            UIFactory.Anchor(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(38f, 38f));

            Text label = UIFactory.CreateText("Label", panel.transform, "МОНЕТЫ", 11, UiTheme.Muted);
            UIFactory.Anchor(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(52f, 11f), new Vector2(180f, 18f));

            coinsText = UIFactory.CreateText("Value", panel.transform, "0", 23, UiTheme.Gold);
            UIFactory.Anchor(coinsText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(52f, -14f), new Vector2(180f, 30f));
        }

        private void BuildWeaponPanel()
        {
            Image panel = UIFactory.CreatePanel("WeaponPanel", transform, UiTheme.Panel);
            UIFactory.Anchor(panel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 28f), new Vector2(520f, 152f));

            weaponNameText = UIFactory.CreateText("WeaponName", panel.transform, "ПИСТОЛЕТ", 16, UiTheme.Accent);
            UIFactory.Anchor(weaponNameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -8f), new Vector2(484f, 24f));

            ammoText = UIFactory.CreateText("Ammo", panel.transform, "12 / 72", 31, UiTheme.Text);
            UIFactory.Anchor(ammoText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -31f), new Vector2(300f, 44f));

            reloadText = UIFactory.CreateText("Reload", panel.transform, "R — ПЕРЕЗАРЯДКА", 13, UiTheme.Muted, TextAnchor.MiddleRight);
            UIFactory.Anchor(reloadText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -41f), new Vector2(260f, 26f));

            inventoryText = UIFactory.CreateText("Inventory", panel.transform, "1. ПИСТОЛЕТ", 13, UiTheme.Muted);
            UIFactory.Anchor(inventoryText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -88f), new Vector2(484f, 46f));
        }

        private void BuildHints()
        {
            hintsText = UIFactory.CreateText(
                "Hints",
                transform,
                BuildHintsContent(),
                13,
                UiTheme.Muted,
                TextAnchor.LowerRight);
            UIFactory.Anchor(hintsText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 26f), new Vector2(820f, 78f));
        }

        private static string BuildHintsContent()
        {
            string move = $"{Key(GameAction.MoveUp)}/{Key(GameAction.MoveLeft)}/{Key(GameAction.MoveDown)}/{Key(GameAction.MoveRight)}";
            string gold = UiTheme.ColorToHex(UiTheme.Gold);
            return $"{move} — движение   •   {Key(GameAction.Shoot)} — огонь   •   {Key(GameAction.Reload)} — перезарядка   •   {Key(GameAction.Dash)} — рывок\n" +
                   $"{Key(GameAction.Shop)} / TAB — магазин   •   {Key(GameAction.Demolish)} — режим сноса   •   {Key(GameAction.Interact)} — улучшить постройку   •   1–9 / колесо — оружие   •   {Key(GameAction.Pause)} — пауза\n" +
                   $"<color=#{gold}>ALT+1</color> — режим бога   •   <color=#{gold}>ALT+E</color> — пропуск волны   •   бинды — в паузе";
        }

        private static string Key(GameAction action)
        {
            return InputBindings.GetKeyLabel(action);
        }

        private void RefreshHints()
        {
            if (hintsText != null)
            {
                hintsText.text = BuildHintsContent();
            }
        }

        private void BuildDashPanel()
        {
            Image panel = UIFactory.CreatePanel("DashPanel", transform, UiTheme.Panel);
            UIFactory.Anchor(panel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 192f), new Vector2(300f, 54f));

            dashLabel = UIFactory.CreateText("Label", panel.transform, "SPACE — РЫВОК", 12, UiTheme.Muted);
            UIFactory.Anchor(dashLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -6f), new Vector2(268f, 16f));

            Image background = UIFactory.CreatePanel("BarBackground", panel.transform, new Color(0.03f, 0.04f, 0.055f, 1f));
            UIFactory.Anchor(background.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -28f), new Vector2(268f, 10f));

            Image fill = UIFactory.CreateImage("Fill", background.transform, RuntimeSprites.White, UiTheme.Success, false);
            UIFactory.Stretch(fill.rectTransform, 2f, 2f, 2f, 2f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            dashFill = fill;
        }

        private void BuildStartWavePanel()
        {
            Image panel = UIFactory.CreatePanel("StartWavePanel", transform, new Color(0.07f, 0.09f, 0.12f, 0.97f));
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(460f, 132f));
            startWavePanel = panel.gameObject;

            Text title = UIFactory.CreateText("Title", panel.transform, "ПЕРЕДЫШКА", 18, UiTheme.Accent, TextAnchor.MiddleCenter);
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(430f, 24f));

            Text subtitle = UIFactory.CreateText("Subtitle", panel.transform, $"Закупитесь в магазине ({Key(GameAction.Shop)} / TAB) и начинайте, когда будете готовы.", 12, UiTheme.Muted, TextAnchor.MiddleCenter);
            UIFactory.Anchor(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(430f, 20f));

            Button startButton = UIFactory.CreateButton("StartButton", panel.transform, "НАЧАТЬ ВОЛНУ", UiTheme.Accent, UiTheme.Background, 18);
            UIFactory.Anchor(startButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(300f, 48f));
            startButton.onClick.AddListener(HandleStartWaveClicked);

            startWaveLabel = startButton.GetComponentInChildren<Text>();
            startWavePanel.SetActive(false);
        }

        private void BuildBuffPanel()
        {
            Image overlay = UIFactory.CreatePanel("BuffOverlay", transform, new Color(0.01f, 0.015f, 0.02f, 0.9f), true);
            overlay.raycastTarget = true;
            buffPanel = overlay.gameObject;

            Image panel = UIFactory.CreatePanel("Panel", overlay.transform, UiTheme.PanelRaised);
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 470f));

            Text title = UIFactory.CreateText("Title", panel.transform, "ВЫБЕРИТЕ УЛУЧШЕНИЕ", 34, UiTheme.Accent, TextAnchor.MiddleCenter);
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(900f, 44f));

            buffSubtitle = UIFactory.CreateText("Subtitle", panel.transform, "Волна пройдена — выберите усиление.", 15, UiTheme.Muted, TextAnchor.MiddleCenter);
            UIFactory.Anchor(buffSubtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(900f, 24f));

            BuildBuffCard(panel.transform, BuffType.Vampirism, -300f, UiTheme.Health);
            BuildBuffCard(panel.transform, BuffType.Speed, 0f, UiTheme.Success);
            BuildBuffCard(panel.transform, BuffType.Shield, 300f, ShieldColor);

            Text hint = UIFactory.CreateText("Hint", panel.transform, "Улучшения складываются. Следующий выбор — после каждой 5-й волны.", 12, UiTheme.Muted, TextAnchor.MiddleCenter);
            UIFactory.Anchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(900f, 20f));

            buffPanel.SetActive(false);
        }

        private void BuildBuffCard(Transform parent, BuffType type, float offsetX, Color accent)
        {
            Image card = UIFactory.CreatePanel("Buff_" + type, parent, UiTheme.PanelSoft);
            UIFactory.Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(offsetX, -12f), new Vector2(280f, 250f));
            card.raycastTarget = true;

            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f),
                pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            button.onClick.AddListener(() => ChooseBuff(type));

            Text title = UIFactory.CreateText("Title", card.transform, GetBuffTitle(type), 22, accent, TextAnchor.MiddleCenter);
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(250f, 30f));

            Text description = UIFactory.CreateText("Description", card.transform, GetBuffDescription(type), 14, UiTheme.Text, TextAnchor.UpperCenter);
            UIFactory.Anchor(description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(244f, 130f));

            Text pick = UIFactory.CreateText("Pick", card.transform, "ВЫБРАТЬ", 14, UiTheme.Background, TextAnchor.MiddleCenter);
            UIFactory.Anchor(pick.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(200f, 34f));

            Image pickBackground = UIFactory.CreatePanel("PickBackground", pick.transform, accent);
            pickBackground.rectTransform.SetAsFirstSibling();
            UIFactory.Stretch(pickBackground.rectTransform);
            pickBackground.raycastTarget = false;
        }

        private string GetBuffTitle(BuffType type)
        {
            return buffs != null
                ? buffs.GetBuffTitle(type)
                : type == BuffType.Vampirism ? "ВАМПИРИЗМ" : type == BuffType.Speed ? "СКОРОСТЬ" : "ЩИТ";
        }

        private string GetBuffDescription(BuffType type)
        {
            if (buffs != null)
            {
                return buffs.GetBuffDescription(type);
            }

            switch (type)
            {
                case BuffType.Vampirism: return "+3 HP за каждое убийство";
                case BuffType.Speed: return "+12% к скорости движения";
                default: return "30 щита, восстанавливается\nв начале каждой волны";
            }
        }

        private void BuildPausePanel()
        {
            Image overlay = UIFactory.CreatePanel("PauseOverlay", transform, new Color(0.01f, 0.015f, 0.02f, 0.85f), true);
            overlay.raycastTarget = true;
            pausePanel = overlay.gameObject;

            Image panel = UIFactory.CreatePanel("Panel", overlay.transform, UiTheme.PanelRaised);
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1020f, 640f));

            Text title = UIFactory.CreateText("Title", panel.transform, "ПАУЗА", 40, UiTheme.Accent, TextAnchor.MiddleCenter);
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(960f, 50f));

            rebindHintText = UIFactory.CreateText(
                "Hint",
                panel.transform,
                "Нажмите на клавишу справа и введите новую. Настройки сохраняются автоматически.",
                13,
                UiTheme.Muted,
                TextAnchor.MiddleCenter);
            UIFactory.Anchor(rebindHintText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(960f, 22f));

            Text bindTitle = UIFactory.CreateText("BindTitle", panel.transform, "УПРАВЛЕНИЕ", 15, UiTheme.Gold);
            UIFactory.Anchor(bindTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -98f), new Vector2(300f, 22f));

            GameAction[] leftActions =
            {
                GameAction.MoveUp, GameAction.MoveDown, GameAction.MoveLeft,
                GameAction.MoveRight, GameAction.Shoot, GameAction.Reload
            };
            GameAction[] rightActions =
            {
                GameAction.Dash, GameAction.Shop, GameAction.Interact,
                GameAction.Demolish, GameAction.Pause
            };

            for (int i = 0; i < leftActions.Length; i++)
            {
                CreateBindRow(panel.transform, leftActions[i], 44f, -130f - i * 44f);
            }

            for (int i = 0; i < rightActions.Length; i++)
            {
                CreateBindRow(panel.transform, rightActions[i], 540f, -130f - i * 44f);
            }

            Text cheatHint = UIFactory.CreateText(
                "CheatHint",
                panel.transform,
                "ALT+1 — режим бога   •   ALT+E — пропуск волны",
                12,
                UiTheme.Gold);
            UIFactory.Anchor(cheatHint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(44f, 92f), new Vector2(600f, 20f));

            Button reset = UIFactory.CreateButton("ResetBinds", panel.transform, "СБРОСИТЬ БИНДЫ", UiTheme.PanelSoft, UiTheme.Text, 14);
            UIFactory.Anchor(reset.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(44f, 24f), new Vector2(260f, 50f));
            reset.onClick.AddListener(HandleResetBindingsClicked);

            Button resume = UIFactory.CreateButton("Resume", panel.transform, "ПРОДОЛЖИТЬ", UiTheme.Accent, UiTheme.Background, 18);
            UIFactory.Anchor(resume.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-44f, 24f), new Vector2(300f, 54f));
            resume.onClick.AddListener(() =>
            {
                EndRebind();

                if (gameManager != null)
                {
                    gameManager.SetPaused(false);
                }
            });

            RefreshBindLabels();
            pausePanel.SetActive(false);
        }

        private void CreateBindRow(Transform parent, GameAction action, float x, float y)
        {
            Text label = UIFactory.CreateText(
                "BindLabel_" + action,
                parent,
                InputBindings.GetActionLabel(action),
                13,
                UiTheme.Text);
            UIFactory.Anchor(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(240f, 34f));

            Button button = UIFactory.CreateButton(
                "BindKey_" + action,
                parent,
                InputBindings.GetKeyLabel(action),
                UiTheme.PanelSoft,
                UiTheme.Accent,
                13);
            UIFactory.Anchor(button.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + 250f, y - 2f), new Vector2(170f, 34f));
            button.onClick.AddListener(() => BeginRebind(action));
            bindLabels[action] = button.GetComponentInChildren<Text>();
        }

        private void BeginRebind(GameAction action)
        {
            pendingRebind = action;
            InputBindings.BeginCapture();

            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }

            RefreshBindLabels();
        }

        private void HandleRebindInput()
        {
            if (!pendingRebind.HasValue)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Esc cancels a rebind, except when the player explicitly rebinds Pause.
                if (pendingRebind.Value == GameAction.Pause)
                {
                    InputBindings.Set(GameAction.Pause, KeyCode.Escape);
                }

                EndRebind();
                return;
            }

            if (!InputBindings.TryGetPressedKey(out KeyCode key))
            {
                return;
            }

            InputBindings.Set(pendingRebind.Value, key);
            EndRebind();
        }

        private void EndRebind()
        {
            pendingRebind = null;
            InputBindings.EndCapture();
            RefreshBindLabels();
        }

        private void RefreshBindLabels()
        {
            foreach (KeyValuePair<GameAction, Text> pair in bindLabels)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                bool waiting = pendingRebind.HasValue && pendingRebind.Value == pair.Key;
                pair.Value.text = waiting ? "..." : InputBindings.GetKeyLabel(pair.Key);
                pair.Value.color = waiting ? UiTheme.Gold : UiTheme.Accent;
            }

            if (rebindHintText != null)
            {
                rebindHintText.text = pendingRebind.HasValue
                    ? $"Введите новую клавишу для «{InputBindings.GetActionLabel(pendingRebind.Value)}»   •   ESC — отмена"
                    : "Нажмите на клавишу справа и введите новую. Настройки сохраняются автоматически.";
            }
        }

        private void HandleResetBindingsClicked()
        {
            pendingRebind = null;
            InputBindings.EndCapture();
            InputBindings.ResetAll();
        }

        private void HandleBindingsChanged()
        {
            RefreshBindLabels();
            RefreshHints();
            RefreshWeapon();
            RefreshDash();
            RefreshBuildHint();
        }

        private void BuildCrosshair()
        {
            crosshair = UIFactory.CreateUIObject("Crosshair", transform);
            crosshair.sizeDelta = new Vector2(28f, 28f);
            crosshair.pivot = new Vector2(0.5f, 0.5f);
            crosshair.anchorMin = Vector2.zero;
            crosshair.anchorMax = Vector2.zero;

            CreateCrosshairBar("Top", new Vector2(0f, 8f), new Vector2(2f, 8f));
            CreateCrosshairBar("Bottom", new Vector2(0f, -8f), new Vector2(2f, 8f));
            CreateCrosshairBar("Left", new Vector2(-8f, 0f), new Vector2(8f, 2f));
            CreateCrosshairBar("Right", new Vector2(8f, 0f), new Vector2(8f, 2f));

            Image dot = UIFactory.CreateImage("Dot", crosshair, null, UiTheme.Accent, false);
            UIFactory.Anchor(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 3f));
        }

        private void CreateCrosshairBar(string objectName, Vector2 position, Vector2 size)
        {
            Image bar = UIFactory.CreateImage(objectName, crosshair, null, new Color(1f, 0.86f, 0.54f, 0.95f), false);
            UIFactory.Anchor(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        }

        private void BuildToast()
        {
            Image toast = UIFactory.CreatePanel("Toast", transform, new Color(0.07f, 0.09f, 0.12f, 0.97f));
            UIFactory.Anchor(toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -216f), new Vector2(640f, 50f));
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;

            toastText = UIFactory.CreateText("Text", toast.transform, string.Empty, 16, UiTheme.Text, TextAnchor.MiddleCenter);
            UIFactory.Stretch(toastText.rectTransform, 14f, 4f, 14f, 4f);
        }

        private void BuildBanner()
        {
            RectTransform banner = UIFactory.CreateUIObject("WaveBanner", transform);
            UIFactory.Anchor(banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 145f), new Vector2(820f, 110f));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;

            bannerText = UIFactory.CreateText("Text", banner, string.Empty, 50, UiTheme.Accent, TextAnchor.MiddleCenter);
            UIFactory.Stretch(bannerText.rectTransform);
        }

        private void BuildBuildHint()
        {
            Image panel = UIFactory.CreatePanel("BuildHint", transform, new Color(0.07f, 0.09f, 0.12f, 0.97f));
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 240f), new Vector2(780f, 42f));
            buildHintPanel = panel.gameObject;

            buildHintText = UIFactory.CreateText("Text", panel.transform, string.Empty, 15, UiTheme.Success, TextAnchor.MiddleCenter);
            UIFactory.Stretch(buildHintText.rectTransform, 12f, 4f, 12f, 4f);
            panel.gameObject.SetActive(false);
        }

        private void BuildCheatBadge()
        {
            Image panel = UIFactory.CreatePanel("CheatBadge", transform, new Color(0.28f, 0.12f, 0.34f, 0.96f));
            UIFactory.Anchor(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -164f), new Vector2(360f, 40f));
            cheatBadge = panel.gameObject;

            Text label = UIFactory.CreateText(
                "Label",
                panel.transform,
                "РЕЖИМ БОГА   •   ALT+1 — ВЫКЛ",
                13,
                UiTheme.Gold,
                TextAnchor.MiddleCenter);
            UIFactory.Stretch(label.rectTransform, 8f, 4f, 8f, 4f);

            cheatBadge.SetActive(false);
        }

        private void RefreshCheatBadge()
        {
            if (cheatBadge == null)
            {
                return;
            }

            bool active = CheatManager.GodMode;
            if (cheatBadge.activeSelf != active)
            {
                cheatBadge.SetActive(active);
            }
        }

        private void BuildInspectionPanel()
        {
            Image border = UIFactory.CreatePanel("InspectPanelBorder", transform, UiTheme.AccentDark);
            UIFactory.Anchor(border.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 8f), new Vector2(372f, 402f));
            inspectPanel = border.gameObject;

            Image panel = UIFactory.CreatePanel("Panel", border.transform, UiTheme.Panel);
            UIFactory.Stretch(panel.rectTransform, 3f, 3f, 3f, 3f);

            inspectTitle = UIFactory.CreateText("Title", panel.transform, "ПОСТРОЙКА", 22, UiTheme.Accent);
            UIFactory.Anchor(inspectTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(280f, 30f));

            Button close = UIFactory.CreateButton("Close", panel.transform, "×", UiTheme.PanelSoft, UiTheme.Text, 22);
            UIFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -10f), new Vector2(38f, 38f));
            close.onClick.AddListener(() => placement?.CloseSelection());

            inspectLevel = UIFactory.CreateText("Level", panel.transform, "УРОВЕНЬ 1", 14, UiTheme.Gold);
            UIFactory.Anchor(inspectLevel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -46f), new Vector2(320f, 22f));

            inspectStats = UIFactory.CreateText("Stats", panel.transform, string.Empty, 14, UiTheme.Text, TextAnchor.UpperLeft);
            UIFactory.Anchor(inspectStats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -78f), new Vector2(332f, 148f));

            inspectUpgradeButton = UIFactory.CreateButton("Upgrade", panel.transform, "УЛУЧШИТЬ", UiTheme.AccentDark, UiTheme.Text, 15);
            UIFactory.Anchor(inspectUpgradeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(320f, 48f));
            inspectUpgradeButton.onClick.AddListener(HandleUpgradeClicked);
            inspectUpgradeLabel = inspectUpgradeButton.GetComponentInChildren<Text>();

            Button demolish = UIFactory.CreateButton("Demolish", panel.transform, "СНЕСТИ", new Color(0.44f, 0.18f, 0.16f, 1f), UiTheme.Text, 15);
            UIFactory.Anchor(demolish.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(320f, 40f));
            demolish.onClick.AddListener(() => placement?.DemolishSelected());
            inspectSellLabel = demolish.GetComponentInChildren<Text>();

            Text hint = UIFactory.CreateText("Hint", panel.transform, "E / ESC — закрыть", 11, UiTheme.Muted, TextAnchor.MiddleRight);
            UIFactory.Anchor(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 60f), new Vector2(180f, 18f));

            inspectPanel.SetActive(false);
        }

        private void BuildBossBar()
        {
            Image border = UIFactory.CreatePanel("BossBarBorder", transform, new Color(0.55f, 0.1f, 0.09f, 1f));
            UIFactory.Anchor(border.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(760f, 70f));
            bossPanel = border.gameObject;

            Image panel = UIFactory.CreatePanel("Panel", border.transform, new Color(0.08f, 0.05f, 0.06f, 0.98f));
            UIFactory.Stretch(panel.rectTransform, 3f, 3f, 3f, 3f);

            bossNameText = UIFactory.CreateText("Name", panel.transform, "БОСС", 20, UiTheme.Health);
            UIFactory.Anchor(bossNameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -6f), new Vector2(420f, 26f));

            bossPhaseText = UIFactory.CreateText("Phase", panel.transform, "ФАЗА 1", 13, UiTheme.Muted, TextAnchor.MiddleRight);
            UIFactory.Anchor(bossPhaseText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -8f), new Vector2(220f, 22f));

            Image background = UIFactory.CreatePanel("BarBackground", panel.transform, new Color(0.02f, 0.02f, 0.03f, 1f));
            UIFactory.Anchor(background.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 9f), new Vector2(700f, 20f));

            Image fill = UIFactory.CreateImage("Fill", background.transform, RuntimeSprites.White, UiTheme.Health, false);
            UIFactory.Stretch(fill.rectTransform, 2f, 2f, 2f, 2f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            bossFill = fill;

            bossPanel.SetActive(false);
        }

        private void BuildGameOverPanel()
        {
            Image overlay = UIFactory.CreatePanel("GameOver", transform, new Color(0.01f, 0.015f, 0.02f, 0.88f), true);
            overlay.raycastTarget = true;
            gameOverPanel = overlay.gameObject;

            Image panel = UIFactory.CreatePanel("Panel", overlay.transform, UiTheme.PanelRaised);
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 390f));

            Text title = UIFactory.CreateText("Title", panel.transform, "ВЫ ПОГИБЛИ", 52, UiTheme.Danger, TextAnchor.MiddleCenter);
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(600f, 68f));

            Text subtitle = UIFactory.CreateText("Subtitle", panel.transform, "Город снова стал тише...", 16, UiTheme.Muted, TextAnchor.MiddleCenter);
            UIFactory.Anchor(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(600f, 28f));

            gameOverStatsText = UIFactory.CreateText("Stats", panel.transform, string.Empty, 22, UiTheme.Text, TextAnchor.MiddleCenter);
            UIFactory.Anchor(gameOverStatsText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Vector2(600f, 100f));

            Button restart = UIFactory.CreateButton("Restart", panel.transform, "ПЕРЕЗАПУСТИТЬ", UiTheme.Accent, UiTheme.Background, 20);
            UIFactory.Anchor(restart.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(300f, 58f));
            restart.onClick.AddListener(() =>
            {
                if (gameManager != null)
                {
                    gameManager.RestartGame();
                }
            });

            gameOverPanel.SetActive(false);
        }

        private void Subscribe()
        {
            if (player != null) player.HealthChanged += HandleHealthChanged;
            if (currency != null) currency.CoinsChanged += HandleCoinsChanged;
            if (weapons != null)
            {
                weapons.WeaponChanged += HandleWeaponChanged;
                weapons.AmmoChanged += RefreshWeapon;
                weapons.InventoryChanged += RefreshWeapon;
                weapons.ReloadStateChanged += HandleReloadStateChanged;
            }

            if (waveManager != null)
            {
                waveManager.WaveStarted += HandleWaveStarted;
                waveManager.WaveCompleted += HandleWaveCompleted;
                waveManager.ReadyForNextWave += HandleReadyForNextWave;
                waveManager.CountsChanged += RefreshWave;
                waveManager.CountdownChanged += HandleCountdownChanged;
            }

            if (gameManager != null)
            {
                gameManager.StateChanged += HandleGameStateChanged;
                gameManager.PauseChanged += HandlePauseChanged;
            }

            if (player != null)
            {
                player.ShieldChanged += HandleShieldChanged;
            }

            if (buffs != null)
            {
                buffs.ChoiceRequested += HandleChoiceRequested;
                buffs.ChoiceResolved += HandleChoiceResolved;
                buffs.BuffsChanged += HandleBuffsChanged;
            }
            if (shop != null)
            {
                shop.ShopToggled += HandleShopToggled;
                shop.NotificationRequested += HandleNotification;
            }

            if (placement != null)
            {
                placement.PlacementStateChanged += HandlePlacementStateChanged;
                placement.SelectionChanged += HandleSelectionChanged;
                placement.ModeChanged += HandleBuildModeChanged;
                placement.BuildingDemolished += HandleBuildingDemolished;
            }

            BossController.BossSpawned += HandleBossSpawned;
            BossController.BossDefeated += HandleBossDefeated;
            InputBindings.Changed += HandleBindingsChanged;
            CheatManager.Notification += HandleCheatNotification;
            bossTarget = BossController.ActiveBoss;
            RefreshBossBar();
            RefreshBuildHint();
        }

        private void RefreshAll()
        {
            RefreshHealth();
            RefreshShield();
            RefreshBuffStats();
            RefreshCoins();
            RefreshWave();
            RefreshWeapon();
            RefreshDash();
            RefreshIntermission();
            RefreshInspection();
            RefreshBossBar();
        }

        private void RefreshHealth()
        {
            if (player == null || healthFill == null)
            {
                return;
            }

            float ratio = Mathf.Clamp01(player.CurrentHealth / Mathf.Max(1f, player.MaxHealth));
            healthFill.fillAmount = ratio;
            healthFill.color = Color.Lerp(UiTheme.HealthLow, UiTheme.Health, Mathf.InverseLerp(0.15f, 0.6f, ratio));
            healthText.text = $"{Mathf.CeilToInt(player.CurrentHealth)} / {Mathf.CeilToInt(player.MaxHealth)}";
        }

        private void RefreshShield()
        {
            if (player == null)
            {
                return;
            }

            bool hasShield = player.MaxShield > 0.01f;
            if (shieldFill != null)
            {
                shieldFill.fillAmount = hasShield ? Mathf.Clamp01(player.Shield / player.MaxShield) : 0f;
            }

            if (shieldText != null)
            {
                shieldText.text = hasShield
                    ? $"ЩИТ {Mathf.CeilToInt(player.Shield)} / {Mathf.CeilToInt(player.MaxShield)}"
                    : "ЩИТ —";
                shieldText.color = hasShield ? ShieldColor : UiTheme.Muted;
            }
        }

        private void RefreshBuffStats()
        {
            if (buffText == null)
            {
                return;
            }

            if (buffs == null)
            {
                buffText.text = string.Empty;
                return;
            }

            StringBuilder builder = new StringBuilder();
            if (buffs.VampirismLevel > 0)
            {
                builder.Append($"ВАМП {buffs.VampirismLevel}   ");
            }

            if (buffs.SpeedLevel > 0)
            {
                builder.Append($"СКОР {buffs.SpeedLevel}   ");
            }

            if (buffs.ShieldLevel > 0)
            {
                builder.Append($"ЩИТ {buffs.ShieldLevel}");
            }

            buffText.text = builder.ToString().TrimEnd();
        }

        private void RefreshCoins()
        {
            if (coinsText != null && currency != null)
            {
                coinsText.text = currency.Balance.ToString("N0");
            }
        }

        private void RefreshWave()
        {
            if (waveManager == null)
            {
                return;
            }

            bool gameOver = gameManager != null && gameManager.State == GameState.GameOver;
            if (gameOver)
            {
                waveTitle.text = "КОНЕЦ ИГРЫ";
                remainingText.text = $"Дошли до волны {Mathf.Max(1, waveManager.CurrentWave)}";
                countdownText.text = "Нажмите «Перезапустить»";
                return;
            }

            if (waveManager.IsWaveActive)
            {
                waveTitle.text = $"ВОЛНА {waveManager.CurrentWave}";
                remainingText.text = $"Осталось зомби: {waveManager.RemainingZombies}";
                countdownText.text = "БОЙ ИДЁТ";
            }
            else
            {
                if (waveManager.CurrentWave <= 0)
                {
                    waveTitle.text = "ПОДГОТОВКА";
                    remainingText.text = "Приготовьтесь к волне 1";
                }
                else
                {
                    waveTitle.text = $"ВОЛНА {waveManager.CurrentWave} ПРОЙДЕНА";
                    remainingText.text = $"Следующая волна: {waveManager.NextWaveNumber}";
                }

                countdownText.text = "Закупитесь и нажмите «Начать волну»";
            }
        }

        /// <summary>
        /// The intermission lasts as long as the player wants, so the start button is
        /// simply shown while WaveManager.AwaitingStart is true.
        /// </summary>
        private void RefreshIntermission()
        {
            if (startWavePanel == null || waveManager == null || gameManager == null)
            {
                return;
            }

            bool awaitingChoice = buffs != null && buffs.AwaitingChoice;
            bool visible = waveManager.AwaitingStart &&
                           gameManager.State != GameState.GameOver &&
                           !gameManager.IsPaused &&
                           !awaitingChoice &&
                           !shopOpen;

            if (startWavePanel.activeSelf != visible)
            {
                startWavePanel.SetActive(visible);
            }

            if (visible && startWaveLabel != null)
            {
                startWaveLabel.text = $"НАЧАТЬ ВОЛНУ {waveManager.NextWaveNumber}";
            }
        }

        private void RefreshDash()
        {
            if (dashFill == null)
            {
                return;
            }

            float ready = playerController != null ? playerController.DashReady01 : 1f;
            bool isReady = ready >= 0.999f;

            dashFill.fillAmount = ready;
            dashFill.color = isReady ? UiTheme.Success : UiTheme.AccentDark;

            if (dashLabel != null)
            {
                dashLabel.text = isReady
                    ? $"<color=#61DB75>{Key(GameAction.Dash)}</color> — РЫВОК ГОТОВ"
                    : "<color=#A85A17>РЫВОК</color> — ПЕРЕЗАРЯДКА";
            }
        }

        private void RefreshWeapon()
        {
            if (weapons == null)
            {
                return;
            }

            WeaponRuntime weapon = weapons.CurrentWeapon;
            if (weapon == null)
            {
                weaponNameText.text = "НЕТ ОРУЖИЯ";
                ammoText.text = "—";
                reloadText.text = string.Empty;
                inventoryText.text = string.Empty;
                return;
            }

            weaponNameText.text = weapon.Data.DisplayName.ToUpperInvariant();
            ammoText.text = $"{weapon.Clip} <size=16><color=#{UiTheme.ColorToHex(UiTheme.Muted)}>/ {weapon.Reserve}</color></size>";
            reloadText.text = weapon.IsReloading
                ? "<color=#FFA33D>ПЕРЕЗАРЯДКА...</color>"
                : $"{Key(GameAction.Reload)} — ПЕРЕЗАРЯДКА";
            reloadText.color = UiTheme.Muted;

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < weapons.OwnedWeapons.Count; i++)
            {
                WeaponRuntime owned = weapons.OwnedWeapons[i];
                if (owned == null)
                {
                    continue;
                }

                string entry = $"{i + 1}. {owned.Data.DisplayName.ToUpperInvariant()}";
                if (i == weapons.CurrentIndex)
                {
                    builder.Append($"<color=#{UiTheme.ColorToHex(UiTheme.Accent)}><b>{entry}</b></color>");
                }
                else
                {
                    builder.Append($"<color=#{UiTheme.ColorToHex(UiTheme.Muted)}>{entry}</color>");
                }

                if (i < weapons.OwnedWeapons.Count - 1)
                {
                    builder.Append("   ");
                }
            }

            inventoryText.text = builder.ToString();
        }

        private void HandleHealthChanged(DamageableBehaviour _, float current, float maximum)
        {
            RefreshHealth();
        }

        private void HandleCoinsChanged(int balance)
        {
            RefreshCoins();
        }

        private void HandleWeaponChanged(WeaponRuntime _)
        {
            RefreshWeapon();
        }

        private void HandleReloadStateChanged(bool _)
        {
            RefreshWeapon();
        }

        private void HandleCountdownChanged(int seconds)
        {
            RefreshWave();
        }

        private void HandleReadyForNextWave(int waveNumber)
        {
            RefreshWave();
            RefreshIntermission();
        }

        private void HandleWaveStarted(int waveNumber)
        {
            RefreshWave();
            RefreshIntermission();
            ShowBanner($"ВОЛНА {waveNumber}");
            AudioService.Instance?.PlayWaveStart();
        }

        private void HandleWaveCompleted(int waveNumber)
        {
            RefreshWave();
            RefreshIntermission();
            ShowBanner($"ВОЛНА {waveNumber} ОЧИЩЕНА");
        }

        private void HandleStartWaveClicked()
        {
            if (waveManager == null || gameManager == null)
            {
                return;
            }

            if (gameManager.IsPaused || (buffs != null && buffs.AwaitingChoice))
            {
                return;
            }

            waveManager.StartNextWave();
        }

        private void ChooseBuff(BuffType type)
        {
            if (buffs == null)
            {
                if (buffPanel != null)
                {
                    buffPanel.SetActive(false);
                }

                return;
            }

            buffs.Choose(type);
        }

        private void HandleChoiceRequested(int waveNumber)
        {
            if (buffPanel != null)
            {
                // Keep the prompt above the shop in case it is open when the last
                // zombie dies; the player should not miss the upgrade.
                buffPanel.transform.SetAsLastSibling();
                buffPanel.SetActive(true);
            }

            if (buffSubtitle != null)
            {
                buffSubtitle.text = $"Волна {waveNumber} пройдена — выберите усиление.";
            }

            if (crosshair != null)
            {
                crosshair.gameObject.SetActive(false);
            }

            RefreshIntermission();
        }

        private void HandleChoiceResolved()
        {
            if (buffPanel != null)
            {
                buffPanel.SetActive(false);
            }

            RefreshBuffStats();
            RefreshShield();
            RefreshIntermission();
            ShowToast("Улучшение получено", true);
        }

        private void HandleBuffsChanged()
        {
            RefreshBuffStats();
            RefreshShield();
        }

        private void HandleShieldChanged(float current, float maximum)
        {
            RefreshShield();
        }

        private void HandlePauseChanged(bool paused)
        {
            if (!paused)
            {
                // Leaving the pause screen must never leave key capture active:
                // otherwise the pause key would stop working on the next frame.
                EndRebind();
            }

            if (pausePanel != null)
            {
                if (paused)
                {
                    pausePanel.transform.SetAsLastSibling();
                }

                pausePanel.SetActive(paused);
            }

            if (paused && crosshair != null)
            {
                crosshair.gameObject.SetActive(false);
            }

            RefreshIntermission();
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.GameOver)
            {
                gameOverStatsText.text = $"Волна: {Mathf.Max(1, waveManager.CurrentWave)}\nУбито зомби: {gameManager.Kills}";
                gameOverPanel.SetActive(true);
                crosshair.gameObject.SetActive(false);

                if (startWavePanel != null) startWavePanel.SetActive(false);
                if (buffPanel != null) buffPanel.SetActive(false);
                if (pausePanel != null) pausePanel.SetActive(false);
                if (inspectPanel != null) inspectPanel.SetActive(false);
                if (bossPanel != null) bossPanel.SetActive(false);
            }
            else
            {
                gameOverPanel.SetActive(false);
            }

            RefreshWave();
        }

        private void HandleShopToggled(bool isOpen)
        {
            shopOpen = isOpen;
            if (crosshair != null)
            {
                crosshair.gameObject.SetActive(
                    !isOpen &&
                    gameManager != null &&
                    gameManager.State != GameState.GameOver &&
                    !gameManager.IsPaused);
            }

            RefreshIntermission();
        }

        private void HandlePlacementStateChanged(bool isPlacing)
        {
            RefreshBuildHint();
        }

        private void HandleBuildModeChanged(BuildInteractionMode mode)
        {
            RefreshBuildHint();
        }

        private void RefreshBuildHint()
        {
            if (placement == null)
            {
                return;
            }

            if (buildHintPanel == null)
            {
                return;
            }

            bool visible = placement.Mode != BuildInteractionMode.None;
            buildHintPanel.SetActive(visible);
            if (!visible)
            {
                return;
            }

            switch (placement.Mode)
            {
                case BuildInteractionMode.Placing:
                    buildHintText.text = placement.PendingData != null
                        ? $"РАЗМЕЩЕНИЕ: {placement.PendingData.DisplayName.ToUpperInvariant()}   •   ЛКМ — поставить   •   ПКМ / ESC — отмена"
                        : "ЛКМ — поставить   •   ПКМ / ESC — отмена";
                    buildHintText.color = UiTheme.Success;
                    break;
                case BuildInteractionMode.Demolishing:
                    buildHintText.text = $"РЕЖИМ СНОСА: ЛКМ — снести постройку   •   {Key(GameAction.Demolish)} / ESC — выход";
                    buildHintText.color = UiTheme.Danger;
                    break;
                case BuildInteractionMode.Inspecting:
                    buildHintText.text = $"ПОСТРОЙКА ВЫБРАНА   •   {Key(GameAction.Interact)} / ESC — закрыть панель";
                    buildHintText.color = UiTheme.Accent;
                    break;
            }
        }

        private void HandleSelectionChanged(BuildingBase building)
        {
            if (inspectedBuilding != null)
            {
                inspectedBuilding.Upgraded -= HandleInspectedUpgraded;
                inspectedBuilding.HealthChanged -= HandleInspectedHealthChanged;
                inspectedBuilding.Removed -= HandleInspectedRemoved;
            }

            inspectedBuilding = building;
            if (inspectedBuilding != null)
            {
                inspectedBuilding.Upgraded += HandleInspectedUpgraded;
                inspectedBuilding.HealthChanged += HandleInspectedHealthChanged;
                inspectedBuilding.Removed += HandleInspectedRemoved;
            }

            if (inspectPanel != null)
            {
                inspectPanel.SetActive(inspectedBuilding != null);
            }

            RefreshInspection();
            RefreshBuildHint();
        }

        private void HandleInspectedUpgraded(BuildingBase building)
        {
            RefreshInspection();
        }

        private void HandleInspectedHealthChanged(DamageableBehaviour building, float current, float maximum)
        {
            RefreshInspection();
        }

        private void HandleInspectedRemoved(BuildingBase building, int refund)
        {
            HandleSelectionChanged(null);
        }

        private void HandleBuildingDemolished(BuildingBase building, int refund)
        {
            if (building == inspectedBuilding)
            {
                HandleSelectionChanged(null);
            }
        }

        private void HandleUpgradeClicked()
        {
            if (placement == null)
            {
                return;
            }

            placement.UpgradeSelected();
            RefreshInspection();
        }

        private void RefreshInspection()
        {
            if (inspectPanel == null)
            {
                return;
            }

            BuildingBase building = inspectedBuilding;
            if (building == null || !building.IsAlive || building.Data == null)
            {
                if (inspectPanel.activeSelf)
                {
                    inspectPanel.SetActive(false);
                }

                return;
            }

            if (!inspectPanel.activeSelf)
            {
                inspectPanel.SetActive(true);
            }

            BuildingDataSO data = building.Data;
            int level = Mathf.Max(1, building.Level);
            inspectTitle.text = data.DisplayName.ToUpperInvariant();
            inspectLevel.text = building.IsMaxLevel
                ? $"УРОВЕНЬ {level} / {data.MaxLevel}   •   МАКСИМУМ"
                : $"УРОВЕНЬ {level} / {data.MaxLevel}";

            StringBuilder stats = new StringBuilder();
            switch (data.BuildingType)
            {
                case BuildingType.Turret:
                    stats.Append($"УРОН: {data.GetTurretDamage(level):0.#}\n");
                    stats.Append($"ТЕМП: {data.GetTurretFireRate(level):0.##}/с\n");
                    stats.Append($"РАДИУС: {data.GetTurretRange(level):0.#}\n");
                    break;
                case BuildingType.MoneyGenerator:
                    stats.Append($"МОНЕТ ЗА СБОР: {data.GetGeneratorCoinAmount(level)}\n");
                    stats.Append($"ИНТЕРВАЛ: {data.GeneratorInterval:0.#} с\n");
                    break;
                case BuildingType.RepairStation:
                    stats.Append($"РЕМОНТ: {data.GetRepairPerSecond(level):0.#} HP/с\n");
                    stats.Append($"РАДИУС: {data.GetRepairRadius(level):0.#}\n");
                    break;
                case BuildingType.Wall:
                    stats.Append("БЛОКИРУЕТ ЗОМБИ\n");
                    stats.Append("ПРИКРЫВАЕТ ТУРЕЛИ\n");
                    break;
            }

            stats.Append($"ПРОЧНОСТЬ: {Mathf.CeilToInt(building.CurrentHealth)} / {Mathf.CeilToInt(building.MaxHealth)}\n");
            stats.Append($"ВЛОЖЕНО: {building.InvestedCoins} монет");
            inspectStats.text = stats.ToString();

            if (inspectUpgradeButton != null && inspectUpgradeLabel != null)
            {
                bool maxed = building.IsMaxLevel;
                inspectUpgradeButton.interactable = !maxed;
                inspectUpgradeLabel.text = maxed ? "МАКС. УРОВЕНЬ" : $"УЛУЧШИТЬ   {building.NextUpgradeCost}";
            }

            if (inspectSellLabel != null)
            {
                inspectSellLabel.text = $"СНЕСТИ   +{building.SellValue}";
            }
        }

        private void HandleBossSpawned(BossController boss)
        {
            bossTarget = boss;
            if (bossPanel != null)
            {
                bossPanel.SetActive(true);
            }

            ShowBanner($"БОСС: {boss.DisplayName}");
            RefreshBossBar();
        }

        private void HandleBossDefeated(BossController boss)
        {
            if (bossTarget == boss)
            {
                bossTarget = null;
            }

            if (bossPanel != null)
            {
                bossPanel.SetActive(false);
            }

            ShowBanner("БОСС ПОВЕРЖЕН");
        }

        private void RefreshBossBar()
        {
            if (bossPanel == null)
            {
                return;
            }

            if (bossTarget == null || !bossTarget.IsAlive || (gameManager != null && gameManager.State == GameState.GameOver))
            {
                if (bossPanel.activeSelf)
                {
                    bossPanel.SetActive(false);
                }

                return;
            }

            if (!bossPanel.activeSelf)
            {
                bossPanel.SetActive(true);
            }

            bossNameText.text = bossTarget.DisplayName;
            bossPhaseText.text = $"ФАЗА {bossTarget.Phase}   •   {Mathf.CeilToInt(bossTarget.CurrentHealth)} / {Mathf.CeilToInt(bossTarget.MaxHealth)}";
            bossFill.fillAmount = bossTarget.HealthFraction;

            Color low = new Color(1f, 0.36f, 0.25f, 1f);
            bossFill.color = Color.Lerp(low, UiTheme.Health, Mathf.InverseLerp(0.15f, 0.5f, bossTarget.HealthFraction));
        }

        private void HandleNotification(string message, bool success)
        {
            if (shopOpen)
            {
                return;
            }

            ShowToast(message, success);
        }

        private void HandleCheatNotification(string message, bool success)
        {
            ShowToast(message, success);
        }

        private void ShowToast(string message, bool success)
        {
            if (toastRoutine != null)
            {
                StopCoroutine(toastRoutine);
            }

            toastText.text = message;
            toastText.color = success ? UiTheme.Success : UiTheme.Danger;
            toastRoutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            const float fade = 0.16f;
            const float hold = 1.6f;
            toastGroup.alpha = 0f;
            float timer = 0f;
            while (timer < fade)
            {
                timer += Time.unscaledDeltaTime;
                toastGroup.alpha = Mathf.Clamp01(timer / fade);
                yield return null;
            }

            toastGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(hold);

            timer = 0f;
            while (timer < fade)
            {
                timer += Time.unscaledDeltaTime;
                toastGroup.alpha = 1f - Mathf.Clamp01(timer / fade);
                yield return null;
            }

            toastGroup.alpha = 0f;
            toastRoutine = null;
        }

        private void ShowBanner(string text)
        {
            if (bannerRoutine != null)
            {
                StopCoroutine(bannerRoutine);
            }

            bannerText.text = text;
            bannerRoutine = StartCoroutine(BannerRoutine());
        }

        private IEnumerator BannerRoutine()
        {
            const float fadeIn = 0.2f;
            const float hold = 1.3f;
            const float fadeOut = 0.5f;

            bannerGroup.alpha = 0f;
            float timer = 0f;
            while (timer < fadeIn)
            {
                timer += Time.unscaledDeltaTime;
                bannerGroup.alpha = Mathf.Clamp01(timer / fadeIn);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(hold);

            timer = 0f;
            while (timer < fadeOut)
            {
                timer += Time.unscaledDeltaTime;
                bannerGroup.alpha = 1f - Mathf.Clamp01(timer / fadeOut);
                yield return null;
            }

            bannerGroup.alpha = 0f;
            bannerRoutine = null;
        }

        private void OnDestroy()
        {
            if (player != null) player.HealthChanged -= HandleHealthChanged;
            if (currency != null) currency.CoinsChanged -= HandleCoinsChanged;
            if (weapons != null)
            {
                weapons.WeaponChanged -= HandleWeaponChanged;
                weapons.AmmoChanged -= RefreshWeapon;
                weapons.InventoryChanged -= RefreshWeapon;
                weapons.ReloadStateChanged -= HandleReloadStateChanged;
            }

            if (waveManager != null)
            {
                waveManager.WaveStarted -= HandleWaveStarted;
                waveManager.WaveCompleted -= HandleWaveCompleted;
                waveManager.ReadyForNextWave -= HandleReadyForNextWave;
                waveManager.CountsChanged -= RefreshWave;
                waveManager.CountdownChanged -= HandleCountdownChanged;
            }

            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleGameStateChanged;
                gameManager.PauseChanged -= HandlePauseChanged;
            }

            if (player != null)
            {
                player.ShieldChanged -= HandleShieldChanged;
            }

            if (buffs != null)
            {
                buffs.ChoiceRequested -= HandleChoiceRequested;
                buffs.ChoiceResolved -= HandleChoiceResolved;
                buffs.BuffsChanged -= HandleBuffsChanged;
            }
            if (shop != null)
            {
                shop.ShopToggled -= HandleShopToggled;
                shop.NotificationRequested -= HandleNotification;
            }

            if (placement != null)
            {
                placement.PlacementStateChanged -= HandlePlacementStateChanged;
                placement.SelectionChanged -= HandleSelectionChanged;
                placement.ModeChanged -= HandleBuildModeChanged;
                placement.BuildingDemolished -= HandleBuildingDemolished;
            }

            if (inspectedBuilding != null)
            {
                inspectedBuilding.Upgraded -= HandleInspectedUpgraded;
                inspectedBuilding.HealthChanged -= HandleInspectedHealthChanged;
                inspectedBuilding.Removed -= HandleInspectedRemoved;
            }

            BossController.BossSpawned -= HandleBossSpawned;
            BossController.BossDefeated -= HandleBossDefeated;
            InputBindings.Changed -= HandleBindingsChanged;
            CheatManager.Notification -= HandleCheatNotification;

            if (pendingRebind.HasValue)
            {
                pendingRebind = null;
                InputBindings.EndCapture();
            }
        }
    }
}
