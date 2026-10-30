using UnityEngine;

namespace PixelZombieSurvivor.Core
{
    /// <summary>
    /// Small, scene-scoped singleton. It intentionally does not persist between
    /// scenes: restarting the arena should reset every manager and its state.
    /// </summary>
    public abstract class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[{typeof(T).Name}] Duplicate instance found on '{name}'. Destroying it.", this);
                Destroy(gameObject);
                return;
            }

            Instance = (T)this;
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
