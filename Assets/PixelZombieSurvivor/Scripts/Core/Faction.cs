namespace PixelZombieSurvivor.Core
{
    /// <summary>
    /// Defines which side an entity belongs to. Projectiles and melee attacks never
    /// damage entities from their own faction.
    /// </summary>
    public enum Faction
    {
        Neutral = 0,
        Player = 1,
        Enemy = 2
    }
}
