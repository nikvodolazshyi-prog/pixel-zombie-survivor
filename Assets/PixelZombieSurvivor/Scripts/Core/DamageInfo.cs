using UnityEngine;

namespace PixelZombieSurvivor.Core
{
    /// <summary>Immutable description of a single hit.</summary>
    public readonly struct DamageInfo
    {
        public float Amount { get; }
        public Vector2 Point { get; }
        public Vector2 Direction { get; }
        public Faction SourceFaction { get; }
        public GameObject Source { get; }

        public DamageInfo(
            float amount,
            Vector2 point,
            Vector2 direction,
            Faction sourceFaction,
            GameObject source)
        {
            Amount = Mathf.Max(0f, amount);
            Point = point;
            Direction = direction;
            SourceFaction = sourceFaction;
            Source = source;
        }
    }
}
