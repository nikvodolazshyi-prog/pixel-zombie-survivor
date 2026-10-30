using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Economy;
using UnityEngine;

namespace PixelZombieSurvivor.Player
{
    /// <summary>
    /// Rigidbody2D top-down movement plus a short dash on Space. Aiming lives in
    /// PlayerWeaponController, buffs (speed/shield/vampirism) live in PlayerBuffs.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 5.5f;
        [SerializeField, Min(0.1f)] private float acceleration = 55f;

        [Header("Dash")]
        [SerializeField, Min(1f)] private float dashSpeedMultiplier = 3.2f;
        [SerializeField, Min(0.01f)] private float dashDuration = 0.16f;
        [SerializeField, Min(0.05f)] private float dashCooldown = 2.2f;
        [SerializeField, Min(0f)] private float dashInvulnerability = 0.2f;

        [Header("References (auto-resolved)")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerWeaponController weapons;
        [SerializeField] private PlayerHealth health;

        private Camera aimCamera;
        private bool shopOpen;
        private float dashUntil;
        private float dashReadyAt;
        private Vector2 dashDirection = Vector2.right;
        private float speedMultiplier = 1f;

        public bool IsDashing => Time.time < dashUntil;

        /// <summary>1 when the dash is ready, 0 right after using it.</summary>
        public float DashReady01 => dashCooldown <= 0f
            ? 1f
            : Mathf.Clamp01(1f - Mathf.Max(0f, dashReadyAt - Time.time) / dashCooldown);

        private bool CanMove =>
            GameManager.Instance != null &&
            GameManager.Instance.State != GameState.GameOver &&
            !GameManager.Instance.IsPaused &&
            !shopOpen;

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (weapons == null) weapons = GetComponent<PlayerWeaponController>();
            if (health == null) health = GetComponent<PlayerHealth>();

            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void Start()
        {
            aimCamera = Camera.main;

            if (ShopManager.Instance != null)
            {
                shopOpen = ShopManager.Instance.IsOpen;
                ShopManager.Instance.ShopToggled += HandleShopToggled;
            }
        }

        private void Update()
        {
            if (bodyRenderer != null && aimCamera != null)
            {
                Vector2 pointer = aimCamera.ScreenToWorldPoint(input.PointerScreenPosition);
                bodyRenderer.flipX = pointer.x < transform.position.x;
            }

            if (input.DashPressed && CanMove)
            {
                TryDash();
            }
        }

        private void FixedUpdate()
        {
            if (Time.time < dashUntil)
            {
                body.linearVelocity = dashDirection * (moveSpeed * speedMultiplier * dashSpeedMultiplier);
                return;
            }

            Vector2 desiredInput = CanMove ? input.Move : Vector2.zero;
            float speed = moveSpeed * speedMultiplier * (weapons != null ? weapons.CurrentMoveSpeedMultiplier : 1f);
            Vector2 targetVelocity = desiredInput * speed;
            body.linearVelocity = Vector2.MoveTowards(
                body.linearVelocity,
                targetVelocity,
                acceleration * Time.fixedDeltaTime);
        }

        /// <summary>Stacks with movement-cap bonuses from the buff selection.</summary>
        public void AddSpeedBonus(float percent)
        {
            speedMultiplier *= 1f + Mathf.Max(0f, percent);
        }

        private void TryDash()
        {
            if (Time.time < dashReadyAt)
            {
                return;
            }

            Vector2 direction = input.Move;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = bodyRenderer != null && bodyRenderer.flipX ? Vector2.left : Vector2.right;
            }

            dashDirection = direction.normalized;
            dashUntil = Time.time + dashDuration;
            dashReadyAt = dashUntil + dashCooldown;

            health?.GrantInvulnerability(dashInvulnerability);
            AudioService.Instance?.PlayDash();
        }

        private void HandleShopToggled(bool isOpen)
        {
            shopOpen = isOpen;
        }

        private void OnDestroy()
        {
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.ShopToggled -= HandleShopToggled;
            }
        }
    }
}
