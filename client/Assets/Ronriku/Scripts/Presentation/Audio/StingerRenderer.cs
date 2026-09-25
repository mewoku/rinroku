using System;

namespace Ronriku.Presentation.Audio
{
    /// <summary>Short one-shot phrases in the key of the current track (mono, 22050 Hz, peak below 0.9).</summary>
    internal static class StingerRenderer
    {
        private const int Rate = Dsp.Rate;

        /// <param name="tonic">Pitch class of the key.</param>
        /// <param name="minor">Whether the key is minor (victory then uses the relative major).</param>
        public static float[] Render(MusicStinger stinger, int tonic, bool minor)
        {
            int majorRoot = 60 + ((minor ? tonic + 3 : tonic) % 12);
            if (majorRoot > 66) majorRoot -= 12;
            int minorRoot = 60 + ((minor ? tonic : tonic + 9) % 12);
            if (minorRoot > 66) minorRoot -= 12;

            switch (stinger)
            {
                case MusicStinger.Victory:
                {
                    // da-da-da-DAAA: 5 1 3 5 then a held major chord with vibrato, plus a snare flam
                    var buf = new float[(int)(1.5f * Rate)];
                    float s = 0.085f;
                    Tone(buf, 0f, s * 0.9f, majorRoot + 7, 0.7f, 0.25f);
                    Tone(buf, s, s * 0.9f, majorRoot + 12, 0.75f, 0.25f);
                    Tone(buf, 2 * s, s * 0.9f, majorRoot + 16, 0.8f, 0.25f);
                    Tone(buf, 3 * s, 0.95f, majorRoot + 19, 1f, 0.3f, vibrato: true);
                    Tone(buf, 3 * s, 0.95f, majorRoot + 12, 0.55f, 0.5f);
                    Tone(buf, 3 * s, 0.95f, majorRoot + 16, 0.5f, 0.5f);
                    Bass(buf, 3 * s, 0.9f, majorRoot - 12);
                    Noise(buf, 3 * s - 0.012f, 0.25f, 0.5f);
                    Noise(buf, 3 * s, 0.9f, 0.3f, crash: true);
                    return Finish(buf);
                }
                case MusicStinger.Defeat:
                {
                    // slow descending minor line that sags at the end
                    var buf = new float[(int)(1.7f * Rate)];
                    float s = 0.2f;
                    Tone(buf, 0f, s, minorRoot + 7, 0.8f, 0.5f);
                    Tone(buf, s, s, minorRoot + 6, 0.75f, 0.5f);
                    Tone(buf, 2 * s, s, minorRoot + 3, 0.7f, 0.5f);
                    Tone(buf, 3 * s, 0.9f, minorRoot, 0.8f, 0.5f, vibrato: true, sag: true);
                    Bass(buf, 3 * s, 0.9f, minorRoot - 12);
                    return Finish(buf);
                }
                case MusicStinger.LevelUp:
                {
                    // rapid two-octave arpeggio up with a sparkle on top
                    var buf = new float[(int)(0.95f * Rate)];
                    int[] iv = { 0, 4, 7, 12, 16, 19, 24 };
                    float s = 0.045f;
                    for (int i = 0; i < iv.Length; i++) Tone(buf, i * s, s * 1.3f, majorRoot + iv[i], 0.55f + 0.06f * i, 0.125f);
                    float end = iv.Length * s;
                    Tone(buf, end, 0.45f, majorRoot + 24, 0.9f, 0.25f, vibrato: true);
                    Tone(buf, end, 0.45f, majorRoot + 31, 0.35f, 0.5f);
                    Tone(buf, end + 0.06f, 0.3f, majorRoot + 36, 0.25f, 0.125f);
                    return Finish(buf);
                }
                default:
                {
                    // Combo: quick up-flick 1-5-8
                    var buf = new float[(int)(0.3f * Rate)];
                    Tone(buf, 0f, 0.05f, majorRoot + 12, 0.6f, 0.25f);
                    Tone(buf, 0.045f, 0.05f, majorRoot + 19, 0.7f, 0.25f);
                    Tone(buf, 0.09f, 0.16f, majorRoot + 24, 0.85f, 0.125f);
                    return Finish(buf);
                }
            }
        }

        private static void Tone(float[] buf, float at, float dur, float midi, float vel, float duty, bool vibrato = false, bool sag = false)
        {
            int start = (int)(at * Rate), len = (int)(dur * Rate), rel = (int)(0.06f * Rate);
            float phase = 0f, lp = 0f, lpA = Dsp.OnePole(3800f), held = 0f;
            for (int j = 0; j < len + rel && start + j < buf.Length; j++)
            {
                float t = j / (float)Rate;
                float pitch = midi;
                if (vibrato && t > 0.12f) pitch += 0.25f * MathF.Sin(Dsp.TwoPi * 5.5f * t);
                if (sag) pitch -= 1.5f * Math.Max(0f, (t - dur * 0.4f) / dur);
                float dt = Dsp.Mtof(pitch) / Rate;
                phase += dt; if (phase >= 1f) phase -= 1f;
                float x = Dsp.Pulse(phase, dt, duty);
                lp += lpA * (x - lp);
                float env;
                if (j < len) { env = (j < 40 ? j / 40f : 1f) * (0.75f + 0.25f * MathF.Exp(-t / 0.08f)); held = env; }
                else env = held * (1f - (j - len) / (float)rel);
                buf[start + j] += 0.22f * vel * lp * env;
            }
        }

        private static void Bass(float[] buf, float at, float dur, float midi)
        {
            int start = (int)(at * Rate), len = (int)(dur * Rate);
            float phase = 0f, dt = Dsp.Mtof(midi) / Rate;
            for (int j = 0; j < len && start + j < buf.Length; j++)
            {
                phase += dt; if (phase >= 1f) phase -= 1f;
                float env = (j < 40 ? j / 40f : 1f) * (1f - j / (float)len);
                buf[start + j] += 0.3f * Dsp.Triangle(phase) * env;
            }
        }

        private static void Noise(float[] buf, float at, float dur, float vel, bool crash = false)
        {
            int start = Math.Max(0, (int)(at * Rate)), len = (int)(dur * Rate);
            var rng = new Rng(0xC0FFEEu + (uint)start);
            float lp = 0f, lpA = Dsp.OnePole(crash ? 2600f : 900f), env = 1f, dec = Dsp.Decay(crash ? 0.3f : 0.06f);
            for (int j = 0; j < len && start + j < buf.Length; j++)
            {
                float x = rng.Bipolar();
                lp += lpA * (x - lp);
                buf[start + j] += 0.25f * vel * (x - lp) * env * (j < 20 ? j / 20f : 1f);
                env *= dec;
            }
        }

        private static float[] Finish(float[] buf)
        {
            int fade = Math.Min(buf.Length, 200);
            for (int i = 0; i < buf.Length; i++)
            {
                float v = 0.85f * Dsp.Sat(buf[i] / 0.85f);
                if (i >= buf.Length - fade) v *= (buf.Length - i) / (float)fade;
                buf[i] = v;
            }
            return buf;
        }
    }
}
