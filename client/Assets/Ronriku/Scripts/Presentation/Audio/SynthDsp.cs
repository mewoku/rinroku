using System;

namespace Ronriku.Presentation.Audio
{
    /// <summary>Deterministic xorshift RNG (independent of UnityEngine.Random).</summary>
    internal struct Rng
    {
        private uint _s;
        public Rng(uint seed) { _s = seed == 0 ? 0x9E3779B9u : seed; Next(); Next(); }

        public uint Next()
        {
            uint x = _s;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            _s = x;
            return x;
        }

        /// <summary>Uniform in [0,1).</summary>
        public float Value() => (Next() >> 8) * (1f / 16777216f);
        /// <summary>Uniform in [-1,1).</summary>
        public float Bipolar() => Value() * 2f - 1f;
        public int Range(int min, int maxExclusive) => min + (int)(Next() % (uint)(maxExclusive - min));
        public bool Chance(float p) => Value() < p;

        public static uint Hash(uint a, uint b)
        {
            uint h = a * 0x85EBCA6Bu ^ (b + 0x9E3779B9u + (a << 6) + (a >> 2));
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
            return h;
        }
    }

    /// <summary>RBJ biquad (direct form I). A struct so filter state is copied explicitly between work steps.</summary>
    internal struct Biquad
    {
        private float _b0, _b1, _b2, _a1, _a2, _x1, _x2, _y1, _y2;

        public static Biquad HighPass(float hz, float q)
        {
            float w = Dsp.TwoPi * hz / Dsp.Rate, cos = MathF.Cos(w), alpha = MathF.Sin(w) / (2f * q);
            float a0 = 1f + alpha;
            return new Biquad
            {
                _b0 = (1f + cos) / 2f / a0, _b1 = -(1f + cos) / a0, _b2 = (1f + cos) / 2f / a0,
                _a1 = -2f * cos / a0, _a2 = (1f - alpha) / a0,
            };
        }

        public float Process(float x)
        {
            float y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1; _x1 = x; _y2 = _y1; _y1 = y;
            return y;
        }
    }

    /// <summary>Band-limited-ish chiptune oscillators, one-pole filters and saturation.</summary>
    internal static class Dsp
    {
        public const int Rate = 22050;
        public const float TwoPi = 6.2831853f;

        public static float Mtof(float midi) => 440f * MathF.Pow(2f, (midi - 69f) / 12f);

        /// <summary>PolyBLEP residual; removes most aliasing from hard edges.</summary>
        public static float Blep(float t, float dt)
        {
            if (t < dt) { t /= dt; return t + t - t * t - 1f; }
            if (t > 1f - dt) { t = (t - 1f) / dt; return t * t + t + t + 1f; }
            return 0f;
        }

        public static float Saw(float phase, float dt) => 2f * phase - 1f - Blep(phase, dt);

        public static float Pulse(float phase, float dt, float duty)
        {
            float v = phase < duty ? 1f : -1f;
            v += Blep(phase, dt);
            float p2 = phase - duty; if (p2 < 0f) p2 += 1f;
            v -= Blep(p2, dt);
            return v - (2f * duty - 1f); // remove DC so narrow pulses don't thump when gated
        }

        public static float Triangle(float phase) => 1f - 4f * MathF.Abs(phase - 0.5f);

        /// <summary>Coefficient for y += a * (x - y) at the given cutoff.</summary>
        public static float OnePole(float hz)
        {
            if (hz >= Rate * 0.49f) return 1f;
            return 1f - MathF.Exp(-TwoPi * hz / Rate);
        }

        /// <summary>Per-sample multiplier for an exponential decay with time constant tau seconds.</summary>
        public static float Decay(float tau) => MathF.Exp(-1f / (tau * Rate));

        /// <summary>Rational tanh approximation; smooth soft clip, exactly ±1 beyond ±3.</summary>
        public static float Sat(float x)
        {
            if (x >= 3f) return 1f;
            if (x <= -3f) return -1f;
            float x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);
        }
    }
}
