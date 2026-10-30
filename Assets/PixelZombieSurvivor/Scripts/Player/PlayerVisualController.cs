using PixelZombieSurvivor.Core;
using UnityEngine;

namespace PixelZombieSurvivor.Player
{
    /// <summary>
    /// Swaps idle/walk animation clips based on the player's actual velocity. It is
    /// intentionally separate from PlayerController: movement and presentation can
    /// evolve independently.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerVisualController : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private SpriteAnimator animator;
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] walkFrames;
        [SerializeField, Min(0.5f)] private float idleFps = 3f;
        [SerializeField, Min(0.5f)] private float walkFps = 10f;
        [SerializeField, Min(0f)] private float walkVelocityThreshold = 0.05f;

        private Rigidbody2D body;
        private bool walking;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (animator == null)
            {
                animator = GetComponent<SpriteAnimator>();
            }
        }

        private void Start()
        {
            if (animator == null)
            {
                return;
            }

            walking = false;
            animator.Configure(idleFrames, idleFps, true);
        }

        private void Update()
        {
            if (animator == null || body == null)
            {
                return;
            }

            bool isWalking = body.linearVelocity.sqrMagnitude > walkVelocityThreshold * walkVelocityThreshold;
            if (isWalking == walking)
            {
                return;
            }

            walking = isWalking;
            Sprite[] clip = isWalking && walkFrames != null && walkFrames.Length > 0 ? walkFrames : idleFrames;
            animator.Configure(clip, isWalking ? walkFps : idleFps, true);
        }
    }
}
