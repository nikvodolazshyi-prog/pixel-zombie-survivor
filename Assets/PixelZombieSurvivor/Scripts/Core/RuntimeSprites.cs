using UnityEngine;

namespace PixelZombieSurvivor.Core
{
    /// <summary>Tiny cached sprites used by runtime health bars.</summary>
    public static class RuntimeSprites
    {
        private static Sprite white;
        private static Sprite whiteLeft;

        public static Sprite White => white != null ? white : white = CreateWhite(new Vector2(0.5f, 0.5f));
        public static Sprite WhiteLeft => whiteLeft != null ? whiteLeft : whiteLeft = CreateWhite(new Vector2(0f, 0.5f));

        private static Sprite CreateWhite(Vector2 pivot)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Runtime White Pixel",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), pivot, 1f);
            sprite.name = "Runtime White Sprite";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
