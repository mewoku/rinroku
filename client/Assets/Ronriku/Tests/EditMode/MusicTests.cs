using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Ronriku.Presentation.Audio;

namespace Ronriku.Tests
{
    public sealed class MusicTests
    {
        private static readonly Dictionary<MusicTrack, SongRenderJob> Rendered = new Dictionary<MusicTrack, SongRenderJob>();

        private static SongRenderJob Render(MusicTrack track)
        {
            if (!Rendered.TryGetValue(track, out SongRenderJob job))
            {
                job = SongRenderJob.RenderAll(SongBook.Get(track));
                Rendered[track] = job;
            }
            return job;
        }

        private static ulong Hash(SongRenderJob job)
        {
            ulong h = 1469598103934665603UL;
            foreach (float[] stem in job.Stems)
                foreach (float v in stem)
                {
                    h ^= (ulong)BitConverter.SingleToInt32Bits(v);
                    h *= 1099511628211UL;
                }
            return h;
        }

        [Test]
        public void EveryTrack_Renders_WholeBars_NoNaN_NoClipping_SaneLoudness()
        {
            // warm the JIT so the first track's timing is honest
            SongRenderJob.RenderAll(SongBook.Get(MusicTrack.World1));

            var report = new System.Text.StringBuilder("Music render report (editor, Mono):\n");
            foreach (MusicTrack track in SongBook.All)
            {
                SongSpec spec = SongBook.Get(track);
                var watch = Stopwatch.StartNew();
                var job = SongRenderJob.RenderAll(spec);
                watch.Stop();
                Rendered[track] = job;

                Assert.That(Math.Abs(job.Bpm - spec.Bpm), Is.LessThan(0.1), $"{track} tempo");
                Assert.That(job.BarLen, Is.EqualTo(4 * job.SamplesPerBeat));
                double exactBar = 4 * 60.0 / job.Bpm * SongRenderJob.Rate;
                for (int s = 0; s < SongRenderJob.StemCount; s++)
                {
                    float[] stem = job.Stems[s];
                    Assert.That(stem, Is.Not.Null, $"{track} stem {s}");
                    double bars = stem.Length / exactBar;
                    Assert.That(Math.Abs(bars - Math.Round(bars)), Is.LessThan(1e-6), $"{track} stem {s} whole bars");
                    Assert.That(Math.Round(bars), Is.EqualTo(s == 2 ? 8 : 4));
                    Assert.That(Math.Abs(stem.Length / exactBar * 4 * 60 / spec.Bpm - stem.Length / (double)SongRenderJob.Rate),
                        Is.LessThan(0.05), $"{track} loop length vs nominal BPM");
                    foreach (float v in stem) Assert.That(float.IsNaN(v) || float.IsInfinity(v), Is.False, $"{track} stem {s} NaN");
                    // seamless: the wrap-around step is no bigger than a normal sample step
                    Assert.That(Math.Abs(stem[stem.Length - 1] - stem[0]), Is.LessThan(0.25f), $"{track} stem {s} loop seam");
                }

                int loop = job.LengthOf(2);
                report.Append($"  {track,-8} bpm {job.Bpm:F2}  render {watch.Elapsed.TotalMilliseconds,6:F1} ms  float data {job.TotalSamples * 4 / 1048576.0:F2} MB ");
                for (int level = 0; level <= 2; level++)
                {
                    float[] mix = job.Mixdown(level, loop);
                    double peak = 0, sum = 0;
                    foreach (float v in mix) { peak = Math.Max(peak, Math.Abs(v)); sum += v * v; }
                    double rms = Math.Sqrt(sum / mix.Length);
                    report.Append($" | L{level} peak {peak:F3} rms {rms:F3}");
                    Assert.That(peak, Is.LessThanOrEqualTo(1.0), $"{track} level {level} peak");
                    Assert.That(rms, Is.InRange(0.03, 0.35), $"{track} level {level} rms");
                }
                report.AppendLine();
                Assert.That(job.TotalSamples * 4, Is.LessThanOrEqualTo(3.2 * 1024 * 1024), $"{track} memory");
            }
            UnityEngine.Debug.Log(report.ToString());
        }

        [Test]
        public void Rendering_IsDeterministic_AndTracksDiffer()
        {
            var hashes = new HashSet<ulong>();
            foreach (MusicTrack track in SongBook.All)
            {
                ulong a = Hash(Render(track));
                ulong b = Hash(SongRenderJob.RenderAll(SongBook.Get(track)));
                Assert.That(b, Is.EqualTo(a), $"{track} deterministic");
                Assert.That(hashes.Add(a), Is.True, $"{track} differs from other tracks");
            }
        }

        [Test]
        public void HeroRun_HasAKickOnEveryBeat()
        {
            var job = Render(MusicTrack.HeroRun);
            float[] stem = job.Stems[0];
            int beats = stem.Length / job.SamplesPerBeat;
            int window = SongRenderJob.Rate / 50; // 20 ms
            for (int b = 0; b < beats; b++)
            {
                int at = b * job.SamplesPerBeat;
                double onBeat = Energy(stem, at, window);
                double offBeat = Energy(stem, at + job.SamplesPerBeat / 2 + window, window);
                Assert.That(onBeat, Is.GreaterThan(offBeat * 2.0), $"beat {b}");
            }
        }

        [Test]
        public void FunkTracks_Swing_Between55And60Percent_WithExtendedChords()
        {
            foreach (MusicTrack track in SongBook.All)
            {
                if (!SongBook.IsFunk(track)) continue;
                SongSpec spec = SongBook.Get(track);
                Assert.That(spec.SwingRatio, Is.InRange(0.549f, 0.601f), $"{track} swing");
                int extended = 0;
                foreach (Chord c in spec.Progression) if (c.Tones.Length >= 4) extended++;
                Assert.That(extended, Is.GreaterThanOrEqualTo(3), $"{track} 7th/9th voicings");
                Groove g = spec.Groove;
                Assert.That(g.GhostSnare.Contains("g"), Is.True, $"{track} ghost snares");
                Assert.That(g.Bass[0].Contains("O") || g.Bass[0].Contains("7"), Is.True, $"{track} octave pops");
                Assert.That(g.Clav.Contains("x"), Is.True, $"{track} clav stabs");
                Assert.That(g.Brass.Contains("x"), Is.True, $"{track} brass stabs");
            }
        }

        [Test]
        public void Worlds_EachHaveADistinctGroove()
        {
            var styles = new HashSet<GrooveStyle>();
            foreach (MusicTrack t in new[] { MusicTrack.World1, MusicTrack.World2, MusicTrack.World3, MusicTrack.World4, MusicTrack.World5 })
                Assert.That(styles.Add(SongBook.Get(t).Style), Is.True, $"{t} groove repeats");
        }

        [Test]
        public void SwungTracks_PlaceOffSixteenthHatsLate()
        {
            foreach (MusicTrack track in new[] { MusicTrack.World2, MusicTrack.World3, MusicTrack.Daily })
            {
                var job = Render(track);
                float[] drive = job.Stems[1];
                int offset = (int)(job.Spec.Swing * job.StepLen);
                Assert.That(offset, Is.GreaterThan(100), $"{track} swing offset");
                double early = 0, late = 0;
                for (int step = 1; step < drive.Length / job.StepLen; step += 2)
                {
                    int straight = step * job.StepLen;
                    early += HighEnergy(drive, straight, offset);
                    late += HighEnergy(drive, straight + offset, offset);
                }
                // the swung hat starts at straight + offset, so the high band is much busier after it than before it
                Assert.That(late, Is.GreaterThan(early * 1.5), $"{track} hats swing late");
            }
        }

        [Test]
        public void HeroRun_StaysStraightFourOnTheFloor_At140()
        {
            SongSpec spec = SongBook.Get(MusicTrack.HeroRun);
            Assert.That(SongRenderJob.EffectiveBpm(spec), Is.EqualTo(140.0).Within(0.1));
            foreach (string bar in spec.Groove.Kick)
                for (int beat = 0; beat < 4; beat++) Assert.That(bar[beat * 4], Is.EqualTo('x'));
        }

        [Test]
        public void Stingers_Render_InRange()
        {
            foreach (MusicStinger s in Enum.GetValues(typeof(MusicStinger)))
                foreach (bool minor in new[] { false, true })
                {
                    float[] data = StingerRenderer.Render(s, 2, minor);
                    Assert.That(data.Length, Is.GreaterThan(1000));
                    float peak = 0f;
                    foreach (float v in data)
                    {
                        Assert.That(float.IsNaN(v), Is.False);
                        peak = Math.Max(peak, Math.Abs(v));
                    }
                    Assert.That(peak, Is.InRange(0.05f, 1f), $"{s} peak");
                }
        }

        [Test]
        public void Facade_IsSafeBeforeInit()
        {
            Assert.DoesNotThrow(() =>
            {
                Music.SetIntensity(2);
                Music.Duck(0.5f, 0.2f);
                Music.Stinger(MusicStinger.Combo);
                Assert.That(Music.SongBeats, Is.EqualTo(0.0));
                Assert.That(Music.BeatPhase, Is.EqualTo(0f));
                Music.Stop();
                Assert.That(Music.Current, Is.EqualTo(MusicTrack.None));
                Assert.That(Music.Bpm, Is.EqualTo(0f));
            });
        }

        private static double HighEnergy(float[] data, int from, int count)
        {
            double sum = 0;
            for (int i = 0; i < count; i++)
            {
                int k = (from + i) % data.Length;
                float d = data[k] - data[(k + data.Length - 1) % data.Length]; // first difference ~ high-pass
                sum += d * d;
            }
            return sum;
        }

        private static double Energy(float[] data, int from, int count)
        {
            double sum = 0;
            for (int i = 0; i < count; i++) { float v = data[(from + i) % data.Length]; sum += v * v; }
            return sum;
        }
    }
}
