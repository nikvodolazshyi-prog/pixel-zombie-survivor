using UnityEngine;

namespace PixelZombieSurvivor.UI
{
    /// <summary>Shared palette for the generated HUD and shop.</summary>
    public static class UiTheme
    {
        public static readonly Color Background = new Color(0.055f, 0.07f, 0.09f, 1f);
        public static readonly Color Panel = new Color(0.095f, 0.125f, 0.16f, 0.96f);
        public static readonly Color PanelRaised = new Color(0.13f, 0.17f, 0.215f, 0.98f);
        public static readonly Color PanelSoft = new Color(0.18f, 0.23f, 0.28f, 0.98f);
        public static readonly Color Text = new Color(0.91f, 0.94f, 0.96f, 1f);
        public static readonly Color Muted = new Color(0.54f, 0.63f, 0.69f, 1f);
        public static readonly Color Accent = new Color(1f, 0.63f, 0.24f, 1f);
        public static readonly Color AccentDark = new Color(0.66f, 0.34f, 0.09f, 1f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.3f, 1f);
        public static readonly Color Health = new Color(0.92f, 0.27f, 0.28f, 1f);
        public static readonly Color HealthLow = new Color(1f, 0.16f, 0.16f, 1f);
        public static readonly Color Success = new Color(0.38f, 0.86f, 0.46f, 1f);
        public static readonly Color Danger = new Color(0.96f, 0.3f, 0.3f, 1f);
        public static readonly Color Outline = new Color(0f, 0f, 0f, 0.85f);

        public static string ColorToHex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }
    }
}
