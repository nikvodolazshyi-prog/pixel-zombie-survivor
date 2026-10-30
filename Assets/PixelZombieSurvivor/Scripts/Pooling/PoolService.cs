using System.Collections.Generic;
using UnityEngine;

namespace PixelZombieSurvivor.Core
{
    /// <summary>
    /// Tiny prefab-keyed pool used for bullets and coins. It keeps combat free from
    /// Instantiate/Destroy churn while staying easy to inspect and extend.
    /// </summary>
    [DefaultExecutionOrder(-700)]
    public sealed class PoolService : SingletonBehaviour<PoolService>
    {
        private readonly Dictionary<GameObject, Stack<GameObject>> pools =
            new Dictionary<GameObject, Stack<GameObject>>();

        private readonly Dictionary<GameObject, IPoolable[]> poolables =
            new Dictionary<GameObject, IPoolable[]>();

        private Transform poolRoot;

        protected override void Awake()
        {
            base.Awake();
            GameObject root = new GameObject("[Pooled Objects]");
            poolRoot = root.transform;
            poolRoot.SetParent(transform, false);
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                return null;
            }

            if (!pools.TryGetValue(prefab, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                pools.Add(prefab, stack);
            }

            GameObject instance = null;
            while (stack.Count > 0)
            {
                GameObject candidate = stack.Pop();
                if (candidate != null)
                {
                    instance = candidate;
                    break;
                }
            }

            if (instance == null)
            {
                instance = Instantiate(prefab);
                PooledObject marker = instance.GetComponent<PooledObject>();
                if (marker == null)
                {
                    marker = instance.AddComponent<PooledObject>();
                }

                marker.SourcePrefab = prefab;
                CachePoolables(instance, prefab);
            }

            instance.transform.SetParent(null, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            Rigidbody2D body = instance.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.simulated = true;

                // Re-enabling a body lets the physics engine push its cached position
                // back onto the transform, so the transform assignment above is not
                // enough. Writing body.position keeps projectiles from resuming their
                // previous flight from an old hit point.
                body.position = position;
                body.rotation = rotation.eulerAngles.z;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            IPoolable[] hooks = poolables[prefab];
            for (int i = 0; i < hooks.Length; i++)
            {
                hooks[i]?.OnSpawned();
            }

            return instance;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            PooledObject marker = instance.GetComponent<PooledObject>();
            if (marker == null || marker.SourcePrefab == null)
            {
                Destroy(instance);
                return;
            }

            GameObject prefab = marker.SourcePrefab;
            if (poolables.TryGetValue(prefab, out IPoolable[] hooks))
            {
                for (int i = 0; i < hooks.Length; i++)
                {
                    hooks[i]?.OnDespawned();
                }
            }

            instance.SetActive(false);
            instance.transform.SetParent(poolRoot, false);

            if (!pools.TryGetValue(prefab, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                pools.Add(prefab, stack);
            }

            stack.Push(instance);
        }

        private void CachePoolables(GameObject instance, GameObject prefab)
        {
            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            List<IPoolable> found = new List<IPoolable>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPoolable poolable)
                {
                    found.Add(poolable);
                }
            }

            poolables[prefab] = found.ToArray();
        }
    }
}
