using System;
using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Data;
using PixelZombieSurvivor.Economy;
using PixelZombieSurvivor.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PixelZombieSurvivor.Building
{
    /// <summary>What the build cursor is currently doing.</summary>
    public enum BuildInteractionMode
    {
        None = 0,
        Placing = 1,
        Demolishing = 2,
        Inspecting = 3
    }

    /// <summary>
    /// Grid-snapped placement, demolition and building inspection. Money is only
    /// charged when a valid cell is confirmed, so cancelling never needs a refund.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class BuildingPlacementController : SingletonBehaviour<BuildingPlacementController>
    {
        [Header("References")]
        [SerializeField] private Transform buildingParent;
        [SerializeField] private GridManager grid;

        [Header("Feedback Colours")]
        [SerializeField] private Color validColor = new Color(0.35f, 1f, 0.55f, 0.55f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.28f, 0.28f, 0.55f);
        [SerializeField] private Color demolishColor = new Color(1f, 0.35f, 0.2f, 0.75f);
        [SerializeField] private Color inspectColor = new Color(0.4f, 0.85f, 1f, 0.8f);

        private CurrencyManager currency;
        private PlayerHealth player;
        private Camera mainCamera;

        private BuildingDataSO pendingData;
        private GameObject ghost;
        private SpriteRenderer ghostRenderer;
        private SpriteRenderer highlight;
        private bool placementValid;
        private float blockClicksUntil;

        private BuildInteractionMode mode = BuildInteractionMode.None;
        private BuildingBase hovered;
        private BuildingBase selected;

        public event Action<bool> PlacementStateChanged;
        public event Action<BuildingDataSO, BuildingBase> BuildingPlaced;
        public event Action<string> PlacementMessage;
        public event Action<BuildInteractionMode> ModeChanged;
        public event Action<BuildingBase> SelectionChanged;
        public event Action<BuildingBase, int> BuildingDemolished;
        public event Action<bool> InspectionChanged;

        public bool IsPlacing => pendingData != null;
        public bool IsDemolishing => mode == BuildInteractionMode.Demolishing;
        public bool IsInspecting => selected != null;
        public bool IsBusy => pendingData != null || mode == BuildInteractionMode.Demolishing || selected != null;
        public BuildInteractionMode Mode => mode;
        public BuildingDataSO PendingData => pendingData;
        public BuildingBase HoveredBuilding => hovered;
        public BuildingBase SelectedBuilding => selected;
        public GridManager Grid => grid;

        protected override void Awake()
        {
            base.Awake();
        }

        private void Start()
        {
            currency = CurrencyManager.Instance;
            player = FindAnyObjectByType<PlayerHealth>();
            mainCamera = Camera.main;

            if (grid == null)
            {
                grid = GridManager.Instance != null ? GridManager.Instance : FindAnyObjectByType<GridManager>();
            }

            if (grid == null)
            {
                Debug.LogError("BuildingPlacementController requires a GridManager in the scene.", this);
            }

            if (buildingParent == null)
            {
                GameObject root = new GameObject("[Buildings]");
                buildingParent = root.transform;
            }

            CreateHighlight();
        }

        public bool BeginPlacement(BuildingDataSO data)
        {
            if (data == null || data.Prefab == null)
            {
                PlacementMessage?.Invoke("У этой постройки нет prefab.");
                return false;
            }

            if (grid == null)
            {
                PlacementMessage?.Invoke("Сетка строительства не найдена.");
                return false;
            }

            if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
            {
                return false;
            }

            if (currency == null)
            {
                currency = CurrencyManager.Instance;
            }

            if (currency == null || currency.Balance < data.Price)
            {
                PlacementMessage?.Invoke($"Не хватает монет: нужно {data.Price}.");
                return false;
            }

            CloseSelection();
            if (mode == BuildInteractionMode.Demolishing)
            {
                SetMode(BuildInteractionMode.None);
            }

            DestroyGhost();
            pendingData = data;
            ghost = CreateGhost(data);
            blockClicksUntil = Time.unscaledTime + 0.18f;
            placementValid = false;
            SetMode(BuildInteractionMode.Placing);
            RefreshGridVisibility();
            PlacementStateChanged?.Invoke(true);
            return true;
        }

        public void CancelPlacement()
        {
            bool wasPlacing = pendingData != null;
            DestroyGhost();
            pendingData = null;
            placementValid = false;

            if (mode == BuildInteractionMode.Placing)
            {
                SetMode(BuildInteractionMode.None);
            }

            RefreshGridVisibility();
            if (wasPlacing)
            {
                PlacementStateChanged?.Invoke(false);
            }
        }

        public void ToggleDemolishMode()
        {
            if (mode == BuildInteractionMode.Demolishing)
            {
                ExitDemolishMode();
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
            {
                return;
            }

            CancelPlacement();
            CloseSelection();
            SetMode(BuildInteractionMode.Demolishing);
            hovered = null;
            RefreshGridVisibility();
            PlacementMessage?.Invoke(
                $"Режим сноса: наведите на постройку и нажмите ЛКМ. " +
                $"{InputBindings.GetKeyLabel(GameAction.Demolish)} / ESC — выход.");
        }

        public void ExitDemolishMode()
        {
            hovered = null;
            if (mode == BuildInteractionMode.Demolishing)
            {
                SetMode(BuildInteractionMode.None);
            }

            HideHighlight();
            RefreshGridVisibility();
        }

        public bool SelectBuilding(BuildingBase building)
        {
            if (building == null || !building.IsAlive)
            {
                return false;
            }

            CancelPlacement();
            if (mode == BuildInteractionMode.Demolishing)
            {
                SetMode(BuildInteractionMode.None);
            }

            if (selected == building)
            {
                return true;
            }

            selected = building;
            SetMode(BuildInteractionMode.Inspecting);
            RefreshGridVisibility();
            SelectionChanged?.Invoke(selected);
            InspectionChanged?.Invoke(true);
            ShowHighlight(building.Cell, inspectColor);
            return true;
        }

        public void CloseSelection()
        {
            if (selected == null)
            {
                if (mode == BuildInteractionMode.Inspecting)
                {
                    SetMode(BuildInteractionMode.None);
                }

                return;
            }

            selected = null;
            if (mode == BuildInteractionMode.Inspecting)
            {
                SetMode(BuildInteractionMode.None);
            }

            HideHighlight();
            RefreshGridVisibility();
            SelectionChanged?.Invoke(null);
            InspectionChanged?.Invoke(false);
        }

        public bool UpgradeSelected()
        {
            if (selected == null || !selected.IsAlive)
            {
                return false;
            }

            if (selected.IsMaxLevel)
            {
                PlacementMessage?.Invoke("Достигнут максимальный уровень.");
                return false;
            }

            if (!selected.TryUpgrade())
            {
                PlacementMessage?.Invoke($"Не хватает монет: нужно {selected.NextUpgradeCost}.");
                return false;
            }

            return true;
        }

        public bool DemolishSelected()
        {
            if (selected == null)
            {
                return false;
            }

            Demolish(selected);
            return true;
        }

        public void Demolish(BuildingBase building)
        {
            if (building == null || !building.IsAlive)
            {
                return;
            }

            int refund = building.SellValue;
            GridManager.Instance?.ReleaseBuilding(building);
            building.Demolish(true);

            if (hovered == building)
            {
                hovered = null;
            }

            if (selected == building)
            {
                CloseSelection();
            }

            BuildingDemolished?.Invoke(building, refund);
            AudioService.Instance?.PlayCoin();
        }

        public void CancelAll()
        {
            CancelPlacement();
            ExitDemolishMode();
            CloseSelection();
        }

        public bool CanPlaceAt(Vector2Int cell, out string reason)
        {
            if (grid == null)
            {
                reason = "Сетка строительства не найдена.";
                return false;
            }

            if (!grid.IsInsideFullArena(cell))
            {
                reason = "За пределами арены.";
                return false;
            }

            if (!grid.IsInsideBuildArea(cell))
            {
                reason = "Здесь нельзя строить.";
                return false;
            }

            if (grid.IsOccupied(cell))
            {
                reason = "Ячейка занята.";
                return false;
            }

            if (player == null)
            {
                player = FindAnyObjectByType<PlayerHealth>();
            }

            if (player != null && pendingData != null)
            {
                Vector3 center = grid.CellToWorld(cell);
                float minDistance = pendingData.PlacementDistanceFromPlayer;
                if (((Vector2)center - (Vector2)player.transform.position).sqrMagnitude < minDistance * minDistance)
                {
                    reason = "Слишком близко к игроку.";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        private void Update()
        {
            if (grid == null)
            {
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
            {
                if (IsBusy)
                {
                    CancelAll();
                }

                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            {
                return;
            }

            Vector3 pointer = GetPointerWorldPosition();
            Vector2Int hoverCell = grid.WorldToCell(pointer);
            BuildingBase hoverBuilding = grid.GetBuilding(hoverCell);
            hovered = hoverBuilding;

            HandleHotkeys(hoverBuilding);

            switch (mode)
            {
                case BuildInteractionMode.Placing:
                    UpdatePlacing(hoverCell);
                    break;
                case BuildInteractionMode.Demolishing:
                    UpdateDemolishing(hoverCell, hoverBuilding);
                    break;
                case BuildInteractionMode.Inspecting:
                    if (selected != null && selected.IsAlive)
                    {
                        ShowHighlight(selected.Cell, inspectColor);
                    }
                    else if (selected != null)
                    {
                        CloseSelection();
                    }

                    break;
                default:
                    HideHighlight();
                    break;
            }
        }

        private void HandleHotkeys(BuildingBase hoverBuilding)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                if (mode != BuildInteractionMode.None)
                {
                    if (mode == BuildInteractionMode.Inspecting)
                    {
                        CloseSelection();
                    }
                    else
                    {
                        CancelAll();
                    }
                }

                return;
            }

            if (InputBindings.IsDown(GameAction.Demolish) && (ShopManager.Instance == null || !ShopManager.Instance.IsOpen))
            {
                ToggleDemolishMode();
                return;
            }

            if (InputBindings.IsDown(GameAction.Interact) && mode == BuildInteractionMode.Inspecting)
            {
                CloseSelection();
                return;
            }

            if (InputBindings.IsDown(GameAction.Interact) && mode == BuildInteractionMode.None && hoverBuilding != null)
            {
                SelectBuilding(hoverBuilding);
            }
        }

        private void UpdatePlacing(Vector2Int hoverCell)
        {
            Vector3 target = grid.CellToWorld(hoverCell);
            if (ghost != null)
            {
                ghost.transform.position = target;
            }

            placementValid = CanPlaceAt(hoverCell, out string reason);
            if (ghostRenderer != null)
            {
                ghostRenderer.color = placementValid ? validColor : invalidColor;
            }

            ShowHighlight(hoverCell, placementValid ? validColor : invalidColor);

            if (!Input.GetMouseButtonDown(0) || Time.unscaledTime < blockClicksUntil)
            {
                return;
            }

            if (IsPointerOverUi())
            {
                return;
            }

            if (!placementValid)
            {
                PlacementMessage?.Invoke(reason);
                return;
            }

            PlaceAtCell(hoverCell);
        }

        private void UpdateDemolishing(Vector2Int hoverCell, BuildingBase hoverBuilding)
        {
            if (hoverBuilding != null)
            {
                ShowHighlight(hoverBuilding.Cell, demolishColor);
            }
            else
            {
                ShowHighlight(hoverCell, new Color(demolishColor.r, demolishColor.g, demolishColor.b, 0.18f));
            }

            if (!Input.GetMouseButtonDown(0) || IsPointerOverUi())
            {
                return;
            }

            if (hoverBuilding == null)
            {
                PlacementMessage?.Invoke("Здесь нет постройки.");
                return;
            }

            Demolish(hoverBuilding);
        }

        private void PlaceAt(Vector3 position)
        {
            if (grid != null)
            {
                PlaceAtCell(grid.WorldToCell(position));
            }
        }

        private bool PlaceAtCell(Vector2Int cell)
        {
            if (pendingData == null)
            {
                return false;
            }

            if (!CanPlaceAt(cell, out string reason))
            {
                PlacementMessage?.Invoke(string.IsNullOrEmpty(reason) ? "Здесь нельзя строить." : reason);
                return false;
            }

            if (currency == null || !currency.TrySpend(pendingData.Price))
            {
                PlacementMessage?.Invoke("Не хватает монет.");
                CancelPlacement();
                return false;
            }

            Vector3 position = grid.CellToWorld(cell);
            GameObject instance = Instantiate(pendingData.Prefab, position, Quaternion.identity, buildingParent);
            BuildingBase building = instance.GetComponentInChildren<BuildingBase>();
            if (building == null)
            {
                Debug.LogError("Building prefab is missing BuildingBase.", pendingData.Prefab);
                currency.Add(pendingData.Price);
                Destroy(instance);
                CancelPlacement();
                return false;
            }

            building.Initialize(pendingData);
            if (!grid.TryOccupy(cell, building))
            {
                currency.Add(pendingData.Price);
                Destroy(instance);
                PlacementMessage?.Invoke("Ячейка уже занята.");
                CancelPlacement();
                return false;
            }

            BuildingDataSO builtData = pendingData;
            BuildingPlaced?.Invoke(builtData, building);

            DestroyGhost();
            pendingData = null;
            placementValid = false;
            SetMode(BuildInteractionMode.None);
            RefreshGridVisibility();
            PlacementStateChanged?.Invoke(false);
            return true;
        }

        private Vector3 GetPointerWorldPosition()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            Vector3 screen = Input.mousePosition;
            screen.z = Mathf.Abs(mainCamera != null ? mainCamera.transform.position.z : 10f);
            return mainCamera != null ? mainCamera.ScreenToWorldPoint(screen) : Vector3.zero;
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private GameObject CreateGhost(BuildingDataSO data)
        {
            GameObject instance = new GameObject($"Ghost_{data.DisplayName}");
            SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = data.Icon != null ? data.Icon : RuntimeSprites.White;
            renderer.color = invalidColor;
            renderer.sortingOrder = 60;
            ghostRenderer = renderer;
            return instance;
        }

        private void CreateHighlight()
        {
            GameObject instance = new GameObject("Cell Highlight");
            highlight = instance.AddComponent<SpriteRenderer>();
            highlight.sprite = RuntimeSprites.White;
            highlight.color = validColor;
            highlight.sortingOrder = 59;
            float size = grid != null ? grid.CellSize : 1f;
            instance.transform.localScale = new Vector3(size * 0.94f, size * 0.94f, 1f);
            highlight.enabled = false;
        }

        private void ShowHighlight(Vector2Int cell, Color color)
        {
            if (highlight == null || grid == null)
            {
                return;
            }

            highlight.transform.position = grid.CellToWorld(cell);
            highlight.color = color;
            highlight.enabled = true;
        }

        private void HideHighlight()
        {
            if (highlight != null)
            {
                highlight.enabled = false;
            }
        }

        private void RefreshGridVisibility()
        {
            if (grid != null)
            {
                grid.SetVisible(mode == BuildInteractionMode.Placing || mode == BuildInteractionMode.Demolishing);
            }
        }

        private void SetMode(BuildInteractionMode newMode)
        {
            if (mode == newMode)
            {
                return;
            }

            mode = newMode;
            ModeChanged?.Invoke(mode);
        }

        private void DestroyGhost()
        {
            if (ghost != null)
            {
                Destroy(ghost);
            }

            ghost = null;
            ghostRenderer = null;
        }

        protected override void OnDestroy()
        {
            DestroyGhost();
            if (highlight != null)
            {
                Destroy(highlight.gameObject);
            }

            base.OnDestroy();
        }
    }
}
