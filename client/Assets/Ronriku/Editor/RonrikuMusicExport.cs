using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Ronriku.Presentation.Audio;
using UnityEditor;
using UnityEngine;

namespace Ronriku.Editor
{
    /// <summary>Renders every track at every intensity (two full 8-bar loops) plus the stingers to 16-bit WAVs.</summary>
    public static class RonrikuMusicExport
    {
        [MenuItem("RONRIKU/Export Music WAVs")]
        public static void Export()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "evidence", "audio"));
            Directory.CreateDirectory(dir);
            var log = new StringBuilder("Exported music to " + dir + "\n");
            long total = 0;

            foreach (MusicTrack track in SongBook.All)
            {
                var watch = Stopwatch.StartNew();
                SongRenderJob job = SongRenderJob.RenderAll(SongBook.Get(track));
                watch.Stop();
                int samples = job.LengthOf(2) * 2;
                for (int level = 0; level <= 2; level++)
                {
                    string path = Path.Combine(dir, $"music_{track.ToString().ToLowerInvariant()}_i{level}.wav");
                    total += WriteWav(path, job.Mixdown(level, samples));
                }
                log.AppendLine($"  {track}: {job.Bpm:F2} bpm, render {watch.Elapsed.TotalMilliseconds:F0} ms, {samples / (double)SongRenderJob.Rate:F1} s per file");
            }

            foreach (MusicStinger s in Enum.GetValues(typeof(MusicStinger)))
            {
                string path = Path.Combine(dir, $"stinger_{s.ToString().ToLowerInvariant()}.wav");
                total += WriteWav(path, StingerRenderer.Render(s, 2, true));
            }

            log.AppendLine($"  total {total / 1048576.0:F2} MB");
            UnityEngine.Debug.Log(log.ToString());
        }

        private static long WriteWav(string path, float[] data)
        {
            const int rate = SongRenderJob.Rate;
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(stream))
            {
                int bytes = data.Length * 2;
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + bytes);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1);      // PCM
                w.Write((short)1);      // mono
                w.Write(rate);
                w.Write(rate * 2);      // byte rate
                w.Write((short)2);      // block align
                w.Write((short)16);     // bits
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(bytes);
                foreach (float v in data) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * 32767f));
                return stream.Length;
            }
        }
    }
}
