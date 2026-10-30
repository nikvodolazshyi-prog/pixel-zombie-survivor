using System.Collections.Generic;
using PixelZombieSurvivor.Building;
using PixelZombieSurvivor.Combat;
using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Data;
using PixelZombieSurvivor.Economy;
using PixelZombieSurvivor.Player;
using PixelZombieSurvivor.Waves;
using UnityEngine;

namespace PixelZombieSurvivor.Enemies
{
    /// <summary>
    /// Simple chase/attack enemy. It targets the player and nearby buildings,
    /// moves with Rigidbody2D.MovePosition and drops wave-scaled coins on death.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class Zombie : DamageableBehaviour
    {
        private static readonly List<Zombie> ActiveZombies = new List<Zombie>();

        [Header("References")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private SpriteAnimator animator;

        [Header("Behaviour")]
        [SerializeField, Min(0.05f)] private float retargetInterval = 0.3f;
        [SerializeField] private float walkBobAmplitude = 0.035f;
        [SerializeField] private float walkBobSpeed = 8f;

        private ZombieDataSO data;
        private PlayerHealth player;
        private WaveManager waveManager;
        private GameDatabaseSO database;
        private IDamageable target;
        private Transform targetTransform;
        private float moveSpeed;
        private float attackDamage;
        private float coinMultiplier;
        private float nextAttackTime;
        private float nextRetargetTime;
        private Vector3 visualBasePosition;
        private float bobSeed;
        private bool moving;
        private bool attackPlaying;

        public ZombieDataSO Data => data;

        protected override void Awake()
        {
            base.Awake();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
            if (animator == null) animator = GetComponentInChildren<SpriteAnimator>();

            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (visual != null)
            {
                visualBasePosition = visual.transform.localPosition;
            }

            bobSeed = Random.value * 10f;
            database = GameDatabaseSO.Load();
        }

        private void OnEnable()
        {
            if (!ActiveZombies.Contains(this))
            {
                ActiveZombies.Add(this);
            }

            EnemyRegistry.Register(this);
        }

        private void OnDisable()
        {
            ActiveZombies.Remove(this);
            EnemyRegistry.Unregister(this);
        }

        public void Initialize(
            PlayerHealth playerTarget,
            WaveManager ownerWaveManager,
            ZombieDataSO zombieData,
            float healthMultiplier,
            float damageMultiplier,
            float speedMultiplier,
            float coinRewardMultiplier)
        {
            player = playerTarget;
            waveManager = ownerWaveManager;
            data = zombieData;

            if (data == null)
            {
                Debug.LogError("Zombie initialized without ZombieDataSO.", this);
                return;
            }

            SetMaxHealth(data.MaxHealth * Mathf.Max(0.1f, healthMultiplier), true);
            moveSpeed = data.MoveSpeed * Mathf.Max(0.1f, speedMultiplier);
            attackDamage = data.Damage * Mathf.Max(0.1f, damageMultiplier);
            coinMultiplier = Mathf.Max(1f, coinRewardMultiplier);

            if (visual != null)
            {
                visual.sprite = data.Sprite;
                visual.color = data.Tint;
            }

            transform.localScale = Vector3.one * data.Scale;
            ChooseTarget();

            if (animator != null)
            {
                animator.Completed -= HandleAttackAnimationCompleted;
                animator.Completed += HandleAttackAnimationCompleted;
                PlayWalkAnimation();
            }
        }

        private void Update()
        {
            if (!IsAlive)
            {
                return;
            }

            if (Time.time >= nextRetargetTime || target == null || !target.IsAlive)
            {
                ChooseTarget();
            }

            UpdateVisuals();

            if (target == null || targetTransform == null)
            {
                moving = false;
                return;
            }

            float distance = Vector2.Distance(transform.position, targetTransform.position);
            if (distance <= data.AttackRange)
            {
                moving = false;
                if (TryAttack())
                {
                    PlayAttackAnimation();
                }
            }
            else
            {
                moving = true;
                FaceDirection((Vector2)(targetTransform.position - transform.position));
            }

            UpdateAnimation();
        }

        private void FixedUpdate()
        {
            if (!IsAlive || !moving || target == null || targetTransform == null)
            {
                return;
            }

            Vector2 current = body.position;
            Vector2 destination = targetTransform.position;
            Vector2 next = Vector2.MoveTowards(current, destination, moveSpeed * Time.fixedDeltaTime);
            GridManager grid = GridManager.Instance;
            if (grid != null)
            {
                next = grid.ClampToArena(next);
            }

            body.MovePosition(next);
        }

        private void ChooseTarget()
        {
            nextRetargetTime = Time.time + retargetInterval;

            IDamageable bestTarget = null;
            Transform bestTransform = null;
            float bestDistance = float.MaxValue;

            if (player != null && player.IsAlive)
            {
                bestTarget = player;
                bestTransform = player.transform;
                bestDistance = ((Vector2)player.transform.position - (Vector2)transform.position).sqrMagnitude;
            }

            // Spitters deliberately ignore walls: they keep aiming at the player and
            // their acid projectile pierces the first row of player-built walls.
            if (data != null && data.RangedAttack && bestTarget != null)
            {
                target = bestTarget;
                targetTransform = bestTransform;
                return;
            }

            BuildingBase nearestBuilding = BuildingBase.FindNearest(transform.position, 30f);
            if (nearestBuilding != null && nearestBuilding.IsAlive)
            {
                float buildingDistance = ((Vector2)nearestBuilding.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (buildingDistance < bestDistance)
                {
                    bestTarget = nearestBuilding;
                    bestTransform = nearestBuilding.transform;
                }
            }

            target = bestTarget;
            targetTransform = bestTransform;
        }

        private bool TryAttack()
        {
            if (Time.time < nextAttackTime || target == null || !target.IsAlive)
            {
                return false;
            }

            nextAttackTime = Time.time + data.AttackCooldown;
            Vector2 direction = targetTransform != null
                ? ((Vector2)targetTransform.position - (Vector2)transform.position).normalized
                : Vector2.right;

            if (data.RangedAttack)
            {
                FireAcid(direction);
                return true;
            }

            target.TakeDamage(new DamageInfo(
                attackDamage,
                transform.position,
                direction,
                Faction.Enemy,
                gameObject));

            return true;
        }

        private void FireAcid(Vector2 direction)
        {
            if (database == null || database.BulletPrefab == null)
            {
                return;
            }

            Vector3 spawnPosition = transform.position + (Vector3)(direction * 0.55f);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GameObject projectileObject = PoolService.Instance != null
                ? PoolService.Instance.Spawn(database.BulletPrefab, spawnPosition, Quaternion.Euler(0f, 0f, angle))
                : Instantiate(database.BulletPrefab, spawnPosition, Quaternion.Euler(0f, 0f, angle));

            Projectile projectile = projectileObject != null ? projectileObject.GetComponent<Projectile>() : null;
            projectile?.Launch(
                direction,
                attackDamage,
                data.ProjectileSpeed,
                data.AttackRange + 4f,
                Faction.Enemy,
                gameObject,
                data.ProjectileColor,
                data.WallsToPierce);
        }

        private void UpdateAnimation()
        {
            if (animator == null || !animator.HasFrames || attackPlaying)
            {
                return;
            }

            if (moving)
            {
                animator.Resume();
            }
            else
            {
                animator.Pause();
            }
        }

        private void PlayWalkAnimation()
        {
            if (animator == null || data == null)
            {
                return;
            }

            Sprite[] walkFrames = data.WalkFrames;
            if (walkFrames == null || walkFrames.Length == 0)
            {
                return;
            }

            attackPlaying = false;
            animator.Configure(walkFrames, data.WalkFramesPerSecond, true);
            animator.Play();
        }

        private void PlayAttackAnimation()
        {
            if (animator == null || data == null)
            {
                return;
            }

            Sprite[] attackFrames = data.AttackFrames;
            if (attackFrames == null || attackFrames.Length == 0)
            {
                return;
            }

            attackPlaying = true;
            animator.Configure(attackFrames, data.AttackFramesPerSecond, false);
        }

        private void HandleAttackAnimationCompleted()
        {
            PlayWalkAnimation();
        }

        private void UpdateVisuals()
        {
            if (visual == null)
            {
                return;
            }

            float bob = moving ? Mathf.Sin(Time.time * walkBobSpeed + bobSeed) * walkBobAmplitude : 0f;
            visual.transform.localPosition = visualBasePosition + new Vector3(0f, bob, 0f);
        }

        private void FaceDirection(Vector2 direction)
        {
            if (visual != null && Mathf.Abs(direction.x) > 0.01f)
            {
                visual.flipX = direction.x < 0f;
            }
        }

        public static Zombie FindNearest(Vector2 position, float maxRange)
        {
            Zombie nearest = null;
            float bestSqrDistance = maxRange * maxRange;

            for (int i = 0; i < ActiveZombies.Count; i++)
            {
                Zombie zombie = ActiveZombies[i];
                if (zombie == null || !zombie.IsAlive)
                {
                    continue;
                }

                float sqrDistance = ((Vector2)zombie.transform.position - position).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    nearest = zombie;
                }
            }

            return nearest;
        }

        protected override void OnDeath()
        {
            ActiveZombies.Remove(this);
            EnemyRegistry.Unregister(this);
            if (bodyCollider != null) bodyCollider.enabled = false;
            if (body != null) body.simulated = false;
            if (animator != null) animator.Pause();

            DropCoins();
            Destroy(gameObject, 0.12f);
        }

        private void OnDestroy()
        {
            if (animator != null)
            {
                animator.Completed -= HandleAttackAnimationCompleted;
            }
        }

        private void DropCoins()
        {
            if (data == null || database == null || database.CoinPrefab == null)
            {
                return;
            }

            int totalValue = Mathf.Max(1, Mathf.RoundToInt(data.CoinReward * coinMultiplier));
            CoinBurst.Drop(database.CoinPrefab, transform.position, totalValue, 8, 0.55f);
        }
    }
}
