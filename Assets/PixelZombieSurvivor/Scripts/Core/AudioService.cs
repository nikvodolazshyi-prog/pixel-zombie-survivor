using UnityEngine;

namespace PixelZombieSurvivor.Core
{
    /// <summary>
    /// Minimal procedural sound effects. Clips are synthesized at runtime so the demo
    /// stays asset-free: no audio files, no AudioMixer, just short PCM bursts.
    /// The service installs itself after the scene loads, so nothing has to be wired
    /// inside the arena scene.
    /// </summary>
    [DefaultExecutionOrder(-650)]
    public sealed class AudioService : SingletonBehaviour<AudioService>
    {
        private const int SampleRate = 44100;

        [Range(0f, 1f)] [SerializeField] private float masterVolume = 0.55f;

        private AudioSource source;
        private AudioClip shotClip;
        private AudioClip coinClip;
        private AudioClip hurtClip;
        private AudioClip dashClip;
        private AudioClip waveClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Instance != null)
            {
                return;
            }

            GameObject host = new GameObject("Audio Service");
            host.AddComponent<AudioService>();
        }

        protected override void Awake()
        {
            base.Awake();

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 1f;

            shotClip = NoiseBurst("sfx_shot", 0.085f, 42f, 0.85f, 130f);
            coinClip = Tone("sfx_coin", 0.10f, 1180f, 1560f, 26f, 0.5f);
            hurtClip = Tone("sfx_hurt", 0.16f, 180f, 90f, 16f, 0.8f);
            dashClip = NoiseBurst("sfx_dash", 0.16f, 18f, 0.45f, 60f);
            waveClip = Tone("sfx_wave", 0.32f, 520f, 780f, 5f, 0.4f);
        }

        public void PlayShot(float pitch = 1f)
        {
            Play(shotClip, Mathf.Clamp(pitch, 0.6f, 1.6f), 1f);
        }

        public void PlayCoin()
        {
            Play(coinClip, Random.Range(0.96f, 1.08f), 0.45f);
        }

        public void PlayHurt()
        {
            Play(hurtClip, Random.Range(0.94f, 1.06f), 0.8f);
        }

        public void PlayDash()
        {
            Play(dashClip, Random.Range(0.95f, 1.05f), 0.5f);
        }

        public void PlayWaveStart()
        {
            Play(waveClip, 1f, 0.5f);
        }

        private void Play(AudioClip clip, float pitch, float volume)
        {
            if (clip == null || source == null)
            {
                return;
            }

            source.pitch = pitch;
            source.PlayOneShot(clip, masterVolume * volume);
        }

        /// <summary>White noise with an exponential decay, used for shots and whooshes.</summary>
        private static AudioClip NoiseBurst(string name, float duration, float decay, float amplitude, float bodyHz)
        {
            return Build(name, duration, (time, _) =>
            {
                float envelope = Mathf.Exp(-time * decay);
                float noise = Random.Range(-1f, 1f);
                float body = Mathf.Sin(2f * Mathf.PI * bodyHz * time);
                return (noise * 0.7f + body * 0.3f) * envelope * amplitude;
            });
        }

        /// <summary>Short two-frequency tone with a linear pitch sweep.</summary>
        private static AudioClip Tone(
            string name,
            float duration,
            float startHz,
            float endHz,
            float decay,
            float amplitude)
        {
            return Build(name, duration, (time, progress) =>
            {
                float hz = Mathf.Lerp(startHz, endHz, progress);
                float envelope = Mathf.Exp(-time * decay);
                return Mathf.Sin(2f * Mathf.PI * hz * time) * envelope * amplitude;
            });
        }

        private static AudioClip Build(string name, float duration, System.Func<float, float, float> sample)
        {
            int sampleCount = Mathf.Max(16, Mathf.CeilToInt(duration * SampleRate));
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                data[i] = Mathf.Clamp(sample(time, i / (float)sampleCount), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
