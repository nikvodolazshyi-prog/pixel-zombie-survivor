using PixelZombieSurvivor.Data;
using UnityEngine;

namespace PixelZombieSurvivor.Player
{
    /// <summary>
    /// Runtime ammo state for one owned weapon. The data asset remains immutable,
    /// while this object holds the clip/reserve that changes during a run.
    /// </summary>
    public sealed class WeaponRuntime
    {
        public WeaponDataSO Data { get; }
        public int Clip { get; private set; }
        public int Reserve { get; private set; }
        public bool IsReloading { get; internal set; }

        public WeaponRuntime(WeaponDataSO data)
        {
            Data = data;
            Clip = data.ClipSize;
            Reserve = data.StartingReserveAmmo;
        }

        public bool CanFire => Clip > 0;

        public bool ConsumeRound()
        {
            if (Clip <= 0)
            {
                return false;
            }

            Clip--;
            return true;
        }

        public int AddReserve(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int previous = Reserve;
            Reserve = Mathf.Min(Data.MaxReserveAmmo, Reserve + amount);
            return Reserve - previous;
        }

        public int CompleteReload()
        {
            if (Clip >= Data.ClipSize || Reserve <= 0)
            {
                return 0;
            }

            int needed = Data.ClipSize - Clip;
            int loaded = Mathf.Min(needed, Reserve);
            Clip += loaded;
            Reserve -= loaded;
            return loaded;
        }

        public bool HasReserveCapacity => Reserve < Data.MaxReserveAmmo;
    }
}
