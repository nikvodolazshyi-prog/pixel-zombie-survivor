namespace PixelZombieSurvivor.Core
{
    /// <summary>Optional lifecycle hooks for pooled objects.</summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }
}
