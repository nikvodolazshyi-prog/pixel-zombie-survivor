using System.Collections.Generic;
using PixelZombieSurvivor.Core;
using UnityEngine;

namespace PixelZombieSurvivor.Enemies
{
    /// <summary>
    /// Lightweight registry of hostile damageables. Turrets use it to target both
    /// regular zombies and the boss without depending on the Zombie component.
    /// </summary>
    public static class EnemyRegistry
    {
        private static readonly List<DamageableBehaviour> enemies = new List<DamageableBehaviour>();

        public static IReadOnlyList<DamageableBehaviour> Active => enemies;

        public static void Register(DamageableBehaviour enemy)
        {
            if (enemy != null && !enemies.Contains(enemy))
            {
                enemies.Add(enemy);
            }
        }

        public static void Unregister(DamageableBehaviour enemy)
        {
            if (enemy != null)
            {
                enemies.Remove(enemy);
            }
        }

        public static DamageableBehaviour FindNearest(Vector2 position, float maxRange)
        {
            DamageableBehaviour nearest = null;
            float bestSqrDistance = maxRange * maxRange;

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                DamageableBehaviour enemy = enemies[i];
                if (enemy == null)
                {
                    enemies.RemoveAt(i);
                    continue;
                }

                if (!enemy.IsAlive || enemy.Faction != Faction.Enemy)
                {
                    continue;
                }

                float sqrDistance = ((Vector2)enemy.transform.position - position).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    nearest = enemy;
                }
            }

            return nearest;
        }

        public static int CountHostiles()
        {
            int count = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && enemies[i].IsAlive)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
