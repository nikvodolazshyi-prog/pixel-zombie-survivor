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
    /// Wave-25 boss. It chases the player, smashes buildings in melee, fires acid
    /// volleys at range and summons minions. Its three phases shorten cooldowns as
    /// health drops. The HUD listens to the static spawn/defeat events for the boss bar.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class BossController : DamageableBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "МЯСНИК";

        [Header("References")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private SpriteRenderer visual;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.6f;
        [SerializeField, Min(0.5f)] private float preferredDistance = 2.6f;
        [SerializeField, Min(0.05f)] private float retargetInterval = 0.35f;
        [SerializeField] private float bobAmplitude = 0.06f;
        [SerializeField] private float bobSpeed = 5f;

        [Header("Melee Smash")]
        [SerializeField, Min(1f)] private float meleeRange = 2.8f;
        [SerializeField, Min(1f)] private float meleeRadius = 3.1f;
        [SerializeField, Min(0f)] private float meleeDamage = 34f;
        [SerializeField, Min(0.2f)] private float meleeCooldown = 2.1f;

        [Header("Acid Volley")]
        [SerializeField, Min(0f)] private float projectileDamage = 18f;
        [SerializeField, Min(0.2f)] private float volleyCooldown = 4.2f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 8f;
        [SerializeField, Min(1)] private int volleyCount = 5;
        [SerializeField, Range(0f, 80f)] private float volleySpread = 44f;
        [SerializeField] private Color projectileColor = new Color(1f, 0.38f, 0.2f, 1f);

        [Header("Summon")]
        [SerializeField, Min(0.2f)] private float summonCooldown = 9f;
        [SerializeField, Min(1)] private int summonCount = 3;

        [Header("Reward")]
        [SerializeField, Min(1)] private int coinReward = 220;

        private static readonly Color PhaseTwoTint = new Color(1f, 0.82f, 0.72f, 1f);
        private static readonly Color PhaseThreeTint = new Color(1f, 0.55f, 0.48f, 1f);

        private PlayerHealth player;
        private WaveManager waveManager;
        private GameDatabaseSO database;
        private IDamageable target;
        private Transform targetTransform;
        private float damageMultiplier = 1f;
        private float coinMultiplier = 1f;
        private float nextMeleeTime;
        private float nextVolleyTime;
        private float nextSummonTime;
        private float nextRetargetTime;
        private Vector3 visualBasePosition;
        private bool moving;
        private int phase = 1;

        public static event System.Action<BossController> BossSpawned;
        public static event System.Action<BossController> BossDefeated;

        public static BossController ActiveBoss { get; private set; }

        public string DisplayName => displayName;
        public int Phase => phase;
        public float HealthFraction => MaxHealth > 0f ? Mathf.Clamp01(CurrentHealth / MaxHealth) : 0f;
        public bool IsBoss => true;

        protected override void Awake()
        {
            base.Awake();

            if (body == null) body = GetComponent<Rigidbody2D>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();

            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (visual != null)
            {
                visualBasePosition = visual.transform.localPosition;
            }

            database = GameDatabaseSO.Load();
        }

        private void OnEnable()
        {
            EnemyRegistry.Register(this);
        }

        private void OnDisable()
        {
            EnemyRegistry.Unregister(this);
            if (ActiveBoss == this)
            {
                ActiveBoss = null;
            }
        }

        public void Initialize(
            PlayerHealth playerTarget,
            WaveManager ownerWaveManager,
            float healthMultiplier,
            float damageScale,
            float speedScale,
            float rewardScale)
        {
            player = playerTarget;
            waveManager = ownerWaveManager;
            damageMultiplier = Mathf.Max(0.1f, damageScale);
            coinMultiplier = Mathf.Max(1f, rewardScale);

            SetMaxHealth(MaxHealth * Mathf.Max(0.1f, healthMultiplier), true);
            moveSpeed *= Mathf.Max(0.1f, speedScale);
            nextMeleeTime = Time.time + 1.4f;
            nextVolleyTime = Time.time + 2.6f;
            nextSummonTime = Time.time + summonCooldown;

            ActiveBoss = this;
            EnemyRegistry.Register(this);
            ChooseTarget();
            BossSpawned?.Invoke(this);
        }

        private void Update()
        {
            if (!IsAlive)
            {
                return;
            }

            UpdatePhase();

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

            Vector2 toTarget = (Vector2)targetTransform.position - (Vector2)transform.position;
            float distance = toTarget.magnitude;

            if (distance > preferredDistance)
            {
                moving = true;
            }
            else
            {
                moving = false;
            }

            if (visual != null && Mathf.Abs(toTarget.x) > 0.01f)
            {
                visual.flipX = toTarget.x < 0f;
            }

            if (distance <= meleeRange && Time.time >= nextMeleeTime)
            {
                PerformMeleeSmash();
            }
            else if (distance > meleeRange && Time.time >= nextVolleyTime)
            {
                FireVolley(toTarget.normalized);
            }

            if (Time.time >= nextSummonTime)
            {
                SummonMinions();
            }
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

            BuildingBase nearestBuilding = BuildingBase.FindNearest(transform.position, 40f);
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

        private void UpdatePhase()
        {
            float ratio = HealthFraction;
            int newPhase = ratio <= 0.33f ? 3 : ratio <= 0.66f ? 2 : 1;
            if (newPhase == phase)
            {
                return;
            }

            phase = newPhase;
            if (visual != null)
            {
                visual.color = phase == 3 ? PhaseThreeTint : phase == 2 ? PhaseTwoTint : Color.white;
            }
        }

        private void PerformMeleeSmash()
        {
            nextMeleeTime = Time.time + meleeCooldown / PhaseSpeed();
            float damage = meleeDamage * damageMultiplier;
            Vector2 center = transform.position;

            if (player != null && player.IsAlive &&
                ((Vector2)player.transform.position - center).sqrMagnitude <= meleeRadius * meleeRadius)
            {
                player.TakeDamage(new DamageInfo(damage, center, Vector2.right, Faction.Enemy, gameObject));
            }

            for (int i = BuildingBase.Active.Count - 1; i >= 0; i--)
            {
                BuildingBase building = BuildingBase.Active[i];
                if (building == null || !building.IsAlive)
                {
                    continue;
                }

                Vector2 toBuilding = (Vector2)building.transform.position - center;
                if (toBuilding.sqrMagnitude <= meleeRadius * meleeRadius)
                {
                    building.TakeDamage(new DamageInfo(
                        damage * 1.15f,
                        building.transform.position,
                        toBuilding.normalized,
                        Faction.Enemy,
                        gameObject));
                }
            }

            AudioService.Instance?.PlayHurt();
        }

        private void FireVolley(Vector2 baseDirection)
        {
            nextVolleyTime = Time.time + volleyCooldown / PhaseSpeed();
            if (database == null || database.BulletPrefab == null)
            {
                return;
            }

            float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
            int count = Mathf.Max(1, volleyCount + (phase - 1));
            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0.5f : i / (float)(count - 1);
                float angle = baseAngle + Mathf.Lerp(-volleySpread * 0.5f, volleySpread * 0.5f, t);
                Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Vector3 spawn = transform.position + (Vector3)(direction * 0.85f);

                GameObject projectileObject = PoolService.Instance != null
                    ? PoolService.Instance.Spawn(database.BulletPrefab, spawn, Quaternion.Euler(0f, 0f, angle))
                    : Instantiate(database.BulletPrefab, spawn, Quaternion.Euler(0f, 0f, angle));

                Projectile projectile = projectileObject != null ? projectileObject.GetComponent<Projectile>() : null;
                projectile?.Launch(
                    direction,
                    projectileDamage * damageMultiplier,
                    projectileSpeed,
                    18f,
                    Faction.Enemy,
                    gameObject,
                    projectileColor);
            }
        }

        private void SummonMinions()
        {
            nextSummonTime = Time.time + summonCooldown / PhaseSpeed();
            if (waveManager == null)
            {
                return;
            }

            int count = Mathf.Max(1, summonCount + phase - 1);
            waveManager.SpawnReinforcements(transform.position, count);
        }

        private float PhaseSpeed()
        {
            return 1f + (phase - 1) * 0.22f;
        }

        private void UpdateVisuals()
        {
            if (visual == null)
            {
                return;
            }

            float bob = moving ? Mathf.Sin(Time.time * bobSpeed) * bobAmplitude : 0f;
            visual.transform.localPosition = visualBasePosition + new Vector3(0f, bob, 0f);
        }

        protected override void OnDeath()
        {
            EnemyRegistry.Unregister(this);
            if (ActiveBoss == this)
            {
                ActiveBoss = null;
            }

            if (bodyCollider != null) bodyCollider.enabled = false;
            if (body != null) body.simulated = false;

            DropCoins();
            BossDefeated?.Invoke(this);
            Destroy(gameObject, 0.2f);
        }

        private void DropCoins()
        {
            if (database == null || database.CoinPrefab == null)
            {
                return;
            }

            int totalValue = Mathf.Max(1, Mathf.RoundToInt(coinReward * coinMultiplier));
            CoinBurst.Drop(database.CoinPrefab, transform.position, totalValue, 12, 1.15f);
        }
    }
}
