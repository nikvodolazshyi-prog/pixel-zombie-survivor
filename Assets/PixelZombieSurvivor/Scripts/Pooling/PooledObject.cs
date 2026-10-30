using UnityEngine;

namespace PixelZombieSurvivor.Core
{
    /// <summary>Added automatically to pooled instances so they know their source prefab.</summary>
    public sealed class PooledObject : MonoBehaviour
    {
        public GameObject SourcePrefab { get; set; }
    }
}
