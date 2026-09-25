namespace Ronriku.Presentation.Audio
{
    internal enum DrumStyle { FourFloor, Breakbeat, Drive }
    internal enum BassStyle { OctaveEighths, Offbeat, Rolling, Gallop, Funk }
    internal enum ArpStyle { Up, UpDown, Broken, PingPong }

    /// <summary>A chord as a root (semitones above the key tonic) plus intervals above that root.</summary>
    internal readonly struct Chord
    {
        public readonly int Root;
        public readonly int[] Tones;
        public Chord(int root, params int[] tones) { Root = root; Tones = tones; }

        public static Chord Maj(int root) => new Chord(root, 0, 4, 7);
        public static Chord Min(int root) => new Chord(root, 0, 3, 7);
        public static Chord Min7(int root) => new Chord(root, 0, 3, 7, 10);
        public static Chord Dom7(int root) => new Chord(root, 0, 4, 7, 10);
    }

    /// <summary>Everything that gives a track its character. Pure data; the renderer turns it into audio.</summary>
    internal sealed class SongSpec
    {
        public MusicTrack Track;
        public float Bpm;
        public uint Seed;
        public int Tonic;              // pitch class of the key (0 = C)
        public int[] Scale;            // 7 semitone offsets from the tonic
        public Chord[] Progression;    // one chord per bar, 4 bars
        public DrumStyle Drums;
        public BassStyle Bass;
        public ArpStyle Arp;
        public float Swing;            // 0..0.3 of a 16th, applied to odd 16ths
        public float Pump = 0.55f;     // sidechain depth on the ducked bus
        public float LeadBright = 1f;  // lead filter scaling
        public int LeadOctave = 5;     // octave of the lead's home register
        public bool Minor => Scale[2] == 3;
    }

    /// <summary>The fixed catalogue of tracks. Seeds make generation deterministic.</summary>
    internal static class SongBook
    {
        private static readonly int[] Ionian = { 0, 2, 4, 5, 7, 9, 11 };
        private static readonly int[] Aeolian = { 0, 2, 3, 5, 7, 8, 10 };
        private static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
        private static readonly int[] Phrygian = { 0, 1, 3, 5, 7, 8, 10 };
        private static readonly int[] HarmonicMinor = { 0, 2, 3, 5, 7, 8, 11 };

        public static readonly MusicTrack[] All =
        {
            MusicTrack.Menu, MusicTrack.World1, MusicTrack.World2, MusicTrack.World3, MusicTrack.World4,
            MusicTrack.World5, MusicTrack.Boss, MusicTrack.Daily, MusicTrack.HeroRun,
        };

        public static SongSpec Get(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Menu: // D minor, i-VI-III-VII, laid-back house drive
                    return new SongSpec
                    {
                        Track = track, Bpm = 112, Seed = 0x4D454E55, Tonic = 2, Scale = Aeolian,
                        Progression = new[] { Chord.Min(0), Chord.Maj(8), Chord.Maj(3), Chord.Maj(10) },
                        Drums = DrumStyle.FourFloor, Bass = BassStyle.Offbeat, Arp = ArpStyle.Broken,
                        Swing = 0.10f, Pump = 0.5f, LeadBright = 0.7f, LeadOctave = 5,
                    };
                case MusicTrack.World1: // C major, I-V-vi-IV, bright
                    return new SongSpec
                    {
                        Track = track, Bpm = 128, Seed = 0x57310001, Tonic = 0, Scale = Ionian,
                        Progression = new[] { Chord.Maj(0), Chord.Maj(7), Chord.Min(9), Chord.Maj(5) },
                        Drums = DrumStyle.FourFloor, Bass = BassStyle.OctaveEighths, Arp = ArpStyle.Up,
                        Swing = 0f, LeadOctave = 5,
                    };
                case MusicTrack.World2: // A minor, i-iv-VI-V, breakbeat
                    return new SongSpec
                    {
                        Track = track, Bpm = 132, Seed = 0x57320002, Tonic = 9, Scale = Aeolian,
                        Progression = new[] { Chord.Min(0), Chord.Min(5), Chord.Maj(8), Chord.Maj(7) },
                        Drums = DrumStyle.Breakbeat, Bass = BassStyle.OctaveEighths, Arp = ArpStyle.UpDown,
                        Swing = 0.04f, LeadOctave = 5,
                    };
                case MusicTrack.World3: // D dorian, i7-IV7-i7-VII, funky swing
                    return new SongSpec
                    {
                        Track = track, Bpm = 138, Seed = 0x57330003, Tonic = 2, Scale = Dorian,
                        Progression = new[] { Chord.Min7(0), Chord.Dom7(5), Chord.Min7(0), Chord.Maj(10) },
                        Drums = DrumStyle.Breakbeat, Bass = BassStyle.Funk, Arp = ArpStyle.PingPong,
                        Swing = 0.16f, Pump = 0.45f, LeadOctave = 5,
                    };
                case MusicTrack.World4: // E phrygian, i-II-III-II, dark rolling
                    return new SongSpec
                    {
                        Track = track, Bpm = 144, Seed = 0x57340004, Tonic = 4, Scale = Phrygian,
                        Progression = new[] { Chord.Min(0), Chord.Maj(1), Chord.Maj(3), Chord.Maj(1) },
                        Drums = DrumStyle.FourFloor, Bass = BassStyle.Rolling, Arp = ArpStyle.Broken,
                        Swing = 0f, Pump = 0.6f, LeadBright = 0.8f, LeadOctave = 4,
                    };
                case MusicTrack.World5: // Eb major, IV-V-iii-vi royal road, heroic gallop
                    return new SongSpec
                    {
                        Track = track, Bpm = 150, Seed = 0x57350005, Tonic = 3, Scale = Ionian,
                        Progression = new[] { Chord.Maj(5), Chord.Maj(7), Chord.Min(4), Chord.Min(9) },
                        Drums = DrumStyle.FourFloor, Bass = BassStyle.Gallop, Arp = ArpStyle.Up,
                        Swing = 0f, LeadOctave = 5,
                    };
                case MusicTrack.Boss: // C# harmonic minor, i-VI-iv-V, intense
                    return new SongSpec
                    {
                        Track = track, Bpm = 160, Seed = 0x42055001, Tonic = 1, Scale = HarmonicMinor,
                        Progression = new[] { Chord.Min(0), Chord.Maj(8), Chord.Min(5), Chord.Maj(7) },
                        Drums = DrumStyle.Drive, Bass = BassStyle.Rolling, Arp = ArpStyle.UpDown,
                        Swing = 0f, Pump = 0.6f, LeadOctave = 5,
                    };
                case MusicTrack.Daily: // G major, vi-IV-I-V, shuffled house
                    return new SongSpec
                    {
                        Track = track, Bpm = 120, Seed = 0x44A11700, Tonic = 7, Scale = Ionian,
                        Progression = new[] { Chord.Min(9), Chord.Maj(5), Chord.Maj(0), Chord.Maj(7) },
                        Drums = DrumStyle.FourFloor, Bass = BassStyle.OctaveEighths, Arp = ArpStyle.PingPong,
                        Swing = 0.14f, LeadBright = 0.85f, LeadOctave = 5,
                    };
                case MusicTrack.HeroRun: // F minor, i-VII-VI-VII, a kick on every beat
                    return new SongSpec
                    {
                        Track = track, Bpm = 140, Seed = 0x4E120140, Tonic = 5, Scale = Aeolian,
                        Progression = new[] { Chord.Min(0), Chord.Maj(10), Chord.Maj(8), Chord.Maj(10) },
                        Drums = DrumStyle.FourFloor, Bass = BassStyle.Offbeat, Arp = ArpStyle.UpDown,
                        Swing = 0f, Pump = 0.6f, LeadOctave = 5,
                    };
                default:
                    return null;
            }
        }
    }
}
