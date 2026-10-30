using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Data;
using UnityEngine;

namespace PixelZombieSurvivor.Building
{
    /// <summary>Heals every damaged building within its repair radius, including itself.</summary>
    [RequireComponent(typeof(BuildingBase))]
    public sealed class RepairStation : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float healTickInterval = 0.25f;

        private BuildingBase building;
        private float timer;

        private void Awake()
        {
            building = GetComponent<BuildingBase>();
        }

        private void Update()
        {
            BuildingDataSO data = building != null ? building.Data : null;
            if (data == null || data.BuildingType != BuildingType.RepairStation || !building.IsAlive)
            {
                return;
            }

            timer += Time.deltaTime;
            if (timer < healTickInterval)
            {
                return;
            }

            int level = Mathf.Max(1, building.Level);
            float healAmount = data.GetRepairPerSecond(level) * timer;
            timer = 0f;
            float repairRadius = data.GetRepairRadius(level);
            float radiusSqr = repairRadius * repairRadius;

            for (int i = BuildingBase.Active.Count - 1; i >= 0; i--)
            {
                BuildingBase target = BuildingBase.Active[i];
                if (target == null || !target.IsAlive || target.CurrentHealth >= target.MaxHealth)
                {
                    continue;
                }

                if (((Vector2)target.transform.position - (Vector2)transform.position).sqrMagnitude <= radiusSqr)
                {
                    target.Heal(healAmount);
                }
            }
        }
    }
}
