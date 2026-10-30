using System;
using PixelZombieSurvivor.Core;
using PixelZombieSurvivor.Waves;
using UnityEngine;

namespace PixelZombieSurvivor.Player
{
    /// <summary>Upgrades offered after milestone waves.</summary>
    public enum BuffType
    {
        Vampirism = 0,
        Speed = 1,
        Shield = 2
    }

    /// <summary>
    /// Late-run upgrades. After wave 10 (and every five waves after that) the game
    /// offers one of three picks: lifesteal per kill, extra movement speed, or a
    /// shield that refills at the start of every wave. Choices stack.
    /// </summary>
    public sealed class PlayerBuffs : MonoBehaviour
    {
        [Header("Numbers")]
        [SerializeField, Min(0f)] private float vampireHealPerKill = 3f;
        [SerializeField, Range(0.01f, 1f)] private float speedBonusPerPick = 0.12f;
        [SerializeField, Min(0f)] private float shieldPerPick = 30f;

        [Header("Timing")]
        [SerializeField, Min(1)] private int firstChoiceWave = 10;
        [SerializeField, Min(1)] private int choiceEveryWaves = 5;

        private WaveManager waves;
        private PlayerHealth health;
        private PlayerController controller;
        private int lastKills;

        /// <summary>Raised when any buff level changes, so the HUD can refresh.</summary>
        public event Action BuffsChanged;

        /// <summary>Raised with the completed wave number when a choice must be shown.</summary>
        public event Action<int> ChoiceRequested;

        /// <summary>Raised after the player picks an upgrade.</summary>
        public event Action ChoiceResolved;

        public bool AwaitingChoice { get; private set; }
        public int VampirismLevel { get; private set; }
        public int SpeedLevel { get; private set; }
        public int ShieldLevel { get; private set; }
        public float VampireHealPerKill => vampireHealPerKill * VampirismLevel;
        public float ShieldCapacity => shieldPerPick * ShieldLevel;

        /// <summary>Localized card title for the upgrade screen.</summary>
        public string GetBuffTitle(BuffType type)
        {
            switch (type)
            {
                case BuffType.Vampirism:
                    return "ВАМПИРИЗМ";
                case BuffType.Speed:
                    return "СКОРОСТЬ";
                default:
                    return "ЩИТ";
            }
        }

        /// <summary>Localized card description including the current stack level.</summary>
        public string GetBuffDescription(BuffType type)
        {
            switch (type)
            {
                case BuffType.Vampirism:
                    return $"+{vampireHealPerKill:0.#} HP за каждое убийство\n\nСейчас: {VampirismLevel} ур.";
                case BuffType.Speed:
                    return $"+{speedBonusPerPick * 100f:0}% к скорости движения\n\nСейчас: {SpeedLevel} ур.";
                default:
                    return $"{shieldPerPick:0} щита, восстанавливается\nв начале каждой волны\n\nСейчас: {ShieldLevel} ур.";
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            PlayerHealth player = FindAnyObjectByType<PlayerHealth>();
            if (player != null && player.GetComponent<PlayerBuffs>() == null)
            {
                player.gameObject.AddComponent<PlayerBuffs>();
            }
        }

        private void Start()
        {
            health = GetComponent<PlayerHealth>();
            controller = GetComponent<PlayerController>();

            waves = GameManager.Instance != null
                ? GameManager.Instance.WaveManager
                : FindAnyObjectByType<WaveManager>();

            if (waves == null)
            {
                return;
            }

            lastKills = waves.TotalKills;
            waves.WaveStarted += HandleWaveStarted;
            waves.WaveCompleted += HandleWaveCompleted;
            waves.KillsChanged += HandleKillsChanged;
        }

        public void Choose(BuffType type)
        {
            if (!AwaitingChoice)
            {
                return;
            }

            switch (type)
            {
                case BuffType.Vampirism:
                    VampirismLevel++;
                    health?.Heal(vampireHealPerKill);
                    break;

                case BuffType.Speed:
                    SpeedLevel++;
                    controller?.AddSpeedBonus(speedBonusPerPick);
                    break;

                case BuffType.Shield:
                    ShieldLevel++;
                    health?.SetShield(ShieldCapacity);
                    break;
            }

            AwaitingChoice = false;
            BuffsChanged?.Invoke();
            ChoiceResolved?.Invoke();
        }

        private void HandleWaveStarted(int waveNumber)
        {
            if (ShieldLevel > 0)
            {
                health?.SetShield(ShieldCapacity);
            }

            BuffsChanged?.Invoke();
        }

        private void HandleWaveCompleted(int waveNumber)
        {
            if (waveNumber < firstChoiceWave || (waveNumber - firstChoiceWave) % choiceEveryWaves != 0)
            {
                return;
            }

            AwaitingChoice = true;
            ChoiceRequested?.Invoke(waveNumber);
        }

        private void HandleKillsChanged(int totalKills)
        {
            int killed = Mathf.Max(0, totalKills - lastKills);
            lastKills = totalKills;

            if (killed <= 0 || VampirismLevel <= 0)
            {
                return;
            }

            health?.Heal(VampireHealPerKill * killed);
        }

        private void OnDestroy()
        {
            if (waves == null)
            {
                return;
            }

            waves.WaveStarted -= HandleWaveStarted;
            waves.WaveCompleted -= HandleWaveCompleted;
            waves.KillsChanged -= HandleKillsChanged;
        }
    }
}
