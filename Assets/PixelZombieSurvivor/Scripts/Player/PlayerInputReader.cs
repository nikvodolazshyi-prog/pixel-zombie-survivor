using PixelZombieSurvivor.Core;
using UnityEngine;

namespace PixelZombieSurvivor.Player
{
    /// <summary>
    /// Thin wrapper around Unity's legacy Input Manager. Gameplay actions are read from
    /// <see cref="InputBindings"/>, so keys can be remapped in the pause menu without
    /// touching movement, shooting or weapon code.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private static readonly KeyCode[] WeaponKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
        };

        public Vector2 Move
        {
            get
            {
                float horizontal = (InputBindings.IsHeld(GameAction.MoveRight) ? 1f : 0f) -
                                   (InputBindings.IsHeld(GameAction.MoveLeft) ? 1f : 0f);
                float vertical = (InputBindings.IsHeld(GameAction.MoveUp) ? 1f : 0f) -
                                 (InputBindings.IsHeld(GameAction.MoveDown) ? 1f : 0f);

                Vector2 input = new Vector2(horizontal, vertical);
                return input.sqrMagnitude > 1f ? input.normalized : input;
            }
        }

        public Vector3 PointerScreenPosition => Input.mousePosition;
        public bool ShootPressed => InputBindings.IsDown(GameAction.Shoot);
        public bool ShootHeld => InputBindings.IsHeld(GameAction.Shoot);
        public bool ReloadPressed => InputBindings.IsDown(GameAction.Reload);
        public bool DashPressed => InputBindings.IsDown(GameAction.Dash);
        public float Scroll => Input.mouseScrollDelta.y;

        public int GetWeaponSlotPressed()
        {
            for (int i = 0; i < WeaponKeys.Length; i++)
            {
                if (Input.GetKeyDown(WeaponKeys[i]))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
