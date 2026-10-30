using PixelZombieSurvivor.Core;
using UnityEngine;

namespace PixelZombieSurvivor.Building
{
    /// <summary>
    /// Minimal health bar built from two sprite quads. It does not need a Canvas,
    /// which keeps many buildings cheap in the world.
    /// </summary>
    public sealed class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] private Vector2 barSize = new Vector2(1.15f, 0.12f);
        [SerializeField] private Vector3 offset = new Vector3(0f, 0.78f, 0f);
        [SerializeField] private Color fillColor = new Color(0.36f, 0.86f, 0.38f, 1f);
        [SerializeField] private Color backgroundColor = new Color(0.06f, 0.07f, 0.09f, 0.9f);

        private DamageableBehaviour owner;
        private SpriteRenderer background;
        private SpriteRenderer fill;
        private Transform fillTransform;

        public void Bind(DamageableBehaviour target)
        {
            owner = target;
            EnsureVisuals();
        }

        private void Awake()
        {
            if (owner == null)
            {
                owner = GetComponentInParent<DamageableBehaviour>();
            }

            EnsureVisuals();
        }

        private void LateUpdate()
        {
            if (owner == null || background == null || fill == null)
            {
                return;
            }

            bool visible = owner.IsAlive && owner.CurrentHealth < owner.MaxHealth - 0.01f;
            background.enabled = visible;
            fill.enabled = visible;

            if (!visible)
            {
                return;
            }

            float ratio = Mathf.Clamp01(owner.CurrentHealth / Mathf.Max(0.001f, owner.MaxHealth));
            fillTransform.localScale = new Vector3(barSize.x * ratio, barSize.y, 1f);
        }

        private void EnsureVisuals()
        {
            if (background != null || this == null)
            {
                return;
            }

            background = CreateBar("HealthBar_Background", backgroundColor, 39, RuntimeSprites.White);
            background.transform.localPosition = offset;
            background.transform.localScale = new Vector3(barSize.x, barSize.y, 1f);

            GameObject fillObject = new GameObject("HealthBar_Fill");
            fillObject.transform.SetParent(transform, false);
            fillObject.transform.localPosition = offset + new Vector3(-barSize.x * 0.5f, 0f, 0f);
            fillTransform = fillObject.transform;
            fillTransform.localScale = new Vector3(barSize.x, barSize.y, 1f);
            fill = fillObject.AddComponent<SpriteRenderer>();
            fill.sprite = RuntimeSprites.WhiteLeft;
            fill.color = fillColor;
            fill.sortingOrder = 40;
        }

        private SpriteRenderer CreateBar(string objectName, Color color, int order, Sprite sprite)
        {
            GameObject barObject = new GameObject(objectName);
            barObject.transform.SetParent(transform, false);
            SpriteRenderer renderer = barObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
