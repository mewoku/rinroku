using System;
using System.Collections.Generic;

namespace Ronriku.Presentation.Audio
{
    /// <summary>
    /// Renders one track into three seamless, sample-locked stems (mono, 22050 Hz). Pure C#: no Unity API,
    /// no threads, so it works on WebGL and in EditMode tests. Work is split into small steps so the host can
    /// spread rendering over frames; <see cref="RenderAll"/> runs it synchronously.
    ///
    /// Stem 0 "base"  (4 bars): kick, light hats, pumping bass, pumping pad          -> intensity 0
    /// Stem 1 "drive" (4 bars): clap/snare, 16th hats, open hats, echoed arp         -> added at intensity 1
    /// Stem 2 "hype"  (8 bars): echoed lead (AABA-ish), shaker/perc, crash, riser+fill -> added at intensity 2
    /// Each stem is soft-limited to its own ceiling and the ceilings sum to 1.0, so any layer mix peaks below 1.
    /// Every note wraps around the loop end, so decays, echoes and the pump cross the loop point seamlessly.
    /// </summary>
    internal sealed class SongRenderJob
    {
        public const int Rate = Dsp.Rate;
        public const int StemCount = 3;
        public const int BarsBase = 4;
        public const int BarsHype = 8;
        internal static readonly float[] Ceilings = { 0.5f, 0.25f, 0.25f };

        private enum Kind { Kick, Hat, OpenHat, Clap, Snare, Shaker, Crash, Tom, Riser, Blip }

        private struct Note
        {
            public int Start, Len;
            public float Pitch, Vel;
            public Kind Kind;
            public float Aux;
        }

        public readonly SongSpec Spec;
        public readonly int SamplesPerBeat, StepLen, BarLen;
        public readonly double Bpm;
        public readonly float[][] Stems = new float[StemCount][];
        private readonly bool[] _ready = new bool[StemCount];
        private readonly List<Action> _work = new List<Action>();
        private int _next;
        private float[] _scratch;
        private float[] _duck;
        private readonly int _swingOffset;

        private readonly List<Note> _kicks = new List<Note>();
        private readonly List<Note> _lightHats = new List<Note>();
        private readonly List<Note> _bass = new List<Note>();
        private readonly List<Note> _pad = new List<Note>();
        private readonly List<Note> _driveDrums = new List<Note>();
        private readonly List<Note> _arp = new List<Note>();
        private readonly List<Note> _lead = new List<Note>();
        private readonly List<Note> _perc = new List<Note>();

        public SongRenderJob(SongSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            // Integer samples per 16th keeps every bar/beat boundary on an exact sample.
            StepLen = StepSamples(spec.Bpm);
            SamplesPerBeat = StepLen * 4;
            BarLen = SamplesPerBeat * 4;
            Bpm = Rate * 60.0 / SamplesPerBeat;
            _swingOffset = (int)(spec.Swing * StepLen);
            Compose();
            BuildWork();
        }

        public static int StepSamples(float bpm) => Math.Max(1, (int)Math.Round(Rate * 60.0 / bpm / 4.0));
        public static double EffectiveBpm(SongSpec spec) => spec == null ? 0.0 : Rate * 60.0 / (StepSamples(spec.Bpm) * 4);

        public int LengthOf(int stem) => (stem == 2 ? BarsHype : BarsBase) * BarLen;
        public int TotalSamples => LengthOf(0) + LengthOf(1) + LengthOf(2);
        public bool Done => _next >= _work.Count;
        public bool IsStemReady(int stem) => _ready[stem];
        public float Progress => _work.Count == 0 ? 1f : _next / (float)_work.Count;

        /// <summary>Runs one small unit of work. Returns true when everything is rendered.</summary>
        public bool Step()
        {
            if (_next < _work.Count) _work[_next++]();
            return Done;
        }

        public static SongRenderJob RenderAll(SongSpec spec)
        {
            var job = new SongRenderJob(spec);
            while (!job.Step()) { }
            return job;
        }

        /// <summary>Sum of the layers audible at <paramref name="level"/>, tiled to <paramref name="samples"/>.</summary>
        public float[] Mixdown(int level, int samples)
        {
            var output = new float[samples];
            for (int s = 0; s < StemCount; s++)
            {
                if (s > level) break;
                float[] stem = Stems[s];
                int len = stem.Length;
                for (int i = 0; i < samples; i++) output[i] += stem[i % len];
            }
            return output;
        }

        // ================================================================== composition

        private int StepPos(int step) => step * StepLen + ((step & 1) == 1 ? _swingOffset : 0);

        private int Wrap(int pos, int len)
        {
            pos %= len;
            return pos < 0 ? pos + len : pos;
        }

        private void Compose()
        {
            var rng = new Rng(Spec.Seed);
            ComposeDrums(ref rng);
            ComposeBass();
            ComposePad();
            ComposeArp();
            ComposeLead(ref rng);
            ComposePerc(ref rng);
        }

        private static readonly float[] HatAccent = { 0.9f, 0.38f, 0.62f, 0.42f };

        private void ComposeDrums(ref Rng rng)
        {
            string[] kick;
            switch (Spec.Drums)
            {
                case DrumStyle.Breakbeat:
                    kick = new[] { "x.........x.....", "x.x.......x..x..", "x.........x.....", "x.x.......x...x." };
                    break;
                case DrumStyle.Drive:
                    kick = new[] { "x...x...x...x...", "x...x...x...x.x.", "x...x...x...x...", "x...x...x..xx.x." };
                    break;
                default:
                    kick = new[] { "x...x...x...x...", "x...x...x...x...", "x...x...x...x...", "x...x...x...x..." };
                    break;
            }

            bool breaks = Spec.Drums == DrumStyle.Breakbeat;
            for (int bar = 0; bar < BarsBase; bar++)
            {
                for (int s = 0; s < 16; s++)
                {
                    int step = bar * 16 + s;
                    int pos = StepPos(step);
                    if (kick[bar][s] == 'x')
                        _kicks.Add(new Note { Start = pos, Vel = s % 4 == 0 ? 1f : 0.82f, Kind = Kind.Kick });

                    // light hats in the base stem
                    bool lightHat = breaks ? s % 2 == 0 : s % 4 == 2;
                    if (lightHat)
                        _lightHats.Add(new Note { Start = pos, Vel = (breaks ? (s % 4 == 0 ? 0.55f : 0.75f) : 0.8f) + rng.Bipolar() * 0.06f, Kind = Kind.Hat });

                    // drive stem: backbeat, 16th hats, open hats
                    if (s == 4 || s == 12)
                        _driveDrums.Add(new Note { Start = pos, Vel = 1f, Kind = breaks ? Kind.Snare : Kind.Clap });
                    if (breaks && ((s == 7 && bar % 2 == 1) || (s == 9) || (s == 15 && bar == 3)))
                        _driveDrums.Add(new Note { Start = pos, Vel = 0.28f + rng.Value() * 0.08f, Kind = Kind.Snare });

                    bool open = breaks ? (s == 14 && bar % 2 == 1) : s % 4 == 2;
                    if (open)
                        _driveDrums.Add(new Note { Start = pos, Vel = 0.8f, Kind = Kind.OpenHat });
                    else
                        _driveDrums.Add(new Note { Start = pos, Vel = HatAccent[s % 4] + rng.Bipolar() * 0.07f, Kind = Kind.Hat });
                }
            }
        }

        private void ComposeBass()
        {
            string pattern;
            switch (Spec.Bass)
            {
                case BassStyle.Offbeat: pattern = "..R-..O-..R-..O-"; break;
                case BassStyle.Rolling: pattern = ".RRO.RRO.RRO.ROF"; break;
                case BassStyle.Gallop: pattern = "R-RRO-RRR-RRO-OO"; break;
                case BassStyle.Funk: pattern = "R..O..R.R.O..FO."; break;
                default: pattern = "R-O-R-O-R-O-R-O-"; break;
            }

            for (int bar = 0; bar < BarsBase; bar++)
            {
                Chord chord = Spec.Progression[bar % Spec.Progression.Length];
                int root = 40 + ((Spec.Tonic + chord.Root) % 12 + 8) % 12; // E2..D#3: audible on phone speakers
                for (int s = 0; s < 16; s++)
                {
                    char c = pattern[s];
                    if (c == '.' || c == '-') continue;
                    int len = 1;
                    while (s + len < 16 && pattern[s + len] == '-') len++;
                    // walk up into the next chord on the last 16th of each bar
                    if (bar == BarsBase - 1 && s == 15 && Spec.Bass != BassStyle.Rolling) c = 'F';
                    int pitch = root + (c == 'O' ? 12 : c == 'F' ? 7 : 0);
                    int step = bar * 16 + s;
                    int start = StepPos(step);
                    int end = StepPos(step + len);
                    float vel = s % 4 == 0 ? 1f : (c == 'O' ? 0.92f : 0.8f);
                    _bass.Add(new Note { Start = start, Len = (int)((end - start) * 0.82f), Pitch = pitch, Vel = vel });
                }
            }
        }

        private void ComposePad()
        {
            for (int bar = 0; bar < BarsBase; bar++)
            {
                Chord chord = Spec.Progression[bar % Spec.Progression.Length];
                foreach (int iv in chord.Tones)
                {
                    int pc = (Spec.Tonic + chord.Root + iv) % 12;
                    int note = 57 + ((pc - 57 % 12) + 24) % 12; // close voicing in [57, 69)
                    _pad.Add(new Note { Start = bar * BarLen, Len = BarLen - StepLen, Pitch = note, Vel = iv == 0 ? 1f : 0.85f });
                }
            }
        }

        private void ComposeArp()
        {
            for (int bar = 0; bar < BarsBase; bar++)
            {
                Chord chord = Spec.Progression[bar % Spec.Progression.Length];
                int basePc = (Spec.Tonic + chord.Root) % 12;
                int baseNote = 64 + ((basePc - 64 % 12) + 24) % 12;
                var tones = new List<int>();
                for (int oct = 0; oct < 2; oct++)
                    foreach (int iv in chord.Tones) tones.Add(baseNote + iv + 12 * oct);
                int n = chord.Tones.Length, n2 = tones.Count;
                float duty = bar % 2 == 0 ? 0.125f : 0.25f;

                for (int s = 0; s < 16; s++)
                {
                    int idx;
                    switch (Spec.Arp)
                    {
                        case ArpStyle.UpDown:
                            int period = 2 * n2 - 2, k = s % period;
                            idx = k < n2 ? k : period - k;
                            break;
                        case ArpStyle.Broken: idx = ((s >> 1) + (s & 1) * 2) % n2; break;
                        case ArpStyle.PingPong: idx = (s >> 1) % n + ((s & 1) == 1 ? n : 0); break;
                        default: idx = s % n2; break;
                    }
                    int step = bar * 16 + s;
                    int start = StepPos(step);
                    _arp.Add(new Note
                    {
                        Start = start, Len = (int)(StepLen * 0.75f), Pitch = tones[idx],
                        Vel = s % 4 == 0 ? 1f : (s % 2 == 0 ? 0.72f : 0.55f), Aux = duty,
                    });
                }
            }
        }

        // ---------------------------------------------------------------- lead

        private struct MotifNote { public int Step, Len, Rel; public float Vel; }

        private static readonly string[] Cells =
        {
            "x---", "x-x-", "x.x.", "x-.x", ".x-x", "xxx-", "x..x", "x-xx", "..x-", "x.xx",
        };

        private static readonly int[] CellWeight = { 3, 4, 3, 3, 2, 2, 2, 2, 1, 2 };

        private List<(int step, int len)> MakeRhythm(ref Rng rng, bool response)
        {
            var sb = new char[32];
            for (int beat = 0; beat < 8; beat++)
            {
                string cell;
                if (beat == 6) cell = response ? "x-x-" : "x---";
                else if (beat == 7) cell = response ? "--.." : "....";
                else if (beat == 0) cell = response ? ".x-x" : "x-x-";
                else
                {
                    int total = 0; foreach (int w in CellWeight) total += w;
                    int r = rng.Range(0, total), i = 0;
                    while (r >= CellWeight[i]) { r -= CellWeight[i]; i++; }
                    cell = Cells[i];
                    if (beat == 3 && !response) cell = "x..."; // a breath mid-phrase
                }
                for (int j = 0; j < 4; j++) sb[beat * 4 + j] = cell[j];
            }
            var notes = new List<(int, int)>();
            for (int s = 0; s < 32; s++)
            {
                if (sb[s] != 'x') continue;
                int len = 1;
                while (s + len < 32 && sb[s + len] == '-') len++;
                notes.Add((s, len));
            }
            return notes;
        }

        private int ChordDegree(int bar)
        {
            Chord chord = Spec.Progression[bar % Spec.Progression.Length];
            int pc = chord.Root % 12;
            for (int i = 0; i < 7; i++) if (Spec.Scale[i] == pc) return i;
            int best = 0;
            for (int i = 0; i < 7; i++) if (Math.Abs(Spec.Scale[i] - pc) < Math.Abs(Spec.Scale[best] - pc)) best = i;
            return best;
        }

        /// <summary>Pitches a rhythm as scale-degree offsets relative to the chord root (so it can be sequenced).</summary>
        private List<MotifNote> PitchPhrase(ref Rng rng, List<(int step, int len)> rhythm, int barOffset, int startRel)
        {
            var result = new List<MotifNote>();
            int prevAbs = ChordDegree(barOffset) + startRel;
            int dir = 1;
            for (int i = 0; i < rhythm.Count; i++)
            {
                (int step, int len) = rhythm[i];
                int bar = barOffset + step / 16;
                int root = ChordDegree(bar);
                int abs;
                if (step % 4 == 0 || i == 0)
                {
                    // strong position: nearest chord tone, sometimes the next one in the contour direction
                    int best = int.MaxValue, second = int.MaxValue;
                    for (int oct = -1; oct <= 2; oct++)
                        foreach (int ct in new[] { 0, 2, 4 })
                        {
                            int cand = root + ct + oct * 7;
                            if (cand == prevAbs && i > 0) continue;
                            if (best == int.MaxValue || Math.Abs(cand - prevAbs) < Math.Abs(best - prevAbs)) { second = best; best = cand; }
                            else if (second == int.MaxValue || Math.Abs(cand - prevAbs) < Math.Abs(second - prevAbs)) second = cand;
                        }
                    abs = (second != int.MaxValue && Math.Sign(second - prevAbs) == dir && rng.Chance(0.45f)) ? second : best;
                }
                else
                {
                    int move = rng.Chance(0.7f) ? 1 : 2;
                    if (rng.Chance(0.2f)) dir = -dir;
                    abs = prevAbs + dir * move;
                }
                if (abs > 9) { abs -= 2; dir = -1; }
                if (abs < -2) { abs += 2; dir = 1; }
                if (i == rhythm.Count / 2) dir = -dir;
                prevAbs = abs;
                result.Add(new MotifNote { Step = step, Len = len, Rel = abs - root, Vel = step % 4 == 0 ? 1f : 0.82f + rng.Bipolar() * 0.06f });
            }
            return result;
        }

        private void ComposeLead(ref Rng rng)
        {
            var callRhythm = MakeRhythm(ref rng, false);
            var respRhythm = MakeRhythm(ref rng, true);
            var call = PitchPhrase(ref rng, callRhythm, 0, 2);
            var response = PitchPhrase(ref rng, respRhythm, 4, 4);

            // A (bars 0-1), A sequenced over the next chords (2-3), B response (4-5), A with a resolving end (6-7)
            EmitPhrase(call, 0, false);
            EmitPhrase(call, 2, false);
            EmitPhrase(response, 4, false);
            EmitPhrase(call, 6, true);
        }

        private void EmitPhrase(List<MotifNote> phrase, int barOffset, bool resolve)
        {
            for (int i = 0; i < phrase.Count; i++)
            {
                MotifNote m = phrase[i];
                int bar = barOffset + m.Step / 16;
                int root = ChordDegree(bar);
                int deg = root + m.Rel;
                if (resolve && i == phrase.Count - 1)
                {
                    // land on the chord root nearest to where the phrase was heading
                    deg = root + (m.Rel >= 4 ? 7 : 0);
                }
                int step = barOffset * 16 + m.Step;
                int start = StepPos(step);
                int end = StepPos(step + m.Len);
                int pitch = LocalPitch(deg, bar);
                _lead.Add(new Note { Start = start, Len = Math.Max(StepLen / 2, (int)((end - start) * 0.9f)), Pitch = pitch, Vel = m.Vel });
            }
        }

        /// <summary>Scale degree to MIDI, nudged onto chord tones that the scale doesn't contain (e.g. V in minor).</summary>
        private int LocalPitch(int degree, int bar)
        {
            int oct = (int)Math.Floor(degree / 7.0);
            int idx = degree - oct * 7;
            int midi = 12 * (Spec.LeadOctave + 1) + Spec.Tonic + Spec.Scale[idx] + 12 * oct;
            Chord chord = Spec.Progression[bar % Spec.Progression.Length];
            int pc = ((midi % 12) + 12) % 12;
            foreach (int iv in chord.Tones)
            {
                int ct = (Spec.Tonic + chord.Root + iv) % 12;
                bool inScale = Array.IndexOf(Spec.Scale, ((ct - Spec.Tonic) % 12 + 12) % 12) >= 0;
                if (inScale) continue;
                if ((ct - pc + 12) % 12 == 1) return midi + 1;
                if ((pc - ct + 12) % 12 == 1) return midi - 1;
            }
            return midi;
        }

        // ---------------------------------------------------------------- hype percussion

        private void ComposePerc(ref Rng rng)
        {
            bool breaks = Spec.Drums == DrumStyle.Breakbeat;
            for (int bar = 0; bar < BarsHype; bar++)
            {
                for (int s = 0; s < 16; s++)
                {
                    int step = bar * 16 + s;
                    int pos = StepPos(step);
                    bool fillZone = bar == BarsHype - 1 && s >= 8;
                    if (fillZone)
                    {
                        float ramp = (s - 8) / 7f;
                        _perc.Add(new Note { Start = pos, Vel = 0.35f + 0.6f * ramp, Kind = Kind.Snare });
                        if (s == 12 || s == 14) _perc.Add(new Note { Start = pos, Vel = 0.8f, Kind = Kind.Tom, Pitch = s == 12 ? 190f : 140f });
                        continue;
                    }
                    if (breaks)
                    {
                        if (s == 3 || s == 6 || s == 11 || (s == 14 && bar % 2 == 0))
                            _perc.Add(new Note { Start = pos, Vel = 0.7f + rng.Bipolar() * 0.1f, Kind = Kind.Blip, Pitch = s == 6 ? 1.5f : 1f });
                        _perc.Add(new Note { Start = pos, Vel = (s % 2 == 1 ? 0.7f : 0.35f) + rng.Bipolar() * 0.08f, Kind = Kind.Shaker });
                    }
                    else
                    {
                        _perc.Add(new Note { Start = pos, Vel = (s % 4 == 2 ? 0.85f : s % 2 == 1 ? 0.55f : 0.3f) + rng.Bipolar() * 0.08f, Kind = Kind.Shaker });
                        if (Spec.Drums == DrumStyle.Drive && (s == 3 || s == 10))
                            _perc.Add(new Note { Start = pos, Vel = 0.55f, Kind = Kind.Tom, Pitch = s == 3 ? 160f : 120f });
                    }
                }
            }
            _perc.Add(new Note { Start = 0, Vel = 1f, Kind = Kind.Crash });
            _perc.Add(new Note { Start = 4 * BarLen, Vel = 0.7f, Kind = Kind.Crash });
            _perc.Add(new Note { Start = (BarsHype - 1) * BarLen, Len = BarLen, Vel = 1f, Kind = Kind.Riser });
        }

        // ================================================================== work plan

        private void BuildWork()
        {
            int lenA = LengthOf(0), lenC = LengthOf(2);
            const int chunk = 16384;

            // ---- stem 0: base
            _work.Add(() => { Stems[0] = new float[lenA]; BuildDuckEnvelope(lenA); });
            AddNotes(_bass, 16, n => Bass(Stems[0], n));
            AddNotes(_pad, 3, n => Pad(Stems[0], n));
            AddChunks(lenA, chunk, (a, b) => ApplyDuck(Stems[0], a, b, 1f));
            AddNotes(_kicks, 8, n => Kick(Stems[0], n));
            AddNotes(_lightHats, 16, n => Drum(Stems[0], n));
            AddChunks(lenA, chunk, (a, b) => Limit(0, a, b));
            _work.Add(() => _ready[0] = true);

            // ---- stem 1: drive
            _work.Add(() => { Stems[1] = new float[lenA]; _scratch = new float[lenC]; });
            AddNotes(_arp, 16, n => Arp(_scratch, lenA, n));
            AddChunks(lenA, chunk, (a, b) => EchoInto(Stems[1], _scratch, lenA, a, b, 3 * StepLen, 0.32f, 3, 0.1f));
            AddChunks(lenA, chunk, (a, b) => ApplyDuck(Stems[1], a, b, 0.8f));
            AddNotes(_driveDrums, 16, n => Drum(Stems[1], n));
            AddChunks(lenA, chunk, (a, b) => Limit(1, a, b));
            _work.Add(() => _ready[1] = true);

            // ---- stem 2: hype
            _work.Add(() => { Stems[2] = new float[lenC]; Array.Clear(_scratch, 0, _scratch.Length); });
            AddNotes(_lead, 6, n => Lead(_scratch, n));
            AddChunks(lenC, chunk, (a, b) => EchoInto(Stems[2], _scratch, lenC, a, b, 3 * StepLen, 0.36f, 4, 0.35f));
            AddChunks(lenC, chunk, (a, b) => ApplyDuck(Stems[2], a, b, 0.45f));
            AddNotes(_perc, 16, n => Drum(Stems[2], n));
            AddChunks(lenC, chunk, (a, b) => Limit(2, a, b));
            _work.Add(() => { _ready[2] = true; _scratch = null; _duck = null; });
        }

        private void AddNotes(List<Note> notes, int perStep, Action<Note> render)
        {
            for (int i = 0; i < notes.Count; i += perStep)
            {
                int from = i, to = Math.Min(notes.Count, i + perStep);
                _work.Add(() => { for (int k = from; k < to; k++) render(notes[k]); });
            }
        }

        private void AddChunks(int length, int chunk, Action<int, int> process)
        {
            for (int a = 0; a < length; a += chunk)
            {
                int from = a, to = Math.Min(length, a + chunk);
                _work.Add(() => process(from, to));
            }
        }

        // ================================================================== bus processing

        private void BuildDuckEnvelope(int len)
        {
            _duck = new float[len];
            var starts = new List<int>();
            foreach (Note k in _kicks) starts.Add(Wrap(k.Start, len));
            starts.Sort();
            float release = SamplesPerBeat * 0.8f;
            float attack = Rate * 0.004f;
            for (int ki = 0; ki < starts.Count; ki++)
            {
                int s0 = starts[ki];
                int s1 = ki + 1 < starts.Count ? starts[ki + 1] : starts[0] + len;
                for (int p = s0; p < s1; p++)
                {
                    float t = p - s0;
                    float x = Math.Min(1f, t / release);
                    float a = Math.Min(1f, t / attack);
                    float r = 1f - x;
                    _duck[p % len] = a * r * r; // 1 = fully ducked
                }
            }
        }

        private void ApplyDuck(float[] buf, int from, int to, float amount)
        {
            float depth = Spec.Pump * amount;
            int dl = _duck.Length;
            for (int i = from; i < to; i++) buf[i] *= 1f - depth * _duck[i % dl];
        }

        private static void EchoInto(float[] dst, float[] src, int len, int from, int to, int delay, float feedback, int taps, float damp)
        {
            for (int i = from; i < to; i++)
            {
                float v = src[i];
                float g = 1f;
                int d = i;
                for (int t = 0; t < taps; t++)
                {
                    g *= feedback;
                    d -= delay;
                    if (d < 0) d += len;
                    // later repeats get softer and duller (cheap approximation: average with neighbour)
                    int d2 = d == 0 ? len - 1 : d - 1;
                    float s = src[d] * (1f - damp) + src[d2] * damp;
                    v += g * s;
                }
                dst[i] += v;
            }
        }

        /// <summary>Peak of each stem before its soft limiter (diagnostics: ~ceiling means gentle saturation).</summary>
        public readonly float[] PreLimitPeak = new float[StemCount];

        private void Limit(int stem, int from, int to)
        {
            float[] buf = Stems[stem];
            float ceiling = Ceilings[stem], inv = 1f / ceiling, peak = PreLimitPeak[stem];
            for (int i = from; i < to; i++)
            {
                float v = buf[i];
                float a = v < 0f ? -v : v;
                if (a > peak) peak = a;
                buf[i] = ceiling * Dsp.Sat(v * inv);
            }
            PreLimitPeak[stem] = peak;
        }

        // ================================================================== voices

        private uint NoteSeed(Note n) => Rng.Hash(Spec.Seed, (uint)n.Start * 31u + (uint)n.Kind);

        private void Kick(float[] buf, Note n)
        {
            int len = buf.Length;
            int count = (int)(0.34f * Rate);
            int w = Wrap(n.Start, len);
            float phase = 0f, amp = 1f, ampDecay = Dsp.Decay(0.115f), sweep = 1f, sweepDecay = Dsp.Decay(0.03f);
            var rng = new Rng(NoteSeed(n));
            float gain = 0.56f * n.Vel;
            for (int j = 0; j < count; j++)
            {
                float f = 50f + 160f * sweep;
                phase += f / Rate; if (phase >= 1f) phase -= 1f;
                float body = Dsp.Sat(MathF.Sin(Dsp.TwoPi * phase) * 1.6f);
                float att = j < 22 ? j / 22f : 1f;
                float click = j < 110 ? (rng.Bipolar() * 0.18f + (j < 40 ? 0.25f : 0f) * MathF.Sin(j * 0.55f)) * (1f - j / 110f) : 0f;
                float tail = j > count - 200 ? (count - j) / 200f : 1f;
                buf[w] += gain * (body * amp * att * tail + click);
                amp *= ampDecay; sweep *= sweepDecay;
                if (++w >= len) w = 0;
            }
        }

        private void Drum(float[] buf, Note n)
        {
            int len = buf.Length;
            int w = Wrap(n.Start, len);
            var rng = new Rng(NoteSeed(n));
            switch (n.Kind)
            {
                case Kind.Hat:
                case Kind.OpenHat:
                case Kind.Shaker:
                {
                    bool open = n.Kind == Kind.OpenHat, shaker = n.Kind == Kind.Shaker;
                    float tau = open ? 0.11f : shaker ? 0.028f : 0.022f;
                    int count = (int)(tau * 6f * Rate);
                    float gain = n.Vel * (open ? 0.15f : shaker ? 0.12f : 0.17f);
                    float lpA = Dsp.OnePole(shaker ? 3800f : 5200f), lp = 0f, dec = Dsp.Decay(tau), env = 1f;
                    float lp2A = Dsp.OnePole(9500f), lp2 = 0f;
                    int attack = shaker ? (int)(0.006f * Rate) : 4;
                    for (int j = 0; j < count; j++)
                    {
                        float x = rng.Bipolar();
                        lp += lpA * (x - lp);
                        float hp = x - lp;
                        lp2 += lp2A * (hp - lp2);
                        float a = j < attack ? j / (float)attack : 1f;
                        float tail = j > count - 64 ? (count - j) / 64f : 1f;
                        buf[w] += gain * lp2 * env * a * tail;
                        env *= dec;
                        if (++w >= len) w = 0;
                    }
                    break;
                }
                case Kind.Clap:
                case Kind.Snare:
                {
                    bool clap = n.Kind == Kind.Clap;
                    int count = (int)(0.3f * Rate);
                    float gain = n.Vel * 0.2f;
                    float hpA = Dsp.OnePole(clap ? 900f : 700f), hpLp = 0f, lpA = Dsp.OnePole(clap ? 3400f : 5200f), lp = 0f;
                    float tone = 0f, toneEnv = 1f, toneDec = Dsp.Decay(0.045f);
                    float tail = 1f, tailDec = Dsp.Decay(clap ? 0.075f : 0.06f);
                    float burstDec = Dsp.Decay(0.004f);
                    int b1 = (int)(0.009f * Rate), b2 = (int)(0.018f * Rate), tailStart = (int)(0.024f * Rate);
                    float burst = 1f;
                    for (int j = 0; j < count; j++)
                    {
                        float x = rng.Bipolar();
                        hpLp += hpA * (x - hpLp);
                        float band = x - hpLp;
                        lp += lpA * (band - lp);
                        float env;
                        if (clap)
                        {
                            if (j == b1 || j == b2) burst = 1f;
                            env = j < tailStart ? burst : 0.85f * tail;
                            burst *= burstDec;
                            if (j >= tailStart) tail *= tailDec;
                        }
                        else
                        {
                            env = tail;
                            tail *= tailDec;
                        }
                        tone += 185f / Rate; if (tone >= 1f) tone -= 1f;
                        float body = MathF.Sin(Dsp.TwoPi * tone) * toneEnv * (clap ? 0.35f : 0.6f);
                        toneEnv *= toneDec;
                        float end = j > count - 100 ? (count - j) / 100f : 1f;
                        buf[w] += gain * (lp * 1.6f * env + body) * end;
                        if (++w >= len) w = 0;
                    }
                    break;
                }
                case Kind.Crash:
                {
                    int count = (int)(1.6f * Rate);
                    float gain = n.Vel * 0.1f;
                    float lpA = Dsp.OnePole(2600f), lp = 0f, env = 1f, dec = Dsp.Decay(0.42f);
                    for (int j = 0; j < count; j++)
                    {
                        float x = rng.Bipolar();
                        lp += lpA * (x - lp);
                        float a = j < 30 ? j / 30f : 1f;
                        float end = j > count - 400 ? (count - j) / 400f : 1f;
                        buf[w] += gain * (x - lp) * env * a * end;
                        env *= dec;
                        if (++w >= len) w = 0;
                    }
                    break;
                }
                case Kind.Tom:
                {
                    int count = (int)(0.28f * Rate);
                    float gain = n.Vel * 0.24f, phase = 0f, env = 1f, dec = Dsp.Decay(0.1f), sweep = 1f, sweepDec = Dsp.Decay(0.05f);
                    for (int j = 0; j < count; j++)
                    {
                        float f = n.Pitch * (0.62f + 0.38f * sweep);
                        phase += f / Rate; if (phase >= 1f) phase -= 1f;
                        float a = j < 20 ? j / 20f : 1f;
                        float end = j > count - 100 ? (count - j) / 100f : 1f;
                        buf[w] += gain * Dsp.Triangle(phase) * env * a * end;
                        env *= dec; sweep *= sweepDec;
                        if (++w >= len) w = 0;
                    }
                    break;
                }
                case Kind.Blip:
                {
                    // chip "cowbell": two detuned pulses, short and bright
                    int count = (int)(0.12f * Rate);
                    float gain = n.Vel * 0.06f, p1 = 0f, p2 = 0f, env = 1f, dec = Dsp.Decay(0.035f);
                    float f1 = 587f * n.Pitch, f2 = 845f * n.Pitch, lpA = Dsp.OnePole(4000f), lp = 0f;
                    for (int j = 0; j < count; j++)
                    {
                        p1 += f1 / Rate; if (p1 >= 1f) p1 -= 1f;
                        p2 += f2 / Rate; if (p2 >= 1f) p2 -= 1f;
                        float x = Dsp.Pulse(p1, f1 / Rate, 0.5f) + Dsp.Pulse(p2, f2 / Rate, 0.5f);
                        lp += lpA * (x - lp);
                        float a = j < 15 ? j / 15f : 1f;
                        buf[w] += gain * lp * env * a;
                        env *= dec;
                        if (++w >= len) w = 0;
                    }
                    break;
                }
                case Kind.Riser:
                {
                    int count = n.Len;
                    float gain = n.Vel * 0.08f, lp = 0f, lp2 = 0f;
                    for (int j = 0; j < count; j++)
                    {
                        float t = j / (float)count;
                        float hz = 300f + 5200f * t * t;
                        float a = Dsp.OnePole(hz);
                        float x = rng.Bipolar();
                        lp += a * (x - lp);
                        lp2 += 0.5f * (x - lp - lp2);
                        float end = j > count - 60 ? (count - j) / 60f : 1f;
                        buf[w] += gain * lp2 * t * t * end;
                        if (++w >= len) w = 0;
                    }
                    break;
                }
            }
        }

        private void Bass(float[] buf, Note n)
        {
            int len = buf.Length;
            int release = (int)(0.012f * Rate);
            int count = n.Len + release;
            int w = Wrap(n.Start, len);
            float f = Dsp.Mtof(n.Pitch), dt = f / Rate;
            // pulse width drifts over the bar: a slow PWM feel across notes
            float barPhase = (n.Start % (BarLen * 2)) / (float)(BarLen * 2);
            float duty = 0.3f + 0.16f * MathF.Sin(Dsp.TwoPi * barPhase);
            float phase = 0f, lp1 = 0f, lp2 = 0f, a = 0f;
            float filt = 1f, filtDec = Dsp.Decay(0.07f);
            float gain = 0.17f * n.Vel;
            float cutBase = 380f, cutEnv = 2200f + 1200f * n.Vel, held = 0f;
            for (int j = 0; j < count; j++)
            {
                if ((j & 7) == 0) a = Dsp.OnePole(cutBase + cutEnv * filt);
                phase += dt; if (phase >= 1f) phase -= 1f;
                float x = Dsp.Pulse(phase, dt, duty) * 0.9f + Dsp.Triangle(phase) * 0.2f;
                lp1 += a * (x - lp1);
                lp2 += a * (lp1 - lp2);
                float env;
                if (j < n.Len) { env = (j < 30 ? j / 30f : 1f) * (1f - 0.2f * Math.Min(1f, j / (0.12f * Rate))); held = env; }
                else env = held * (1f - (j - n.Len) / (float)release);
                buf[w] += gain * Dsp.Sat(lp2 * 1.3f) * env;
                filt *= filtDec;
                if (++w >= len) w = 0;
            }
        }

        private void Pad(float[] buf, Note n)
        {
            int len = buf.Length;
            int release = (int)(0.28f * Rate);
            int attack = (int)(0.07f * Rate);
            int count = n.Len + release;
            int w = Wrap(n.Start, len);
            float f = Dsp.Mtof(n.Pitch);
            float d1 = f * 1.0045f / Rate, d2 = f * 0.9955f / Rate, d3 = f * 0.5f / Rate;
            float p1 = 0f, p2 = 0.37f, p3 = 0.61f, lp1 = 0f, lp2 = 0f, a = 0f;
            float gain = 0.034f * n.Vel;
            for (int j = 0; j < count; j++)
            {
                if ((j & 15) == 0)
                {
                    float t = j / (float)n.Len;
                    a = Dsp.OnePole(1500f + 900f * MathF.Max(0f, 1f - t) + 250f * MathF.Sin(Dsp.TwoPi * t * 2f));
                }
                p1 += d1; if (p1 >= 1f) p1 -= 1f;
                p2 += d2; if (p2 >= 1f) p2 -= 1f;
                p3 += d3; if (p3 >= 1f) p3 -= 1f;
                float x = Dsp.Saw(p1, d1) + Dsp.Saw(p2, d2) + 0.5f * Dsp.Pulse(p3, d3, 0.5f);
                lp1 += a * (x - lp1);
                lp2 += a * (lp1 - lp2);
                float env = j < attack ? j / (float)attack : (j < n.Len ? 1f : 1f - (j - n.Len) / (float)release);
                buf[w] += gain * lp2 * env * env;
                if (++w >= len) w = 0;
            }
        }

        private void Arp(float[] buf, int wrapLen, Note n)
        {
            int count = (int)(0.16f * Rate);
            int w = Wrap(n.Start, wrapLen);
            float f = Dsp.Mtof(n.Pitch), dt = f / Rate;
            float phase = 0f, lp = 0f, lpA = Dsp.OnePole(4800f), env = 1f, dec = Dsp.Decay(0.06f);
            float gain = 0.13f * n.Vel;
            for (int j = 0; j < count; j++)
            {
                phase += dt; if (phase >= 1f) phase -= 1f;
                float x = Dsp.Pulse(phase, dt, n.Aux);
                lp += lpA * (x - lp);
                float a = j < 12 ? j / 12f : 1f;
                float end = j > count - 50 ? (count - j) / 50f : 1f;
                buf[w] += gain * lp * env * a * end;
                env *= j < n.Len ? dec : dec * dec;
                if (++w >= wrapLen) w = 0;
            }
        }

        private float _lastLeadPitch = -1f;
        private int _lastLeadEnd = int.MinValue;

        private void Lead(float[] buf, Note n)
        {
            int len = buf.Length;
            int release = (int)(0.06f * Rate);
            int count = n.Len + release;
            int w = Wrap(n.Start, len);
            // glide in from the previous note when it was (nearly) legato
            float from = (_lastLeadPitch > 0f && n.Start - _lastLeadEnd < StepLen) ? _lastLeadPitch : n.Pitch;
            _lastLeadPitch = n.Pitch; _lastLeadEnd = n.Start + n.Len;
            int glide = (int)(0.035f * Rate);
            int vibDelay = (int)(0.16f * Rate);
            float p1 = 0f, p2 = 0.5f, lp = 0f, lp2 = 0f;
            float lpA = Dsp.OnePole(4300f * Spec.LeadBright), lp2A = Dsp.OnePole(7000f);
            float gain = 0.12f * n.Vel;
            float f1 = 0f, f2 = 0f, held = 0f, decay = 1f, decayMul = Dsp.Decay(0.12f);
            for (int j = 0; j < count; j++)
            {
                if ((j & 7) == 0)
                {
                    float pitch = j < glide ? from + (n.Pitch - from) * (j / (float)glide) : n.Pitch;
                    float t = j / (float)Rate;
                    if (j > vibDelay) pitch += 0.22f * Math.Min(1f, (j - vibDelay) / (0.15f * Rate)) * MathF.Sin(Dsp.TwoPi * 5.6f * t);
                    float hz = Dsp.Mtof(pitch);
                    f1 = hz / Rate; f2 = hz * 1.0058f / Rate;
                }
                float global = (n.Start + j) / (float)Rate;
                float duty = 0.3f + 0.17f * MathF.Sin(Dsp.TwoPi * 0.45f * global + j * 0.00012f);
                p1 += f1; if (p1 >= 1f) p1 -= 1f;
                p2 += f2; if (p2 >= 1f) p2 -= 1f;
                float x = Dsp.Pulse(p1, f1, duty) + 0.6f * Dsp.Pulse(p2, f2, 0.5f);
                lp += lpA * (x - lp);
                lp2 += lp2A * (lp - lp2);
                float env;
                if (j < n.Len)
                {
                    env = j < 60 ? j / 60f : 0.72f + 0.28f * decay;
                    if (j >= 60) decay *= decayMul;
                    held = env;
                }
                else env = held * (1f - (j - n.Len) / (float)release);
                buf[w] += gain * lp2 * env;
                if (++w >= len) w = 0;
            }
        }
    }
}
