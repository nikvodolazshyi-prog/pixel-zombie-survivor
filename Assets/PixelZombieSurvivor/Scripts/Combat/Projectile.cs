using System.Collections.Generic;
using PixelZombieSurvivor.Building;
using PixelZombieSurvivor.Core;
using UnityEngine;

namespace PixelZombieSurvivor.Combat
{
    /// <summary>
    /// Kinematic trigger projectile. It uses Rigidbody2D movement in FixedUpdate so
    /// collisions stay deterministic at high fire rates, and supports object pooling.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class Projectile : MonoBehaviour, IPoolable
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField, Min(0.01f)] private float sweepRadius = 0.09f;

        // Safety net: even if a projectile somehow stops colliding, it can never
        // linger in the world for longer than this.
        [SerializeField, Min(0.1f)] private float maxLifetime = 3f;

        private readonly RaycastHit2D[] sweepHits = new RaycastHit2D[8];
        private readonly HashSet<BuildingBase> piercedWalls = new HashSet<BuildingBase>();

        private Rigidbody2D body;
        private Vector2 direction = Vector2.right;
        private float damage;
        private float speed;
        private float maxRange;
        private float travelled;
        private float aliveTime;
        private int wallPierce;
        private int wallsPierced;
        private Faction sourceFaction;
        private GameObject source;
        private ContactFilter2D hitFilter;
        private bool active;
        private bool despawned;
        private bool launched;
        private float idleTime;
        private float stallTime;
        private float lastTravelled;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            hitFilter = ContactFilter2D.noFilter;
            hitFilter.useTriggers = false;
        }

        public void Launch(
            Vector2 launchDirection,
            float damageAmount,
            float projectileSpeed,
            float range,
            Faction faction,
            GameObject sourceObject,
            Color tint,
            int wallPierceCount = 0)
        {
            direction = launchDirection.sqrMagnitude > 0.0001f ? launchDirection.normalized : Vector2.right;
            damage = Mathf.Max(0f, damageAmount);
            speed = Mathf.Max(0.1f, projectileSpeed);
            maxRange = Mathf.Max(0.1f, range);
            sourceFaction = faction;
            source = sourceObject;
            travelled = 0f;
            aliveTime = 0f;
            wallPierce = Mathf.Max(0, wallPierceCount);
            wallsPierced = 0;
            piercedWalls.Clear();
            despawned = false;
            active = true;
            launched = true;
            idleTime = 0f;
            stallTime = 0f;
            lastTravelled = 0f;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // A recycled bullet may still carry the physics position of its previous
            // flight. Snap the body onto the muzzle before the first step.
            if (body != null)
            {
                body.position = transform.position;
                body.rotation = angle;
                body.linearVelocity = Vector2.zero;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = tint;
            }
        }

        private void FixedUpdate()
        {
            if (!active)
            {
                return;
            }

            float step = speed * Time.fixedDeltaTime;
            aliveTime += Time.fixedDeltaTime;
            if (aliveTime >= maxLifetime)
            {
                Despawn();
                return;
            }

            Vector2 start = body.position;

            // Sweep instead of relying only on trigger contacts: fast bullets would
            // otherwise tunnel through thin enemy colliders at low physics rates.
            if (TrySweepHit(start, step))
            {
                return;
            }

            Vector2 next = start + direction * step;
            body.MovePosition(next);
            travelled += step;

            if (travelled >= maxRange)
            {
                Despawn();
            }
        }

        /// <summary>
        /// Safety net for pool reuse and edge cases: a projectile that exists in the
        /// world without being launched, or one that stopped advancing, is removed
        /// instead of becoming a permanently frozen sprite on the arena floor.
        /// </summary>
        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
            {
                Despawn();
                return;
            }

            if (!active)
            {
                idleTime += Time.unscaledDeltaTime;
                if (idleTime > 0.2f)
                {
                    Despawn();
                }

                return;
            }

            if (!launched)
            {
                Despawn();
                return;
            }

            if (Mathf.Approximately(travelled, lastTravelled))
            {
                stallTime += Time.deltaTime;
                if (stallTime > 0.5f)
                {
                    Despawn();
                }
            }
            else
            {
                stallTime = 0f;
                lastTravelled = travelled;
            }
        }

        private bool TrySweepHit(Vector2 start, float distance)
        {
            int count = Physics2D.CircleCast(
                start,
                sweepRadius,
                direction,
                hitFilter,
                sweepHits,
                distance);

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = sweepHits[i];
                Collider2D collider = hit.collider;
                if (collider == null)
                {
                    continue;
                }

                if (collider.GetComponentInParent<ProjectileBlocker>() != null)
                {
                    body.position = hit.point;
                    Despawn();
                    return true;
                }

                IDamageable damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive || damageable.Faction == sourceFaction)
                {
                    continue;
                }

                if (ShouldPierceWall(damageable))
                {
                    continue;
                }

                damageable.TakeDamage(new DamageInfo(
                    damage,
                    hit.point,
                    direction,
                    sourceFaction,
                    source));

                body.position = hit.point;
                Despawn();
                return true;
            }

            return false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!active)
            {
                return;
            }

            if (other.GetComponentInParent<ProjectileBlocker>() != null)
            {
                Despawn();
                return;
            }

            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive || damageable.Faction == sourceFaction)
            {
                return;
            }

            if (ShouldPierceWall(damageable))
            {
                return;
            }

            Vector2 hitPoint = other.ClosestPoint(transform.position);
            damageable.TakeDamage(new DamageInfo(
                damage,
                hitPoint,
                direction,
                sourceFaction,
                source));

            Despawn();
        }

        /// <summary>
        /// Acid projectiles may ignore the first player-built wall row. Each wall is
        /// remembered, otherwise a single wall caught by several sweeps would eat
        /// the entire pierce budget.
        /// </summary>
        private bool ShouldPierceWall(IDamageable damageable)
        {
            if (wallPierce <= 0 || wallsPierced >= wallPierce)
            {
                return false;
            }

            if (!(damageable is BuildingBase building) || !building.IsWall)
            {
                return false;
            }

            if (piercedWalls.Contains(building))
            {
                return true;
            }

            piercedWalls.Add(building);
            wallsPierced++;
            return true;
        }

        public void OnSpawned()
        {
            active = false;
            despawned = false;
            launched = false;
            idleTime = 0f;
            stallTime = 0f;
            lastTravelled = 0f;
            travelled = 0f;
            aliveTime = 0f;
            wallPierce = 0;
            wallsPierced = 0;
            piercedWalls.Clear();
        }

        public void OnDespawned()
        {
            active = false;
            despawned = true;
            launched = false;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private void Despawn()
        {
            if (despawned)
            {
                return;
            }

            despawned = true;
            active = false;
            launched = false;
            if (PoolService.Instance != null)
            {
                PoolService.Instance.Despawn(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }
    }
}
