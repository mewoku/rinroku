using System.Collections.Generic;
using UnityEngine;

namespace Ronriku.Presentation.Accessibility
{
    /// <summary>
    /// Global juice: haptics + synthesized chiptune SFX for every interaction. Initialised once by the
    /// composition root; calls before that are silent no-ops (tests, edit mode).
    /// </summary>
    public static class Feedback
    {
        public enum Sfx { Tap, Move, Snap, Success, Error, Coin, Hit, Win, Lose, Whoosh }

        private const string SoundKey = "ronriku.sound";
        private static IHapticsService _haptics;
        private static AudioSource _source;
        private static readonly Dictionary<Sfx, AudioClip> Clips = new Dictionary<Sfx, AudioClip>();

        public static bool SoundEnabled
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
            set => PlayerPrefs.SetInt(SoundKey, value ? 1 : 0);
        }

        public static void Init(GameObject host, IHapticsService haptics)
        {
            _haptics = haptics;
            _source = host.GetComponent<AudioSource>();
            if (_source == null) _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            foreach (Sfx sfx in System.Enum.GetValues(typeof(Sfx))) Clips[sfx] = Synth.Build(sfx);
        }

        public static void Tap() { _haptics?.Selection(); Play(Sfx.Tap, 0.35f); }
        public static void Move() { _haptics?.Selection(); Play(Sfx.Move, 0.45f); }
        public static void Snap() { _haptics?.Selection(); Play(Sfx.Snap, 0.5f); }
        public static void Success() { _haptics?.Success(); Play(Sfx.Success, 0.7f); }
        public static void Error() { _haptics?.Error(); Play(Sfx.Error, 0.6f); }
        public static void Coin() { _haptics?.Selection(); Play(Sfx.Coin, 0.5f); }
        public static void Hit() { _haptics?.Error(); Play(Sfx.Hit, 0.7f); }
        public static void Win() { _haptics?.Success(); Play(Sfx.Win, 0.8f); }
        public static void Lose() { _haptics?.Error(); Play(Sfx.Lose, 0.7f); }
        public static void Whoosh() => Play(Sfx.Whoosh, 0.35f);

        private static void Play(Sfx sfx, float volume)
        {
            if (_source == null || !SoundEnabled || !Clips.TryGetValue(sfx, out var clip)) return;
            _source.pitch = 1f + Random.Range(-0.03f, 0.03f);
            _source.PlayOneShot(clip, volume * RonrikuTuning.Current.sfxVolume);
        }

        /// <summary>Tiny chiptune synthesizer: square / triangle / noise voices with linear envelopes.</summary>
        private static class Synth
        {
            private const int Rate = 22050;

            public static AudioClip Build(Sfx sfx)
            {
                switch (sfx)
                {
                    case Sfx.Tap: return Make(sfx, 0.04f, (t, n) => Square(t, 880) * Env(n, 0.04f));
                    case Sfx.Move: return Make(sfx, 0.07f, (t, n) => Square(t, Lerp(520, 780, n / 0.07f)) * Env(n, 0.07f));
                    case Sfx.Snap: return Make(sfx, 0.05f, (t, n) => Triangle(t, 1320) * Env(n, 0.05f));
                    case Sfx.Coin: return Make(sfx, 0.14f, (t, n) => Square(t, n < 0.05f ? 988 : 1319) * Env(n, 0.14f));
                    case Sfx.Success: return Arpeggio(sfx, new[] { 523f, 659f, 784f, 1047f }, 0.07f);
                    case Sfx.Win: return Arpeggio(sfx, new[] { 523f, 659f, 784f, 1047f, 784f, 1047f, 1319f }, 0.08f);
                    case Sfx.Error: return Make(sfx, 0.18f, (t, n) => Square(t, n < 0.09f ? 220 : 165) * Env(n, 0.18f));
                    case Sfx.Lose: return Arpeggio(sfx, new[] { 392f, 330f, 262f, 196f }, 0.1f);
                    case Sfx.Hit: return Make(sfx, 0.16f, (t, n) => (Noise() * 0.7f + Square(t, Lerp(180, 60, n / 0.16f)) * 0.5f) * Env(n, 0.16f));
                    default: return Make(sfx, 0.2f, (t, n) => Noise() * 0.5f * Env(n, 0.2f) * (n / 0.2f));
                }
            }

            private static AudioClip Arpeggio(Sfx sfx, float[] notes, float step) =>
                Make(sfx, notes.Length * step, (t, n) =>
                {
                    int i = Mathf.Min(notes.Length - 1, (int)(n / step));
                    float local = n - i * step;
                    return Square(t, notes[i]) * Env(local, step) * 0.8f;
                });

            private static AudioClip Make(Sfx sfx, float seconds, System.Func<float, float, float> voice)
            {
                int count = Mathf.CeilToInt(seconds * Rate);
                var data = new float[count];
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)Rate;
                    data[i] = Mathf.Clamp(voice(t, t) * 0.35f, -1f, 1f);
                }
                var clip = AudioClip.Create("sfx-" + sfx, count, 1, Rate, false);
                clip.SetData(data, 0);
                return clip;
            }

            private static float Square(float t, float hz) => Mathf.Repeat(t * hz, 1f) < 0.5f ? 1f : -1f;
            private static float Triangle(float t, float hz) => 1f - 4f * Mathf.Abs(Mathf.Repeat(t * hz, 1f) - 0.5f);
            private static float Noise() => Random.value * 2f - 1f;
            private static float Env(float t, float length) => Mathf.Clamp01(1f - t / length) * Mathf.Clamp01(t / 0.004f);
            private static float Lerp(float a, float b, float t) => a + (b - a) * Mathf.Clamp01(t);
        }
    }
}
