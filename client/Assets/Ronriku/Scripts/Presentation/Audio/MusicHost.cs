using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Ronriku.Presentation.Audio
{
    /// <summary>
    /// Runtime driver behind <see cref="Music"/>: two decks of three looping stem sources (for crossfades),
    /// a frame-budgeted render scheduler, a 2-track clip cache, intensity layer fades, ducking, the beat clock
    /// and pause handling. Lives on its own child GameObject so it never shares AudioSources with SFX.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class MusicHost : MonoBehaviour
    {
        private const float CrossfadeSeconds = 0.6f;
        private const int CacheSize = 2;
        private const double ScheduleLead = 0.08;

        private sealed class Song
        {
            public MusicTrack Track;
            public SongRenderJob Job;          // non-null while rendering
            public readonly AudioClip[] Clips = new AudioClip[SongRenderJob.StemCount];
            public int SamplesPerBeat;
            public int BaseLength;
            public float BarSeconds;
            public int Tonic;
            public bool Minor;
            public bool Complete => Job == null;
        }

        private sealed class Deck
        {
            public Song Song;
            public readonly AudioSource[] Sources = new AudioSource[SongRenderJob.StemCount];
            public readonly bool[] Started = new bool[SongRenderJob.StemCount];
            public readonly float[] StemGain = new float[SongRenderJob.StemCount];
            public float Fade, FadeTarget, FadeSpeed;
            public double DspStart;
            public long Loops;
            public int LastTs;
            public int LastBeat;
        }

        private readonly Deck[] _decks = { new Deck(), new Deck() };
        private readonly List<Song> _cache = new List<Song>();
        private readonly Dictionary<int, AudioClip> _stingers = new Dictionary<int, AudioClip>();
        private readonly Stopwatch _watch = new Stopwatch();
        private AudioSource _stingerSource;
        private Song _rendering;
        private int _active = -1;
        private MusicTrack _requested = MusicTrack.None;
        private int _intensity = 1;
        private float _duckAmount, _duckSeconds, _duckTime = float.MaxValue;
        private bool _appPaused, _focusLost, _paused;
        private double _pauseDsp;

        /// <summary>Last measured wall time spent rendering a track (ms, summed over frames).</summary>
        internal double LastRenderMs { get; private set; }

        internal void Setup()
        {
            for (int d = 0; d < _decks.Length; d++)
                for (int s = 0; s < SongRenderJob.StemCount; s++)
                    _decks[d].Sources[s] = MakeSource();
            _stingerSource = MakeSource();
        }

        private AudioSource MakeSource()
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.loop = false;
            src.priority = 16; // music outranks SFX voices
            src.volume = 0f;
            return src;
        }

        // ================================================================== commands

        internal void RequestPlay(MusicTrack track)
        {
            _requested = track;
            Resolve();
        }

        internal void RequestStop(float fade)
        {
            _requested = MusicTrack.None;
            if (_active >= 0)
            {
                Deck d = _decks[_active];
                d.FadeTarget = 0f;
                d.FadeSpeed = 1f / fade;
                _active = -1;
            }
        }

        internal void SetIntensity(int level) => _intensity = level;

        internal void Duck(float amount, float seconds)
        {
            float current = CurrentDuck();
            if (amount * 1f < current) return;
            _duckAmount = amount;
            _duckSeconds = seconds;
            _duckTime = 0f;
            ApplyVolumes();
        }

        internal void PlayStinger(MusicStinger stinger, MusicTrack keyOf)
        {
            SongSpec spec = SongBook.Get(keyOf == MusicTrack.None ? MusicTrack.World1 : keyOf);
            int key = (int)stinger * 100 + spec.Tonic * 2 + (spec.Minor ? 1 : 0);
            if (!_stingers.TryGetValue(key, out AudioClip clip) || clip == null)
            {
                float[] data = StingerRenderer.Render(stinger, spec.Tonic, spec.Minor);
                clip = AudioClip.Create("music-stinger-" + stinger, data.Length, 1, Dsp.Rate, false);
                clip.SetData(data, 0);
                _stingers[key] = clip;
            }
            if (!Music.Enabled) return;
            float seconds = clip.length;
            switch (stinger)
            {
                case MusicStinger.Victory: Duck(0.75f, seconds + 0.3f); break;
                case MusicStinger.Defeat: Duck(0.8f, seconds + 0.4f); break;
                case MusicStinger.LevelUp: Duck(0.5f, seconds); break;
                default: Duck(0.25f, 0.3f); break;
            }
            _stingerSource.PlayOneShot(clip, Mathf.Clamp01(Music.Volume * 1.3f));
        }

        // ================================================================== clock

        internal bool IsPlaying(MusicTrack track)
        {
            Deck d = ActiveDeck(track);
            return d != null && d.Started[0] && AudioSettings.dspTime >= d.DspStart;
        }

        internal double SongBeats(MusicTrack track)
        {
            Deck d = ActiveDeck(track);
            return d == null ? 0.0 : Beats(d);
        }

        private Deck ActiveDeck(MusicTrack track)
        {
            if (_active < 0 || track == MusicTrack.None) return null;
            Deck d = _decks[_active];
            return d.Song != null && d.Song.Track == track ? d : null;
        }

        private double Beats(Deck d)
        {
            if (!d.Started[0] || d.Song == null) return 0.0;
            if (!_paused && AudioSettings.dspTime < d.DspStart) return 0.0;
            int ts = d.Sources[0].timeSamples;
            if (ts < d.LastTs - d.Song.BaseLength / 2) d.Loops++;
            d.LastTs = ts;
            return (d.Loops * (double)d.Song.BaseLength + ts) / d.Song.SamplesPerBeat;
        }

        // ================================================================== frame loop

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            PumpRender();
            Resolve();

            for (int i = 0; i < _decks.Length; i++)
            {
                Deck d = _decks[i];
                if (d.Song == null) continue;
                if (d.Fade != d.FadeTarget)
                {
                    d.Fade = Mathf.MoveTowards(d.Fade, d.FadeTarget, d.FadeSpeed * dt);
                    if (d.Fade <= 0f && d.FadeTarget <= 0f) { StopDeck(d); continue; }
                }
                if (i == _active)
                {
                    if (!_paused) JoinLateStems(d);
                    float rate = dt / Mathf.Max(0.25f, d.Song.BarSeconds * 0.75f);
                    for (int s = 0; s < SongRenderJob.StemCount; s++)
                    {
                        float target = s <= _intensity ? 1f : 0f;
                        d.StemGain[s] = Mathf.MoveTowards(d.StemGain[s], target, rate);
                    }
                }
            }

            if (_duckTime < _duckSeconds) _duckTime += dt;
            ApplyVolumes();

            if (_active >= 0 && !_paused)
            {
                Deck d = _decks[_active];
                double beats = Beats(d);
                if (d.Started[0] && AudioSettings.dspTime >= d.DspStart)
                {
                    int beat = (int)Math.Floor(beats);
                    if (beat - d.LastBeat > 4) d.LastBeat = beat - 1; // after a hitch, only fire the latest beat
                    while (d.LastBeat < beat)
                    {
                        d.LastBeat++;
                        Music.RaiseBeat(d.LastBeat);
                    }
                }
            }
        }

        /// <summary>Starts / crossfades decks and manages the render job so the requested track ends up playing.</summary>
        private void Resolve()
        {
            Deck active = _active >= 0 ? _decks[_active] : null;
            if (_requested == MusicTrack.None) { AbandonUnusedRender(); return; }
            if (active != null && active.Song.Track == _requested) return;

            Song cached = _cache.Find(s => s.Track == _requested);
            if (cached != null)
            {
                Touch(cached);
                StartDeck(cached);
                return;
            }

            if (_rendering != null && _rendering.Track != _requested) AbandonUnusedRender();
            if (_rendering == null)
            {
                SongSpec spec = SongBook.Get(_requested);
                var job = new SongRenderJob(spec);
                _rendering = new Song
                {
                    Track = _requested, Job = job, SamplesPerBeat = job.SamplesPerBeat,
                    BaseLength = job.LengthOf(0), BarSeconds = job.BarLen / (float)Dsp.Rate,
                    Tonic = spec.Tonic, Minor = spec.Minor,
                };
                LastRenderMs = 0;
            }

            // Cold start (nothing audible): begin with the calm layer as soon as it exists.
            if (_rendering.Track == _requested && active == null && _rendering.Clips[0] != null)
                StartDeck(_rendering);
        }

        private void AbandonUnusedRender()
        {
            if (_rendering == null) return;
            foreach (Deck d in _decks) if (d.Song == _rendering) return; // playing early: let it finish
            DestroyClips(_rendering);
            _rendering = null;
        }

        private void PumpRender()
        {
            if (_rendering == null) return;
            SongRenderJob job = _rendering.Job;
            // Tight budget while music is audible; a bit more when the player hears silence anyway.
            double budget = _active >= 0 ? 4.0 : 9.0;
            _watch.Restart();
            while (!job.Done && _watch.Elapsed.TotalMilliseconds < budget) job.Step();
            LastRenderMs += _watch.Elapsed.TotalMilliseconds;

            for (int s = 0; s < SongRenderJob.StemCount; s++)
            {
                if (_rendering.Clips[s] != null || !job.IsStemReady(s)) continue;
                float[] data = job.Stems[s];
                var clip = AudioClip.Create($"music-{_rendering.Track}-{s}", data.Length, 1, Dsp.Rate, false);
                clip.SetData(data, 0);
                _rendering.Clips[s] = clip;
                job.Stems[s] = null; // the clip owns the audio now
            }

            if (job.Done)
            {
                _rendering.Job = null;
                Song done = _rendering;
                _rendering = null;
                AddToCache(done);
            }
        }

        private void AddToCache(Song song)
        {
            _cache.Remove(song);
            _cache.Add(song);
            while (_cache.Count > CacheSize)
            {
                Song victim = null;
                foreach (Song s in _cache)
                {
                    if (s == song) continue;
                    if (_active >= 0 && _decks[_active].Song == s) continue;
                    victim = s;
                    break;
                }
                if (victim == null) break;
                foreach (Deck d in _decks) if (d.Song == victim) StopDeck(d);
                _cache.Remove(victim);
                DestroyClips(victim);
            }
        }

        private void Touch(Song song)
        {
            _cache.Remove(song);
            _cache.Add(song);
        }

        private static void DestroyClips(Song song)
        {
            for (int s = 0; s < song.Clips.Length; s++)
            {
                if (song.Clips[s] != null) Destroy(song.Clips[s]);
                song.Clips[s] = null;
            }
        }

        private void StartDeck(Song song)
        {
            int incoming = _active == 0 ? 1 : 0;
            if (_active < 0 && _decks[0].Song != null && _decks[1].Song == null) incoming = 1;
            Deck d = _decks[incoming];
            if (d.Song != null) StopDeck(d);

            bool cold = _active < 0;
            if (!cold)
            {
                Deck old = _decks[_active];
                old.FadeTarget = 0f;
                old.FadeSpeed = 1f / CrossfadeSeconds;
            }

            d.Song = song;
            d.DspStart = AudioSettings.dspTime + ScheduleLead;
            d.Loops = 0;
            d.LastTs = 0;
            d.LastBeat = -1;
            d.Fade = 0f;
            d.FadeTarget = 1f;
            d.FadeSpeed = cold ? 25f : 1f / CrossfadeSeconds;
            for (int s = 0; s < SongRenderJob.StemCount; s++)
            {
                d.Started[s] = false;
                d.StemGain[s] = s <= _intensity ? 1f : 0f;
                AudioClip clip = song.Clips[s];
                if (clip == null) continue;
                AudioSource src = d.Sources[s];
                src.clip = clip;
                src.loop = true;
                src.volume = 0f;
                src.timeSamples = 0;
                src.PlayScheduled(d.DspStart);
                d.Started[s] = true;
            }
            _active = incoming;
            ApplyVolumes();
        }

        /// <summary>Stems that finished rendering after the deck started join sample-aligned and fade in.</summary>
        private void JoinLateStems(Deck d)
        {
            for (int s = 1; s < SongRenderJob.StemCount; s++)
            {
                AudioClip clip = d.Song.Clips[s];
                if (d.Started[s] || clip == null) continue;
                double start = Math.Max(AudioSettings.dspTime + 0.1, d.DspStart);
                long offset = (long)Math.Round((start - d.DspStart) * Dsp.Rate) % clip.samples;
                AudioSource src = d.Sources[s];
                src.clip = clip;
                src.loop = true;
                src.volume = 0f;
                src.timeSamples = (int)offset;
                src.PlayScheduled(start);
                d.Started[s] = true;
                d.StemGain[s] = 0f;
            }
        }

        private static void StopDeck(Deck d)
        {
            for (int s = 0; s < SongRenderJob.StemCount; s++)
            {
                d.Sources[s].Stop();
                d.Sources[s].clip = null;
                d.Sources[s].volume = 0f;
                d.Started[s] = false;
            }
            d.Song = null;
            d.Fade = d.FadeTarget = 0f;
        }

        // ================================================================== volume

        private float CurrentDuck()
        {
            if (_duckTime >= _duckSeconds) return 0f;
            float attack = Mathf.Clamp01(_duckTime / 0.03f);
            float release = Mathf.Clamp01((_duckSeconds - _duckTime) / (_duckSeconds * 0.7f));
            return _duckAmount * attack * release;
        }

        internal void ApplyVolumes()
        {
            float master = Music.Enabled ? Music.Volume : 0f;
            master *= 1f - CurrentDuck();
            foreach (Deck d in _decks)
            {
                if (d.Song == null) continue;
                float fade = Mathf.Sin(Mathf.Clamp01(d.Fade) * Mathf.PI * 0.5f);
                for (int s = 0; s < SongRenderJob.StemCount; s++)
                    if (d.Started[s]) d.Sources[s].volume = master * fade * d.StemGain[s];
            }
        }

        // ================================================================== pause

        private void OnApplicationPause(bool paused)
        {
            _appPaused = paused;
            UpdatePause();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (Application.isEditor) return; // clicking another editor window shouldn't stop the music
            _focusLost = !focused;
            UpdatePause();
        }

        private void UpdatePause()
        {
            bool want = _appPaused || _focusLost;
            if (want == _paused) return;
            _paused = want;
            if (want)
            {
                _pauseDsp = AudioSettings.dspTime;
                foreach (Deck d in _decks)
                    for (int s = 0; s < SongRenderJob.StemCount; s++)
                        if (d.Started[s]) d.Sources[s].Pause();
                _stingerSource.Pause();
            }
            else
            {
                double delta = Math.Max(0.0, AudioSettings.dspTime - _pauseDsp);
                foreach (Deck d in _decks)
                {
                    d.DspStart += delta;
                    for (int s = 0; s < SongRenderJob.StemCount; s++)
                        if (d.Started[s]) d.Sources[s].UnPause();
                }
                _stingerSource.UnPause();
            }
        }

        private void OnDestroy()
        {
            foreach (Song s in _cache) DestroyClips(s);
            if (_rendering != null) DestroyClips(_rendering);
            foreach (AudioClip c in _stingers.Values) if (c != null) Destroy(c);
            _cache.Clear();
            _stingers.Clear();
        }
    }
}
