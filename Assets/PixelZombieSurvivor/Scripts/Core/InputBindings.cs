using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelZombieSurvivor.Core
{
    /// <summary>Every action the player can rebind from the pause menu.</summary>
    public enum GameAction
    {
        MoveUp,
        MoveDown,
        MoveLeft,
        MoveRight,
        Shoot,
        Reload,
        Dash,
        Shop,
        Interact,
        Demolish,
        Pause
    }

    /// <summary>
    /// Central, persistent key map for the legacy Input Manager. Gameplay reads
    /// bindings from here instead of hardcoding KeyCode values, so the pause menu can
    /// remap WASD, LMB, reload, dash and the rest at runtime.
    /// </summary>
    public static class InputBindings
    {
        private const string Prefix = "PZS.Bind.";

        private static readonly Dictionary<GameAction, KeyCode> Defaults =
            new Dictionary<GameAction, KeyCode>
            {
                { GameAction.MoveUp, KeyCode.W },
                { GameAction.MoveDown, KeyCode.S },
                { GameAction.MoveLeft, KeyCode.A },
                { GameAction.MoveRight, KeyCode.D },
                { GameAction.Shoot, KeyCode.Mouse0 },
                { GameAction.Reload, KeyCode.R },
                { GameAction.Dash, KeyCode.Space },
                { GameAction.Shop, KeyCode.B },
                { GameAction.Interact, KeyCode.E },
                { GameAction.Demolish, KeyCode.Q },
                { GameAction.Pause, KeyCode.Escape }
            };

        private static readonly Dictionary<GameAction, KeyCode> Bound =
            new Dictionary<GameAction, KeyCode>();

        private static readonly KeyCode[] AllKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));

        static InputBindings()
        {
            foreach (KeyValuePair<GameAction, KeyCode> pair in Defaults)
            {
                int stored = PlayerPrefs.GetInt(Prefix + pair.Key, (int)pair.Value);
                Bound[pair.Key] = (KeyCode)stored;
            }
        }

        /// <summary>Raised after any binding changes so HUD labels can refresh.</summary>
        public static event Action Changed;

        /// <summary>True while the pause menu waits for a new key, so Esc does not unpause.</summary>
        public static bool IsCapturing { get; private set; }

        public static KeyCode Get(GameAction action)
        {
            KeyCode key;
            return Bound.TryGetValue(action, out key) ? key : KeyCode.None;
        }

        public static bool IsDown(GameAction action)
        {
            KeyCode key = Get(action);
            return key != KeyCode.None && Input.GetKeyDown(key);
        }

        public static bool IsHeld(GameAction action)
        {
            KeyCode key = Get(action);
            return key != KeyCode.None && Input.GetKey(key);
        }

        public static void Set(GameAction action, KeyCode key)
        {
            if (key == KeyCode.None || !IsBindable(key))
            {
                return;
            }

            KeyCode previous = Get(action);

            // Keep one key per action: if the new key already belongs to another
            // action, that action inherits the previous key of this one (a swap).
            List<GameAction> conflicts = new List<GameAction>();
            foreach (KeyValuePair<GameAction, KeyCode> pair in Bound)
            {
                if (pair.Key != action && pair.Value == key)
                {
                    conflicts.Add(pair.Key);
                }
            }

            for (int i = 0; i < conflicts.Count; i++)
            {
                Bound[conflicts[i]] = previous;
                Save(conflicts[i]);
            }

            Bound[action] = key;
            Save(action);
            Changed?.Invoke();
        }

        public static void ResetAll()
        {
            foreach (KeyValuePair<GameAction, KeyCode> pair in Defaults)
            {
                Bound[pair.Key] = pair.Value;
                Save(pair.Key);
            }

            Changed?.Invoke();
        }

        public static void BeginCapture()
        {
            IsCapturing = true;
        }

        public static void EndCapture()
        {
            IsCapturing = false;
        }

        /// <summary>
        /// Returns the first key or mouse button pressed this frame. Esc is excluded:
        /// the pause menu uses it to cancel a rebind.
        /// </summary>
        public static bool TryGetPressedKey(out KeyCode key)
        {
            for (int i = 0; i < AllKeys.Length; i++)
            {
                KeyCode candidate = AllKeys[i];
                if (!IsBindable(candidate) || candidate == KeyCode.Escape)
                {
                    continue;
                }

                if (Input.GetKeyDown(candidate))
                {
                    key = candidate;
                    return true;
                }
            }

            key = KeyCode.None;
            return false;
        }

        public static string GetActionLabel(GameAction action)
        {
            switch (action)
            {
                case GameAction.MoveUp: return "ДВИЖЕНИЕ ВПЕРЁД";
                case GameAction.MoveDown: return "ДВИЖЕНИЕ НАЗАД";
                case GameAction.MoveLeft: return "ДВИЖЕНИЕ ВЛЕВО";
                case GameAction.MoveRight: return "ДВИЖЕНИЕ ВПРАВО";
                case GameAction.Shoot: return "ОГОНЬ";
                case GameAction.Reload: return "ПЕРЕЗАРЯДКА";
                case GameAction.Dash: return "РЫВОК";
                case GameAction.Shop: return "МАГАЗИН";
                case GameAction.Interact: return "УЛУЧШИТЬ ПОСТРОЙКУ";
                case GameAction.Demolish: return "РЕЖИМ СНОСА";
                case GameAction.Pause: return "ПАУЗА";
                default: return action.ToString().ToUpperInvariant();
            }
        }

        public static string GetKeyLabel(GameAction action)
        {
            return GetKeyLabel(Get(action));
        }

        public static string GetKeyLabel(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Mouse0: return "ЛКМ";
                case KeyCode.Mouse1: return "ПКМ";
                case KeyCode.Mouse2: return "СКМ";
                case KeyCode.Space: return "ПРОБЕЛ";
                case KeyCode.Escape: return "ESC";
                case KeyCode.Tab: return "TAB";
                case KeyCode.Return: return "ENTER";
                case KeyCode.Backspace: return "BACKSPACE";
                case KeyCode.LeftShift:
                case KeyCode.RightShift: return "SHIFT";
                case KeyCode.LeftControl:
                case KeyCode.RightControl: return "CTRL";
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
            }

            string name = key.ToString();
            if (name.StartsWith("Alpha", StringComparison.Ordinal) && name.Length == 6)
            {
                return name.Substring(5);
            }

            if (name.StartsWith("Keypad", StringComparison.Ordinal) && name.Length == 7)
            {
                return "NUM " + name.Substring(6);
            }

            return name.ToUpperInvariant();
        }

        private static bool IsBindable(KeyCode key)
        {
            if (key == KeyCode.None)
            {
                return false;
            }

            switch (key)
            {
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                case KeyCode.LeftCommand:
                case KeyCode.RightCommand:
                case KeyCode.LeftWindows:
                case KeyCode.RightWindows:
                    return false;
            }

            return !key.ToString().StartsWith("Joystick", StringComparison.Ordinal);
        }

        private static void Save(GameAction action)
        {
            PlayerPrefs.SetInt(Prefix + action, (int)Get(action));
            PlayerPrefs.Save();
        }
    }
}
