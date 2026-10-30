using UnityEngine;

namespace PixelZombieSurvivor.Core
{
    /// <summary>Anything that can be targeted and damaged by combat code.</summary>
    public interface IDamageable
    {
        Faction Faction { get; }
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsAlive { get; }
        Transform Transform { get; }
        void TakeDamage(DamageInfo damage);
    }
}
