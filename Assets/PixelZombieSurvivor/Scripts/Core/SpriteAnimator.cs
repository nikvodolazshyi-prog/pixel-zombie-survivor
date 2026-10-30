using System;
using UnityEngine;

namespace PixelZombieSurvivor.Core
{
    /// <summary>
    /// Lightweight frame-by-frame sprite animation. Gameplay code decides which
    /// clip to play, so the project does not need Animator Controller assets and
    /// pooled objects can be reconfigured at runtime.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(0.1f)] private float framesPerSecond = 8f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playOnAwake = true;

        private Sprite[] activeFrames;
        private float timer;
        private int frameIndex;
        private bool playing;

        /// <summary>Raised when a non-looping clip reaches its last frame.</summary>
        public event Action Completed;

        public bool HasFrames => activeFrames != null && activeFrames.Length > 0;
        public bool IsPlaying => playing;
        public Sprite[] Frames => activeFrames;
        public float FramesPerSecond => framesPerSecond;

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponent<SpriteRenderer>();
            }

            activeFrames = frames;
            if (playOnAwake)
            {
                Play();
            }
        }

        /// <summary>
        /// Switches to a clip. Calling it again with the same clip keeps the current
        /// frame, which lets gameplay poll state every frame without side effects.
        /// </summary>
        public void Configure(Sprite[] newFrames, float fps, bool shouldLoop = true)
        {
            if (newFrames == null || newFrames.Length == 0)
            {
                return;
            }

            bool sameClip = activeFrames == newFrames &&
                            Mathf.Approximately(framesPerSecond, fps) &&
                            loop == shouldLoop;

            activeFrames = newFrames;
            framesPerSecond = Mathf.Max(0.1f, fps);
            loop = shouldLoop;

            if (sameClip)
            {
                if (!playing)
                {
                    Resume();
                }

                return;
            }

            Play();
        }

        public void Play()
        {
            if (!HasFrames)
            {
                return;
            }

            frameIndex = 0;
            timer = 0f;
            playing = true;
            ApplyFrame();
        }

        public void Pause()
        {
            playing = false;
        }

        public void Resume()
        {
            if (HasFrames)
            {
                playing = true;
            }
        }

        private void Update()
        {
            if (!playing || activeFrames == null || activeFrames.Length < 2)
            {
                return;
            }

            timer += Time.deltaTime;
            float interval = 1f / Mathf.Max(0.1f, framesPerSecond);
            while (timer >= interval)
            {
                timer -= interval;
                Advance();
                if (!playing)
                {
                    break;
                }
            }
        }

        private void Advance()
        {
            frameIndex++;
            if (frameIndex < activeFrames.Length)
            {
                ApplyFrame();
                return;
            }

            if (loop)
            {
                frameIndex = 0;
                ApplyFrame();
                return;
            }

            frameIndex = activeFrames.Length - 1;
            ApplyFrame();
            playing = false;
            Completed?.Invoke();
        }

        private void ApplyFrame()
        {
            if (target == null || activeFrames == null || activeFrames.Length == 0)
            {
                return;
            }

            target.sprite = activeFrames[Mathf.Clamp(frameIndex, 0, activeFrames.Length - 1)];
        }
    }
}
