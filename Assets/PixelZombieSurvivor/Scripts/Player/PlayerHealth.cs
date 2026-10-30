using System;
using PixelZombieSurvivor.Core;
using UnityEngine;

namespace PixelZombieSurvivor.Player
{
    /// <summary>
    /// Player health with a regenerating shield layer. Damage eats the shield first,
    /// the body flashes red on a real hit, and short mercy invulnerability prevents
    /// a crowd of zombies from deleting the player in a single frame.
    /// </summary>
    public sealed class PlayerHealth : DamageableBehaviour
    {
        [SerializeField, Min(0f)] private float invulnerabilityDuration = 0.22f;
        [SerializeField] private Color damageFlashColor = new Color(1f, 0.26f, 0.26f, 1f);
        [SerializeField, Min(0.01f)] private float damageFlashDuration = 0.16f;

        private float invulnerableUntil;
        private float shield;
        private float maxShield;

        /// <summary>Current shield and its capacity. Fired whenever the shield changes.</summary>
        public event Action<float, float> ShieldChanged;

        public float Shield => shield;
        public float MaxShield => maxShield;
        public bool HasShield => shield > 0.001f;

        protected override void Awake()
        {
            base.Awake();

            // Only the body sprite flashes; weapon and shadow keep their colours.
            ConfigureHitFlash(damageFlashColor, damageFlashDuration, GetComponent<SpriteRenderer>());
        }

        public override void TakeDamage(DamageInfo damage)
        {
            if (!IsAlive ||
                Time.time < invulnerableUntil ||
                damage.SourceFaction == Faction ||
                damage.Amount <= 0f)
            {
                return;
            }

            if (CheatManager.GodMode)
            {
                return;
            }

            float remaining = damage.Amount;
            if (shield > 0f)
            {
                float absorbed = Mathf.Min(shield, remaining);
                shield -= absorbed;
                remaining -= absorbed;
                ShieldChanged?.Invoke(shield, maxShield);
            }

            if (remaining > 0.001f)
            {
                base.TakeDamage(new DamageInfo(
                    remaining,
                    damage.Point,
                    damage.Direction,
                    damage.SourceFaction,
                    damage.Source));

                AudioService.Instance?.PlayHurt();
            }

            invulnerableUntil = Time.time + invulnerabilityDuration;
        }

        public float RestoreHealth(float amount)
        {
            return Heal(amount);
        }

        /// <summary>Sets shield capacity and refills the shield to full.</summary>
        public void SetShield(float capacity)
        {
            maxShield = Mathf.Max(0f, capacity);
            shield = maxShield;
            ShieldChanged?.Invoke(shield, maxShield);
        }

        public void GrantInvulnerability(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
        }

        protected override void OnDeath()
        {
            GameManager.Instance?.HandlePlayerDeath();
        }
    }
}
