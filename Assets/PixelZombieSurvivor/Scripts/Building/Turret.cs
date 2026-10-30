using PixelZombieSurvivor.Combat;
using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Data;
using PixelZombieSurvivor.Enemies;
using UnityEngine;

namespace PixelZombieSurvivor.Building
{
    /// <summary>
    /// Auto-targeting turret. It targets any hostile damageable (zombies, spitters,
    /// the boss) through <see cref="EnemyRegistry"/> and fires the pooled projectile.
    /// Damage, fire rate and range scale with the building level.
    /// </summary>
    [RequireComponent(typeof(BuildingBase))]
    public sealed class Turret : MonoBehaviour
    {
        [SerializeField] private Transform barrel;
        [SerializeField] private float retargetInterval = 0.2f;

        private BuildingBase building;
        private GameDatabaseSO database;
        private DamageableBehaviour target;
        private float nextRetargetTime;
        private float nextFireTime;

        private void Awake()
        {
            building = GetComponent<BuildingBase>();
            database = GameDatabaseSO.Load();
            if (barrel == null)
            {
                barrel = transform.Find("Barrel");
            }
        }

        private void Update()
        {
            BuildingDataSO data = building != null ? building.Data : null;
            if (data == null || data.BuildingType != BuildingType.Turret || !building.IsAlive)
            {
                return;
            }

            int level = Mathf.Max(1, building.Level);
            float range = data.GetTurretRange(level);

            if (Time.time >= nextRetargetTime || target == null || !target.IsAlive)
            {
                target = EnemyRegistry.FindNearest(transform.position, range);
                nextRetargetTime = Time.time + retargetInterval;
            }

            if (target == null || !target.IsAlive)
            {
                return;
            }

            Vector2 muzzlePosition = barrel != null ? (Vector2)barrel.position : (Vector2)transform.position;
            Vector2 toTarget = (Vector2)target.transform.position - muzzlePosition;
            if (toTarget.sqrMagnitude > range * range)
            {
                target = null;
                return;
            }

            if (barrel != null)
            {
                float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
                barrel.rotation = Quaternion.RotateTowards(
                    barrel.rotation,
                    targetRotation,
                    data.TurretRotationSpeed * Time.deltaTime);

                float angleDifference = Quaternion.Angle(barrel.rotation, targetRotation);
                if (angleDifference >= 12f)
                {
                    return;
                }
            }

            if (Time.time < nextFireTime)
            {
                return;
            }

            Fire(data, level, toTarget.normalized, muzzlePosition);
            nextFireTime = Time.time + data.GetTurretShotInterval(level);
        }

        private void Fire(BuildingDataSO data, int level, Vector2 direction, Vector2 muzzlePosition)
        {
            if (database == null || database.BulletPrefab == null)
            {
                return;
            }

            Vector3 spawnPosition = (Vector3)(muzzlePosition + direction * 0.55f);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GameObject projectileObject = PoolService.Instance != null
                ? PoolService.Instance.Spawn(database.BulletPrefab, spawnPosition, Quaternion.Euler(0f, 0f, angle))
                : Instantiate(database.BulletPrefab, spawnPosition, Quaternion.Euler(0f, 0f, angle));

            Projectile projectile = projectileObject != null ? projectileObject.GetComponent<Projectile>() : null;
            projectile?.Launch(
                direction,
                data.GetTurretDamage(level),
                data.TurretProjectileSpeed,
                data.GetTurretRange(level) + 2f,
                Faction.Player,
                gameObject,
                data.TurretProjectileColor);
        }
    }
}
