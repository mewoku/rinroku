using System;
using UnityEngine;

namespace Ronriku.Presentation.Audio
{
    /// <summary>
    /// Procedural chiptune music with intensity layers and a sample-accurate beat clock. Initialised once by the
    /// composition root; every call before <see cref="Init"/> is a safe no-op (the last requested track and
    /// intensity are remembered and start on Init). Tracks are rendered lazily on first Play, spread over
    /// frames, and the last two are cached. When <see cref="Enabled"/> is false the music keeps running silently
    /// so the beat clock (and anything synced to it) still works.
    /// </summary>
    public static class Music
    {
        private const string EnabledKey = "ronriku.music";
        private const string VolumeKey = "ronriku.music.volume";
        public const float DefaultVolume = 0.55f;

        private static MusicHost _host;
        private static MusicTrack _current = MusicTrack.None;
        private static int _intensity = 1;
        private static bool? _enabled;
        private static float? _volume;

        /// <summary>Fired from the host's Update once per beat of the current track, with the beat index (0-based).</summary>
        public static event Action<int> Beat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _host = null;
            _current = MusicTrack.None;
            _intensity = 1;
            _enabled = null;
            _volume = null;
            Beat = null;
        }

        public static void Init(GameObject host)
        {
            if (host == null || _host != null) return;
            var go = new GameObject("RonrikuMusic");
            go.transform.SetParent(host.transform, false);
            _host = go.AddComponent<MusicHost>();
            _host.Setup();
            _host.SetIntensity(_intensity);
            if (_current != MusicTrack.None) _host.RequestPlay(_current);
        }

        /// <summary>Crossfades (~0.6 s) to <paramref name="track"/>; does nothing if it is already current.</summary>
        public static void Play(MusicTrack track)
        {
            if (track == MusicTrack.None) { Stop(); return; }
            if (track == _current) return;
            _current = track;
            if (_host != null) _host.RequestPlay(track);
        }

        public static void Stop(float fade = 0.5f)
        {
            _current = MusicTrack.None;
            if (_host != null) _host.RequestStop(Mathf.Max(0.01f, fade));
        }

        /// <summary>0 calm (light drums, pad, bass), 1 drive (+ full drums, arp), 2 hype (+ lead, extra percussion, fills).</summary>
        public static void SetIntensity(int level)
        {
            _intensity = Mathf.Clamp(level, 0, 2);
            if (_host != null) _host.SetIntensity(_intensity);
        }

        public static int Intensity => _intensity;

        /// <summary>Plays a short phrase in the key of the current track and briefly ducks the music under it.</summary>
        public static void Stinger(MusicStinger stinger)
        {
            if (_host != null) _host.PlayStinger(stinger, _current);
        }

        public static bool Enabled
        {
            get
            {
                if (_enabled == null) _enabled = PlayerPrefs.GetInt(EnabledKey, 1) == 1;
                return _enabled.Value;
            }
            set
            {
                _enabled = value;
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                if (_host != null) _host.ApplyVolumes();
            }
        }

        public static float Volume
        {
            get
            {
                if (_volume == null) _volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, DefaultVolume));
                return _volume.Value;
            }
            set
            {
                _volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, _volume.Value);
                if (_host != null) _host.ApplyVolumes();
            }
        }

        public static MusicTrack Current => _current;

        /// <summary>Exact tempo of the current track (sample-quantised, within 0.05 BPM of the nominal tempo); 0 if none.</summary>
        public static float Bpm => _current == MusicTrack.None ? 0f : (float)SongRenderJob.EffectiveBpm(SongBook.Get(_current));

        public static float SecondsPerBeat => Bpm > 0f ? 60f / Bpm : 0f;

        /// <summary>True once the current track is audible (rendered and started).</summary>
        public static bool IsPlaying => _host != null && _host.IsPlaying(_current);

        /// <summary>Continuous beat position of the current track, from AudioSource.timeSamples; 0 if none / not started yet.</summary>
        public static double SongBeats => _host != null ? _host.SongBeats(_current) : 0.0;

        /// <summary>0..1 position within the current beat.</summary>
        public static float BeatPhase
        {
            get
            {
                double b = SongBeats;
                return (float)(b - Math.Floor(b));
            }
        }

        /// <summary>Briefly lowers the music by <paramref name="amount"/> (0..1), recovering over <paramref name="seconds"/>.</summary>
        public static void Duck(float amount, float seconds)
        {
            if (_host != null) _host.Duck(Mathf.Clamp01(amount), Mathf.Max(0.05f, seconds));
        }

        internal static void RaiseBeat(int index) => Beat?.Invoke(index);
    }
}
