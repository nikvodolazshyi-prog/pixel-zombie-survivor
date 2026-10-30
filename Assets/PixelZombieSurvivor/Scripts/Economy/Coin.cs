using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Player;
using UnityEngine;

namespace PixelZombieSurvivor.Economy
{
    /// <summary>
    /// Pooled coin pickup. Coins dropped by zombies or generators are credited
    /// instantly and play a short "pop" burst; manually spawned coins still support
    /// magnet pickup as a fallback.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Coin : MonoBehaviour, IPoolable
    {
        [Header("Pickup")]
        [SerializeField, Min(0.5f)] private float magnetRadius = 3.6f;
        [SerializeField, Min(0.1f)] private float magnetSpeed = 12f;
        [SerializeField, Min(0.1f)] private float collectRadius = 0.6f;

        [Header("Burst")]
        [SerializeField, Min(0.05f)] private float burstDuration = 0.32f;
        [SerializeField, Min(0f)] private float burstRise = 0.55f;

        [Header("Visual")]
        [SerializeField] private Transform visual;
        [SerializeField] private float bobAmplitude = 0.07f;
        [SerializeField] private float bobSpeed = 6f;

        private int value = 1;
        private bool collected;
        private bool burst;
        private float burstTime;
        private Vector3 visualBasePosition;
        private Vector3 visualBaseScale;
        private float bobSeed;
        private Collider2D pickupCollider;
        private SpriteRenderer visualRenderer;

        private void Awake()
        {
            if (visual == null)
            {
                SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
                visual = renderer != null ? renderer.transform : null;
            }

            if (visual != null)
            {
                visualBasePosition = visual.localPosition;
                visualBaseScale = visual.localScale;
                visualRenderer = visual.GetComponent<SpriteRenderer>();
            }

            pickupCollider = GetComponent<Collider2D>();
            bobSeed = Random.value * 10f;
        }

        public void Initialize(int coinValue)
        {
            value = Mathf.Max(1, coinValue);
            collected = false;
            burst = false;
            burstTime = 0f;
            RestoreVisual();
        }

        /// <summary>
        /// Visual-only coin used by <see cref="CoinBurst"/>: the money has already
        /// been credited, so the sprite just pops and vanishes.
        /// </summary>
        public void InitializeBurst()
        {
            value = 0;
            collected = true;
            burst = true;
            burstTime = 0f;
            RestoreVisual();
            if (pickupCollider != null)
            {
                pickupCollider.enabled = false;
            }
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
            {
                Despawn();
                return;
            }

            if (burst)
            {
                AnimateBurst();
                return;
            }

            if (collected)
            {
                return;
            }

            PlayerHealth player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null || !player.IsAlive)
            {
                Animate();
                return;
            }

            Vector2 playerPosition = player.transform.position;
            float distance = Vector2.Distance(transform.position, playerPosition);

            if (distance <= magnetRadius)
            {
                transform.position = Vector2.MoveTowards(
                    transform.position,
                    playerPosition,
                    magnetSpeed * Time.deltaTime);
            }

            if (distance <= collectRadius)
            {
                Collect();
                return;
            }

            Animate();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!collected && !burst && other.GetComponentInParent<PlayerHealth>() != null)
            {
                Collect();
            }
        }

        private void Collect()
        {
            if (collected)
            {
                return;
            }

            collected = true;
            CurrencyManager.Instance?.Add(value);
            AudioService.Instance?.PlayCoin();
            Despawn();
        }

        /// <summary>
        /// Awards the coin immediately. Used by the wave manager at the end of a wave
        /// so drops that fell away from the player are never left behind.
        /// </summary>
        public void ForceCollect()
        {
            Collect();
        }

        private void Animate()
        {
            if (visual == null)
            {
                return;
            }

            float bob = Mathf.Sin(Time.time * bobSpeed + bobSeed) * bobAmplitude;
            visual.localPosition = visualBasePosition + new Vector3(0f, bob, 0f);
        }

        private void AnimateBurst()
        {
            burstTime += Time.deltaTime;
            float t = Mathf.Clamp01(burstTime / Mathf.Max(0.05f, burstDuration));

            if (visual != null)
            {
                float rise = Mathf.Sin(t * Mathf.PI) * burstRise;
                visual.localPosition = visualBasePosition + new Vector3(0f, rise, 0f);
                visual.localScale = visualBaseScale * Mathf.Lerp(1.15f, 0.2f, t);
            }

            if (visualRenderer != null)
            {
                Color tint = visualRenderer.color;
                tint.a = 1f - t * t;
                visualRenderer.color = tint;
            }

            if (t >= 1f)
            {
                Despawn();
            }
        }

        private void RestoreVisual()
        {
            if (visual != null)
            {
                visual.localPosition = visualBasePosition;
                visual.localScale = visualBaseScale;
            }

            if (visualRenderer != null)
            {
                Color tint = visualRenderer.color;
                tint.a = 1f;
                visualRenderer.color = tint;
            }

            if (pickupCollider != null)
            {
                pickupCollider.enabled = true;
            }
        }

        public void OnSpawned()
        {
            collected = false;
            burst = false;
            burstTime = 0f;
            value = 1;
            RestoreVisual();
        }

        public void OnDespawned()
        {
            collected = true;
            burst = false;
        }

        private void Despawn()
        {
            if (PoolService.Instance != null)
            {
                PoolService.Instance.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
