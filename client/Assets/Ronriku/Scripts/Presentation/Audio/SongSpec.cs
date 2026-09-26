namespace Ronriku.Presentation.Audio
{
    internal enum GrooveStyle { NuDisco, GFunk, JazzFunk, ElectroFunk, SynthFunk, BossFunk, HeroDisco }
    internal enum LeadVoice { Pulse, Whine }

    /// <summary>A chord as a root (semitones above the key tonic) plus intervals above that root.</summary>
    internal readonly struct Chord
    {
        public readonly int Root;
        public readonly int[] Tones;
        public Chord(int root, params int[] tones) { Root = root; Tones = tones; }

        public static Chord Maj(int root) => new Chord(root, 0, 4, 7);
        public static Chord Min(int root) => new Chord(root, 0, 3, 7);
        public static Chord Maj7(int root) => new Chord(root, 0, 4, 7, 11);
        public static Chord Maj9(int root) => new Chord(root, 0, 4, 7, 11, 14);
        public static Chord Min7(int root) => new Chord(root, 0, 3, 7, 10);
        public static Chord Min9(int root) => new Chord(root, 0, 3, 7, 10, 14);
        public static Chord Dom7(int root) => new Chord(root, 0, 4, 7, 10);
        public static Chord Dom9(int root) => new Chord(root, 0, 4, 7, 10, 14);
        public static Chord Dom13(int root) => new Chord(root, 0, 4, 10, 14, 21);
        public static Chord Dom7Sharp9(int root) => new Chord(root, 0, 4, 10, 15);

        public int Third => Tones.Length > 1 ? Tones[1] : 4;
        /// <summary>The chord's 7th if it has one, otherwise a flat 7 (the funk default).</summary>
        public int Seventh
        {
            get
            {
                foreach (int t in Tones) if (t == 10 || t == 11) return t;
                return 10;
            }
        }
    }

    /// <summary>
    /// Rhythm patterns that make a groove. Strings are 16 steps (one bar of 16ths) unless noted.
    /// Kick/hat/clav: 'x' hit, 'g' ghost, '.' rest.
    /// Bass: R root, O octave pop, F fifth, 7 seventh, 3 third, x dead/ghost note, b chromatic approach to the
    /// next chord, '-' hold, '.' rest. Brass: 32 steps (two bars), 'x' stab, '-' hold.
    /// </summary>
    internal sealed class Groove
    {
        public string[] Kick;            // 4 bars
        public string LightHat;          // base-layer hats
        public string OpenHat;           // drive layer
        public string GhostSnare;        // drive layer, 'g' ghost, 'x' accent snare
        public string[] Bass;            // [0] bars 1-3, [1] turnaround bar 4
        public string Clav;              // wah clav/guitar stabs, 'g' = muted scratch
        public string Brass;             // 32 steps
        public float[] HatAccent = { 0.95f, 0.35f, 0.62f, 0.45f };
        internal static readonly float[] DiscoAccent = { 0.6f, 0.32f, 0.8f, 0.42f }; // let the open hat own the offbeat
        public float KickDecay = 0.115f; // longer = 808-style
        public float KickPitch = 50f;
        public float ArpLevel;           // 0 = no chip arp in the drive layer
    }

    /// <summary>Everything that gives a track its character. Pure data; the renderer turns it into audio.</summary>
    internal sealed class SongSpec
    {
        public MusicTrack Track;
        public GrooveStyle Style;
        public Groove Groove;
        public float Bpm;
        public uint Seed;
        public int Tonic;              // pitch class of the key (0 = C)
        public int[] Scale;            // 7 semitone offsets from the tonic
        public Chord[] Progression;    // one chord per bar, 4 bars
        public float Swing;            // fraction of a 16th that odd 16ths are delayed; 0.1..0.2 = 55..60 % swing
        public float Pump = 0.5f;      // sidechain depth on the ducked bus
        public float LeadBright = 1f;  // lead filter scaling
        public int LeadOctave = 5;     // octave of the lead's home register
        public LeadVoice Lead = LeadVoice.Pulse;
        public float WahQ = 4f;        // resonance of the clav wah
        public bool Minor => Scale[2] == 3;
        /// <summary>Swing as the classic ratio: where the off 16th lands inside an 8th (0.5 = straight).</summary>
        public float SwingRatio => 0.5f + Swing * 0.5f;
    }

    /// <summary>The fixed catalogue of tracks. Seeds make generation deterministic.</summary>
    internal static class SongBook
    {
        private static readonly int[] Ionian = { 0, 2, 4, 5, 7, 9, 11 };
        private static readonly int[] Aeolian = { 0, 2, 3, 5, 7, 8, 10 };
        private static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
        private static readonly int[] HarmonicMinor = { 0, 2, 3, 5, 7, 8, 11 };

        public static readonly MusicTrack[] All =
        {
            MusicTrack.Menu, MusicTrack.World1, MusicTrack.World2, MusicTrack.World3, MusicTrack.World4,
            MusicTrack.World5, MusicTrack.Boss, MusicTrack.Daily, MusicTrack.HeroRun,
        };

        /// <summary>Tracks meant to swing and groove (everything except the straight Boss / HeroRun drivers).</summary>
        public static bool IsFunk(MusicTrack t) => t != MusicTrack.Boss && t != MusicTrack.HeroRun && t != MusicTrack.None;

        public static Groove GrooveFor(GrooveStyle style)
        {
            switch (style)
            {
                case GrooveStyle.NuDisco: // four on the floor, octave disco bass, offbeat open hats, chicken-scratch guitar
                    return new Groove
                    {
                        Kick = new[] { "x...x...x...x...", "x...x...x...x...", "x...x...x...x...", "x...x...x...x..x" },
                        LightHat = "..x...x...x...x.",
                        OpenHat = "..x...x...x...x.",
                        GhostSnare = ".......g.g....g.",
                        Bass = new[] { "R.O.xRO.R.O.xRO.", "R.O.xRO.R.O.7.Ob" },
                        Clav = "g.x.gx.xg.x.gx.x",
                        Brass = "x-.............." + "..........x.x.x-",
                    };
                case GrooveStyle.GFunk: // laid back, heavy shuffle, long bass notes, sparse clav, whiny lead
                    return new Groove
                    {
                        Kick = new[] { "x......x..x.....", "x.x....x..x..x..", "x......x..x.....", "x.x....x..x...x." },
                        LightHat = "x.x.x.x.x.x.x.x.",
                        OpenHat = "..............x.",
                        GhostSnare = "......g..g....g.",
                        Bass = new[] { "R--..R.7--.F.R..", "R--..R.O-.F.3.Rb" },
                        Clav = "....x.....x.x...",
                        Brass = "x--............." + "............x-x-",
                        KickDecay = 0.15f, KickPitch = 46f,
                    };
                case GrooveStyle.JazzFunk: // busy syncopated bass, broken kick, ghost-note heavy snare, comping clav
                    return new Groove
                    {
                        Kick = new[] { "x.....x...x.....", "x.x...x....x....", "x.....x...x.....", "x.x...x....x..x." },
                        LightHat = "x.x.x.x.x.x.x.x.",
                        OpenHat = "......x.......x.",
                        GhostSnare = ".g.....g.gg...g.",
                        Bass = new[] { "R.xR.O7.xR.3F.xO", "R.xR.O7.3.F.O.7b" },
                        Clav = "x..x..x.x..x.x..",
                        Brass = "..x..x-........." + "x.x.....x..x.x--",
                    };
                case GrooveStyle.ElectroFunk: // robotic 808 syncopation, cowbell, square bass
                    return new Groove
                    {
                        Kick = new[] { "x..x..x...x.....", "x..x..x...x..x.x", "x..x..x...x.....", "x..x..x...x.xx.x" },
                        LightHat = "x.xxx.xxx.xxx.xx",
                        OpenHat = "..........x.....",
                        GhostSnare = "..........g...g.",
                        Bass = new[] { "R..R..O...R.x.O.", "R..R..O...R.F.7b" },
                        Clav = ".x..x..x.x..x...",
                        Brass = "x..x..x........." + "..........x..x--",
                        KickDecay = 0.14f, KickPitch = 48f,
                    };
                case GrooveStyle.SynthFunk: // slap-and-pop bass, driving stabs, disco hats, chip arp sparkle
                    return new Groove
                    {
                        Kick = new[] { "x...x...x...x...", "x...x...x..xx...", "x...x...x...x...", "x...x...x..x..x." },
                        LightHat = "..x...x...x...x.",
                        OpenHat = "..x...x...x...x.",
                        GhostSnare = ".......g.g..g..g",
                        Bass = new[] { "R.xOR.xO.RxO.R7O", "R.xOR.xO.R.F.3.b" },
                        Clav = "x.gx.gx.x.gx.gxx",
                        Brass = "x-..x-.........." + "........x.x..x--",
                        ArpLevel = 0.5f,
                    };
                case GrooveStyle.BossFunk: // intense: driving kick, rolling slap 16ths, stabs everywhere
                    return new Groove
                    {
                        Kick = new[] { "x...x...x...x...", "x...x...x...x.x.", "x...x...x...x...", "x...x...x..xx.x." },
                        LightHat = "..x...x...x...x.",
                        OpenHat = "..x...x...x...x.",
                        GhostSnare = ".g.....g.g.g...g",
                        Bass = new[] { "RxOxRxOxRxO7RxOx", "RxOxRxOxR7O7F7Ob" },
                        Clav = "x.x.gx.xx.x.gx.x",
                        Brass = "x-.x-.x-........" + "x-.x-.x-....x.x.",
                        ArpLevel = 0.8f,
                    };
                default: // HeroDisco: a clean kick on every beat (hero moves sync to it), bass off the beat
                    return new Groove
                    {
                        Kick = new[] { "x...x...x...x...", "x...x...x...x...", "x...x...x...x...", "x...x...x...x..." },
                        LightHat = "..x...x...x...x.",
                        OpenHat = "..x...x...x...x.",
                        GhostSnare = ".......g......g.",
                        Bass = new[] { ".xO..RO..xO..RO.", ".xO..RO..xO.7.Ob" },
                        Clav = "..x.g.x...x.g.xg",
                        Brass = "x-.............." + "..........x.x.x-",
                        ArpLevel = 0.7f,
                    };
            }
        }

        public static SongSpec Get(MusicTrack track)
        {
            SongSpec spec;
            switch (track)
            {
                case MusicTrack.Menu: // synth-funk, relaxed: D dorian i9 - IV13 vamp
                    spec = new SongSpec
                    {
                        Style = GrooveStyle.SynthFunk, Bpm = 112, Seed = 0x4D454E55, Tonic = 2, Scale = Dorian,
                        Progression = new[] { Chord.Min9(0), Chord.Dom13(5), Chord.Min9(0), Chord.Dom9(5) },
                        Swing = 0.14f, Pump = 0.4f, LeadBright = 0.75f,
                    };
                    break;
                case MusicTrack.World1: // nu-disco, bright: C Imaj9 - vi9 - ii9 - V13
                    spec = new SongSpec
                    {
                        Style = GrooveStyle.NuDisco, Bpm = 124, Seed = 0x57310001, Tonic = 0, Scale = Ionian,
                        Progression = new[] { Chord.Maj9(0), Chord.Min9(9), Chord.Min9(2), Chord.Dom13(7) },
                        Swing = 0.1f, Pump = 0.5f,
                    };
                    break;
                case MusicTrack.World2: // G-funk: A minor i9 - iv9 with a whiny portamento lead, heavy shuffle
                    spec = new SongSpec
                    {
                        Style = GrooveStyle.GFunk, Bpm = 104, Seed = 0x57320002, Tonic = 9, Scale = Dorian,
                        Progression = new[] { Chord.Min9(0), Chord.Dom9(5), Chord.Min9(0), Chord.Min7(7) },
                        Swing = 0.2f, Pump = 0.3f, Lead = LeadVoice.Whine, LeadOctave = 5, WahQ = 5f,
                    };
                    break;
                case MusicTrack.World3: // jazz-funk: D major ii9 - V13 - Imaj9 - vi9
                    spec = new SongSpec
                    {
                        Style = GrooveStyle.JazzFunk, Bpm = 114, Seed = 0x57330003, Tonic = 2, Scale = Ionian,
                        Progression = new[] { Chord.Min9(2), Chord.Dom13(7), Chord.Maj9(0), Chord.Min9(9) },
                        Swing = 0.16f, Pump = 0.3f, LeadBright = 0.9f,
                    };
                    break;
                case MusicTrack.World4: // electro-funk, dark: E minor i9 - VImaj7 - iv9 - V7#9
                    spec = new SongSpec
                    {
                        Style = GrooveStyle.ElectroFunk, Bpm = 120, Seed = 0x57340004, Tonic = 4, Scale = HarmonicMinor,
                        Progression = new[] { Chord.Min9(0), Chord.Maj7(8), Chord.Min9(5), Chord.Dom7Sharp9(7) },
                        Swing = 0.1f, Pump = 0.45f, LeadBright = 0.8f, LeadOctave = 4, WahQ = 5.5f,
                    };
                    break;
                case MusicTrack.World5: // synth-funk, heroic: Eb IVmaj9 - V13 - iii7 - vi9 (royal road)
                    spec = new SongSpec
                    {
                        Style = GrooveStyle.SynthFunk, Bpm = 126, Seed = 0x57350005, Tonic = 3, Scale = Ionian,
                        Progression = new[] { Chord.Maj9(5), Chord.Dom13(7), Chord.Min7(4), Chord.Min9(9) },
                        Swing = 0.12f, Pump = 0.5f,
                    };
                    break;
                case MusicTrack.Boss: // intense funk-rock: C# harmonic minor i7 - VImaj7 - iv9 - V7#9
                    spec = new SongSpec
                    {
                        Style = GrooveStyle.BossFunk, Bpm = 160, Seed = 0x42055001, Tonic = 1, Scale = HarmonicMinor,
                        Progression = new[] { Chord.Min7(0), Chord.Maj7(8), Chord.Min9(5), Chord.Dom7Sharp9(7) },
                        Swing = 0.04f, Pump = 0.55f, WahQ = 3f,
                    };
                    break;
                case MusicTrack.Daily: // nu-disco boogie: G IVmaj9 - iii7 - ii9 - V13
                    spec = new SongSpec
                    {
                        Style = GrooveStyle.NuDisco, Bpm = 118, Seed = 0x44A11700, Tonic = 7, Scale = Ionian,
                        Progression = new[] { Chord.Maj9(5), Chord.Min7(4), Chord.Min9(2), Chord.Dom13(7) },
                        Swing = 0.16f, Pump = 0.45f, LeadBright = 0.85f,
                    };
                    break;
                case MusicTrack.HeroRun: // four-on-the-floor disco-funk: F dorian i9 - IV9 vamp, kick on every beat
                    spec = new SongSpec
                    {
                        Style = GrooveStyle.HeroDisco, Bpm = 140, Seed = 0x4E120140, Tonic = 5, Scale = Dorian,
                        Progression = new[] { Chord.Min9(0), Chord.Dom9(5), Chord.Min9(0), Chord.Dom13(10) },
                        Swing = 0.08f, Pump = 0.55f,
                    };
                    break;
                default:
                    return null;
            }
            spec.Track = track;
            spec.Groove = GrooveFor(spec.Style);
            if (spec.Groove.OpenHat == "..x...x...x...x.") spec.Groove.HatAccent = Groove.DiscoAccent;
            return spec;
        }
    }
}
