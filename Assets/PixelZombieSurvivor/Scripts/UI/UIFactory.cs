using UnityEngine;
using UnityEngine.UI;

namespace PixelZombieSurvivor.UI
{
    /// <summary>
    /// Small procedural UI toolkit. Keeping construction in code makes the demo
    /// self-contained (no prefab references) while still using standard uGUI components.
    /// </summary>
    public static class UIFactory
    {
        private static Font cachedFont;

        public static Font Font
        {
            get
            {
                if (cachedFont != null)
                {
                    return cachedFont;
                }

                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (cachedFont == null)
                {
                    cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }

                if (cachedFont == null)
                {
                    cachedFont = Font.CreateDynamicFontFromOSFont(
                        new[] { "Consolas", "Courier New", "Arial" },
                        18);
                }

                return cachedFont;
            }
        }

        public static RectTransform CreateUIObject(string objectName, Transform parent)
        {
            GameObject instance = new GameObject(objectName, typeof(RectTransform));
            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        public static RectTransform Anchor(
            RectTransform rect,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static Image CreatePanel(string objectName, Transform parent, Color color, bool stretch = false)
        {
            RectTransform rect = CreateUIObject(objectName, parent);
            if (stretch)
            {
                Stretch(rect);
            }

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text CreateText(
            string objectName,
            Transform parent,
            string content,
            int fontSize,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft,
            bool raycastTarget = false)
        {
            RectTransform rect = CreateUIObject(objectName, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = raycastTarget;

            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = UiTheme.Outline;
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.useGraphicAlpha = true;
            return text;
        }

        public static Button CreateButton(
            string objectName,
            Transform parent,
            string label,
            Color background,
            Color labelColor,
            int fontSize = 18)
        {
            RectTransform rect = CreateUIObject(objectName, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = background;
            image.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f),
                pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            Text text = CreateText("Label", rect, label, fontSize, labelColor, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 6f, 4f, 6f, 4f);
            text.raycastTarget = false;
            return button;
        }

        public static Image CreateImage(
            string objectName,
            Transform parent,
            Sprite sprite,
            Color color,
            bool preserveAspect = true)
        {
            RectTransform rect = CreateUIObject(objectName, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }

        public static void MakeFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
