using System;
using System.Collections.Generic;
using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Waves;
using UnityEngine;

namespace PixelZombieSurvivor.Building
{
    /// <summary>
    /// Integer cell grid shared by placement, demolition, building targeting and
    /// upgrades. Cells are 1 world unit by default and every deployable occupies
    /// exactly one cell, so blocks always sit flush against each other.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public sealed class GridManager : SingletonBehaviour<GridManager>
    {
        [Header("Grid")]
        [SerializeField, Min(0.25f)] private float cellSize = 1f;
        [SerializeField] private Vector2Int fullMinCell = new Vector2Int(-19, -11);
        [SerializeField] private Vector2Int fullMaxCell = new Vector2Int(18, 10);

        [Header("Build Zone")]
        // The whole arena is buildable from wave 1. The central zone fields are kept
        // for compatibility with older scenes and manual SetExpanded(false) calls.
        [SerializeField] private Vector2Int centralMinCell = new Vector2Int(-19, -11);
        [SerializeField] private Vector2Int centralMaxCell = new Vector2Int(18, 10);
        [SerializeField, Min(1)] private int expansionWave = 1;

        [Header("Visuals")]
        [SerializeField] private Color gridColor = new Color(0.55f, 0.85f, 1f, 0.10f);
        [SerializeField] private Color territoryColor = new Color(1f, 0.72f, 0.28f, 0.45f);
        [SerializeField] private int gridSortingOrder = 1;

        private readonly Dictionary<Vector2Int, BuildingBase> occupied = new Dictionary<Vector2Int, BuildingBase>();
        private SpriteRenderer gridVisual;
        private SpriteRenderer territoryVisual;
        private WaveManager waves;

        public event Action<bool> TerritoryChanged;

        public float CellSize => cellSize;

        /// <summary>The entire arena is available for construction from the start.</summary>
        public bool IsExpanded { get; private set; } = true;
        public int ExpansionWave => expansionWave;
        public Vector2Int CentralMinCell => centralMinCell;
        public Vector2Int CentralMaxCell => centralMaxCell;
        public IReadOnlyDictionary<Vector2Int, BuildingBase> Occupied => occupied;

        protected override void Awake()
        {
            base.Awake();
            BuildVisuals();
        }

        private void Start()
        {
            waves = GameManager.Instance != null ? GameManager.Instance.WaveManager : FindAnyObjectByType<WaveManager>();
            if (waves != null)
            {
                waves.WaveCompleted += HandleWaveCompleted;
            }

            SetVisible(false);
        }

        public Vector2Int WorldToCell(Vector2 worldPosition)
        {
            float size = Mathf.Max(0.25f, cellSize);
            return new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / size),
                Mathf.FloorToInt(worldPosition.y / size));
        }

        public Vector3 CellToWorld(Vector2Int cell)
        {
            float size = Mathf.Max(0.25f, cellSize);
            return new Vector3((cell.x + 0.5f) * size, (cell.y + 0.5f) * size, 0f);
        }

        /// <summary>
        /// Keeps a world position inside the playable arena (inside the fence walls).
        /// Used by enemy spawning so minions summoned at the edge never end up
        /// behind the map, and as a safety net for movement.
        /// </summary>
        public Vector3 ClampToArena(Vector3 position, float margin = 0.45f)
        {
            float size = Mathf.Max(0.25f, cellSize);
            float minX = fullMinCell.x * size + margin;
            float maxX = (fullMaxCell.x + 1) * size - margin;
            float minY = fullMinCell.y * size + margin;
            float maxY = (fullMaxCell.y + 1) * size - margin;

            return new Vector3(
                Mathf.Clamp(position.x, minX, maxX),
                Mathf.Clamp(position.y, minY, maxY),
                position.z);
        }

        public bool IsInsideBuildArea(Vector2Int cell)
        {
            return IsExpanded ? IsInsideFullArena(cell) : IsInsideRange(cell, centralMinCell, centralMaxCell);
        }

        public bool IsInsideFullArena(Vector2Int cell)
        {
            return IsInsideRange(cell, fullMinCell, fullMaxCell);
        }

        public bool IsOccupied(Vector2Int cell)
        {
            BuildingBase building;
            if (!occupied.TryGetValue(cell, out building))
            {
                return false;
            }

            if (building == null || !building.IsAlive)
            {
                occupied.Remove(cell);
                return false;
            }

            return true;
        }

        public BuildingBase GetBuilding(Vector2Int cell)
        {
            BuildingBase building;
            if (!occupied.TryGetValue(cell, out building))
            {
                return null;
            }

            if (building == null || !building.IsAlive)
            {
                occupied.Remove(cell);
                return null;
            }

            return building;
        }

        public bool TryOccupy(Vector2Int cell, BuildingBase building)
        {
            if (building == null || !IsInsideBuildArea(cell) || IsOccupied(cell))
            {
                return false;
            }

            occupied[cell] = building;
            building.AssignCell(cell);
            return true;
        }

        public void Release(Vector2Int cell, BuildingBase expected = null)
        {
            BuildingBase building;
            if (!occupied.TryGetValue(cell, out building))
            {
                return;
            }

            if (expected != null && building != expected)
            {
                return;
            }

            occupied.Remove(cell);
        }

        public void ReleaseBuilding(BuildingBase building)
        {
            if (building == null)
            {
                return;
            }

            Release(building.Cell, building);
        }

        public void ExpandTerritory()
        {
            if (IsExpanded)
            {
                return;
            }

            IsExpanded = true;
            RefreshTerritoryVisual();
            TerritoryChanged?.Invoke(true);
        }

        public void SetExpanded(bool expanded)
        {
            if (IsExpanded == expanded)
            {
                return;
            }

            IsExpanded = expanded;
            RefreshTerritoryVisual();
            TerritoryChanged?.Invoke(IsExpanded);
        }

        public void SetVisible(bool visible)
        {
            if (gridVisual != null)
            {
                gridVisual.enabled = visible;
            }

            if (territoryVisual != null)
            {
                territoryVisual.enabled = visible;
            }
        }

        public Vector3 GetBuildAreaCenter()
        {
            Vector2Int min = IsExpanded ? fullMinCell : centralMinCell;
            Vector2Int max = IsExpanded ? fullMaxCell : centralMaxCell;
            return new Vector3(
                (min.x + max.x + 1) * 0.5f * cellSize,
                (min.y + max.y + 1) * 0.5f * cellSize,
                0f);
        }

        private void HandleWaveCompleted(int waveNumber)
        {
            if (waveNumber >= expansionWave)
            {
                ExpandTerritory();
            }
        }

        private static bool IsInsideRange(Vector2Int cell, Vector2Int min, Vector2Int max)
        {
            return cell.x >= min.x && cell.x <= max.x && cell.y >= min.y && cell.y <= max.y;
        }

        private void BuildVisuals()
        {
            GameObject gridObject = new GameObject("Grid Overlay");
            gridObject.transform.SetParent(transform, false);
            gridVisual = gridObject.AddComponent<SpriteRenderer>();
            gridVisual.sprite = CreateOutlineSprite(gridColor);
            gridVisual.drawMode = SpriteDrawMode.Tiled;
            gridVisual.tileMode = SpriteTileMode.Continuous;
            gridVisual.size = new Vector2(
                (fullMaxCell.x - fullMinCell.x + 1) * cellSize,
                (fullMaxCell.y - fullMinCell.y + 1) * cellSize);
            gridVisual.transform.position = new Vector3(
                (fullMinCell.x + fullMaxCell.x + 1) * 0.5f * cellSize,
                (fullMinCell.y + fullMaxCell.y + 1) * 0.5f * cellSize,
                0f);
            gridVisual.sortingOrder = gridSortingOrder;

            GameObject territoryObject = new GameObject("Build Zone");
            territoryObject.transform.SetParent(transform, false);
            territoryVisual = territoryObject.AddComponent<SpriteRenderer>();
            territoryVisual.sprite = CreateOutlineSprite(territoryColor);
            territoryVisual.drawMode = SpriteDrawMode.Tiled;
            territoryVisual.tileMode = SpriteTileMode.Continuous;
            territoryVisual.sortingOrder = gridSortingOrder + 1;
            RefreshTerritoryVisual();
        }

        private void RefreshTerritoryVisual()
        {
            if (territoryVisual == null)
            {
                return;
            }

            Vector2Int min = IsExpanded ? fullMinCell : centralMinCell;
            Vector2Int max = IsExpanded ? fullMaxCell : centralMaxCell;
            territoryVisual.size = new Vector2(
                (max.x - min.x + 1) * cellSize,
                (max.y - min.y + 1) * cellSize);
            territoryVisual.transform.position = new Vector3(
                (min.x + max.x + 1) * 0.5f * cellSize,
                (min.y + max.y + 1) * 0.5f * cellSize,
                0f);
        }

        private static Sprite CreateOutlineSprite(Color color)
        {
            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Grid Cell",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool border = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    texture.SetPixel(x, y, border ? color : Color.clear);
                }
            }

            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "Runtime Grid Cell";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        protected override void OnDestroy()
        {
            if (waves != null)
            {
                waves.WaveCompleted -= HandleWaveCompleted;
            }

            base.OnDestroy();
        }
    }
}
