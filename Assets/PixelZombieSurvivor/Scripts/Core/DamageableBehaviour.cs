using System;
using System.Collections;
using UnityEngine;

namespace PixelZombieSurvivor.Core
{
    /// <summary>
    /// Shared health implementation for the player, zombies and buildings.
    /// Combat code only depends on <see cref="IDamageable"/>; visual feedback and
    /// death behaviour are isolated in derived classes.
    /// </summary>
    public abstract class DamageableBehaviour : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] protected float maxHealth = 100f;
        [SerializeField] protected Faction faction = Faction.Neutral;
        [SerializeField] protected SpriteRenderer[] damageRenderers;

        [Header("Feedback")]
        [SerializeField] private bool flashOnHit = true;
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private float hitFlashDuration = 0.07f;

        private float currentHealth;
        private bool isDead;
        private Coroutine flashRoutine;
        private Color[] originalColors;

        public event Action<DamageableBehaviour, DamageInfo> Damaged;
        public event Action<DamageableBehaviour> Died;
        public event Action<DamageableBehaviour, float, float> HealthChanged;

        public Faction Faction => faction;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsAlive => !isDead;
        public Transform Transform => transform;

        protected virtual void Awake()
        {
            if (damageRenderers == null || damageRenderers.Length == 0)
            {
                damageRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            }

            originalColors = new Color[damageRenderers.Length];
            for (int i = 0; i < damageRenderers.Length; i++)
            {
                originalColors[i] = damageRenderers[i] != null ? damageRenderers[i].color : Color.white;
            }

            currentHealth = maxHealth;
        }

        protected virtual void Start()
        {
            RaiseHealthChanged();
        }

        public virtual void TakeDamage(DamageInfo damage)
        {
            if (!IsAlive || damage.Amount <= 0f || damage.SourceFaction == faction)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damage.Amount);
            Damaged?.Invoke(this, damage);
            RaiseHealthChanged();

            if (flashOnHit && isActiveAndEnabled)
            {
                if (flashRoutine != null)
                {
                    StopCoroutine(flashRoutine);
                }

                flashRoutine = StartCoroutine(FlashRoutine());
            }

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        public float Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return 0f;
            }

            float previous = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            float restored = currentHealth - previous;

            if (restored > 0f)
            {
                RaiseHealthChanged();
            }

            return restored;
        }

        /// <summary>Used by runtime initialization (waves, buildings, upgrades).</summary>
        public void SetMaxHealth(float value, bool refill = true)
        {
            maxHealth = Mathf.Max(1f, value);
            currentHealth = refill ? maxHealth : Mathf.Min(currentHealth, maxHealth);
            RaiseHealthChanged();
        }

        protected void RaiseHealthChanged()
        {
            HealthChanged?.Invoke(this, currentHealth, maxHealth);
        }

        /// <summary>
        /// Lets a derived damageable pick its own flash colour/tint and restrict the
        /// flash to specific renderers (the player flashes red, zombies flash white).
        /// Must be called after the base Awake so the original colours are known.
        /// </summary>
        protected void ConfigureHitFlash(Color color, float duration, params SpriteRenderer[] renderers)
        {
            hitFlashColor = color;
            hitFlashDuration = Mathf.Max(0.01f, duration);

            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            damageRenderers = renderers;
            originalColors = new Color[damageRenderers.Length];
            for (int i = 0; i < damageRenderers.Length; i++)
            {
                originalColors[i] = damageRenderers[i] != null ? damageRenderers[i].color : Color.white;
            }
        }

        protected virtual void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            currentHealth = 0f;
            RaiseHealthChanged();
            Died?.Invoke(this);
            OnDeath();
        }

        protected virtual void OnDeath()
        {
        }

        private IEnumerator FlashRoutine()
        {
            SetRendererColors(hitFlashColor);
            yield return new WaitForSeconds(hitFlashDuration);
            RestoreRendererColors();
            flashRoutine = null;
        }

        private void SetRendererColors(Color color)
        {
            for (int i = 0; i < damageRenderers.Length; i++)
            {
                SpriteRenderer renderer = damageRenderers[i];
                if (renderer != null)
                {
                    renderer.color = color;
                }
            }
        }

        private void RestoreRendererColors()
        {
            for (int i = 0; i < damageRenderers.Length; i++)
            {
                SpriteRenderer renderer = damageRenderers[i];
                if (renderer != null)
                {
                    renderer.color = originalColors[i];
                }
            }
        }
    }
}
