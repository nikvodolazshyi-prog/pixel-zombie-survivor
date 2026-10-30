using UnityEngine;

namespace PixelZombieSurvivor.Core
{
    /// <summary>
    /// Fullscreen / windowed switch for standalone builds. Bind F11 or Alt+Enter.
    /// The component installs itself at runtime, so no scene wiring is required.
    /// Note that the editor's Game view ignores Screen.SetResolution - there the
    /// Game view has to be maximized instead (Shift+Space or Maximize On Play).
    /// </summary>
    public sealed class FullscreenController : MonoBehaviour
    {
        [Header("Window")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F11;
        [SerializeField] private Vector2Int windowedSize = new Vector2Int(1280, 720);

        private bool fullscreen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<FullscreenController>() != null)
            {
                return;
            }

            GameObject host = new GameObject("Fullscreen Controls");
            host.AddComponent<FullscreenController>();
        }

        private void Awake()
        {
            fullscreen = Screen.fullScreen;
        }

        private void Update()
        {
            bool altEnter = Input.GetKeyDown(KeyCode.Return) &&
                            (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));

            if (Input.GetKeyDown(toggleKey) || altEnter)
            {
                Toggle();
            }
        }

        /// <summary>Switches between borderless fullscreen and a 1280x720 window.</summary>
        public void Toggle()
        {
            fullscreen = !fullscreen;

            if (fullscreen)
            {
                Screen.SetResolution(
                    Display.main.systemWidth,
                    Display.main.systemHeight,
                    FullScreenMode.FullScreenWindow);
            }
            else
            {
                Screen.SetResolution(windowedSize.x, windowedSize.y, FullScreenMode.Windowed);
            }
        }
    }
}
