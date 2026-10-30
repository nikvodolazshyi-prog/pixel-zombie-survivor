using UnityEngine;

namespace PixelZombieSurvivor.Core
{
    /// <summary>Optional clamping follow camera with a lightweight screen shake.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float smoothTime = 0.12f;
        [SerializeField] private Vector2 boundsCenter = Vector2.zero;
        [SerializeField] private Vector2 boundsExtents = new Vector2(3.5f, 2.5f);
        [SerializeField] private float shakeDecay = 8f;

        private Vector3 velocity;
        private float shakeStrength;
        private Camera cachedCamera;

        public Camera Camera => cachedCamera;

        private void Awake()
        {
            cachedCamera = GetComponent<Camera>();
        }

        private void Start()
        {
            if (target == null && GameManager.Instance != null && GameManager.Instance.Player != null)
            {
                target = GameManager.Instance.Player.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 targetPosition = new Vector3(target.position.x, target.position.y, transform.position.z);
            Vector3 next = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);

            float halfHeight = cachedCamera.orthographicSize;
            float halfWidth = halfHeight * cachedCamera.aspect;
            float x = ClampAxis(next.x, boundsCenter.x, boundsExtents.x, halfWidth);
            float y = ClampAxis(next.y, boundsCenter.y, boundsExtents.y, halfHeight);

            shakeStrength = Mathf.MoveTowards(shakeStrength, 0f, shakeDecay * Time.unscaledDeltaTime);
            Vector2 shake = shakeStrength > 0f ? Random.insideUnitCircle * shakeStrength : Vector2.zero;
            transform.position = new Vector3(x + shake.x, y + shake.y, next.z);
        }

        /// <summary>
        /// Keeps the camera inside the arena. When the view is wider than the arena
        /// (very wide aspect ratios) the axis simply stays centered instead of
        /// producing an inverted clamp range.
        /// </summary>
        private static float ClampAxis(float value, float center, float extents, float halfView)
        {
            float min = center - extents + halfView;
            float max = center + extents - halfView;
            return min <= max ? Mathf.Clamp(value, min, max) : center;
        }

        public void Shake(float strength, float duration = 0.08f)
        {
            shakeStrength = Mathf.Max(shakeStrength, Mathf.Max(0f, strength));
            // Duration is kept for readability/API symmetry; decay is handled per frame.
            shakeDecay = Mathf.Max(0.01f, shakeStrength / Mathf.Max(0.01f, duration));
        }

        public void Configure(Transform followTarget, Vector2 center, Vector2 extents)
        {
            target = followTarget;
            boundsCenter = center;
            boundsExtents = extents;
        }
    }
}
