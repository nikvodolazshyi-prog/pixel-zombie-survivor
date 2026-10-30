using UnityEngine;

namespace PixelZombieSurvivor.Waves
{
    /// <summary>Marker used by WaveManager to find valid perimeter spawn locations.</summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
        }
    }
}
