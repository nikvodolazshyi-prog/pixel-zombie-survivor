using System;
using PixelZombieSurvivor.Data;
using UnityEngine;

namespace PixelZombieSurvivor.Core
{
    /// <summary>Owns the player's wallet and is the only writer of the balance.</summary>
    [DefaultExecutionOrder(-600)]
    public sealed class CurrencyManager : SingletonBehaviour<CurrencyManager>
    {
        [SerializeField, Min(0)] private int fallbackStartingCoins = 300;

        public event Action<int> CoinsChanged;

        public int Balance { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            GameDatabaseSO database = GameDatabaseSO.Load();
            int startingCoins = database != null ? database.StartingCoins : fallbackStartingCoins;
            Balance = Mathf.Max(0, startingCoins);
        }

        private void Start()
        {
            CoinsChanged?.Invoke(Balance);
        }

        public void Add(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Balance += amount;
            CoinsChanged?.Invoke(Balance);
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (Balance < amount)
            {
                return false;
            }

            Balance -= amount;
            CoinsChanged?.Invoke(Balance);
            return true;
        }
    }
}
