using PixelZombieSurvivor.Core;
using UnityEngine;

namespace PixelZombieSurvivor.Economy
{
    /// <summary>
    /// Instant coin reward with a short visual burst. The wallet is credited on the
    /// spot, then a handful of coin sprites pop out, shrink and vanish — no coin ever
    /// stays on the ground waiting to be picked up.
    /// </summary>
    public static class CoinBurst
    {
        public static void Drop(
            GameObject coinPrefab,
            Vector3 origin,
            int totalValue,
            int maxVisuals = 8,
            float radius = 0.55f)
        {
            int value = Mathf.Max(1, totalValue);
            CurrencyManager.Instance?.Add(value);
            AudioService.Instance?.PlayCoin();

            if (coinPrefab == null || PoolService.Instance == null)
            {
                return;
            }

            int count = Mathf.Clamp(value, 1, Mathf.Max(1, maxVisuals));
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 position = origin + (Vector3)offset;
                GameObject coinObject = PoolService.Instance.Spawn(coinPrefab, position, Quaternion.identity);
                Coin coin = coinObject != null ? coinObject.GetComponent<Coin>() : null;
                coin?.InitializeBurst();
            }
        }
    }
}
