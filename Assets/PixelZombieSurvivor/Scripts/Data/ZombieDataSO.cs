using UnityEngine;

namespace PixelZombieSurvivor.Data
{
    /// <summary>Base zombie archetype. Wave multipliers are applied at runtime.</summary>
    [CreateAssetMenu(fileName = "Zombie_New", menuName = "Pixel Zombie Survivor/Zombie Data")]
    public sealed class ZombieDataSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Zombie";
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color tint = Color.white;
        [SerializeField, Min(0.5f)] private float scale = 1f;

        [Header("Combat")]
        [SerializeField, Min(1f)] private float maxHealth = 30f;
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.2f;
        [SerializeField, Min(0.1f)] private float damage = 8f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 0.8f;
        [SerializeField, Min(0.1f)] private float attackRange = 0.85f;

        [Header("Ranged Attack (acid spitter)")]
        [SerializeField] private bool rangedAttack;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 9f;
        [SerializeField, Min(0f)] private int wallsToPierce = 1;
        [SerializeField] private Color projectileColor = new Color(0.62f, 1f, 0.32f, 1f);

        [Header("Animation")]
        [SerializeField] private Sprite[] walkFrames;
        [SerializeField] private Sprite[] attackFrames;
        [SerializeField, Min(0.5f)] private float walkFramesPerSecond = 9f;
        [SerializeField, Min(0.5f)] private float attackFramesPerSecond = 11f;

        [Header("Reward")]
        [SerializeField, Min(1)] private int coinReward = 3;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Sprite => sprite;
        public Color Tint => tint;
        public float Scale => Mathf.Max(0.5f, scale);
        public float MaxHealth => Mathf.Max(1f, maxHealth);
        public float MoveSpeed => Mathf.Max(0.1f, moveSpeed);
        public float Damage => Mathf.Max(0.1f, damage);
        public float AttackCooldown => Mathf.Max(0.1f, attackCooldown);
        public float AttackRange => Mathf.Max(0.1f, attackRange);
        public bool RangedAttack => rangedAttack;
        public float ProjectileSpeed => Mathf.Max(0.1f, projectileSpeed);
        public int WallsToPierce => Mathf.Max(0, wallsToPierce);
        public Color ProjectileColor => projectileColor;
        public Sprite[] WalkFrames => walkFrames;
        public Sprite[] AttackFrames => attackFrames;
        public float WalkFramesPerSecond => Mathf.Max(0.5f, walkFramesPerSecond);
        public float AttackFramesPerSecond => Mathf.Max(0.5f, attackFramesPerSecond);
        public int CoinReward => Mathf.Max(1, coinReward);

#if UNITY_EDITOR
        public void EditorSetAnimation(Sprite[] walk, Sprite[] attack, float walkFps, float attackFps)
        {
            walkFrames = walk;
            attackFrames = attack;
            walkFramesPerSecond = walkFps;
            attackFramesPerSecond = attackFps;
        }
#endif

#if UNITY_EDITOR
        public void EditorConfigure(
            string newDisplayName,
            Sprite newSprite,
            Color newTint,
            float newScale,
            float newMaxHealth,
            float newMoveSpeed,
            float newDamage,
            float newAttackCooldown,
            float newAttackRange,
            int newCoinReward,
            bool useRangedAttack,
            float newProjectileSpeed,
            int newWallsToPierce,
            Color newProjectileColor)
        {
            displayName = newDisplayName;
            sprite = newSprite;
            tint = newTint;
            scale = newScale;
            maxHealth = newMaxHealth;
            moveSpeed = newMoveSpeed;
            damage = newDamage;
            attackCooldown = newAttackCooldown;
            attackRange = newAttackRange;
            coinReward = newCoinReward;
            rangedAttack = useRangedAttack;
            projectileSpeed = newProjectileSpeed;
            wallsToPierce = newWallsToPierce;
            projectileColor = newProjectileColor;
        }
#endif
    }
}
