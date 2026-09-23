using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ronriku.Domain;
using Ronriku.Domain.Daily;
using Ronriku.Domain.Figures;
using Ronriku.Domain.Puzzles;
using UnityEditor;
using UnityEngine;

namespace Ronriku.Editor
{
    /// <summary>
    /// Exports golden fixtures from the C# domain (the source of truth) to packages/core/fixtures.
    /// The TypeScript port must reproduce every value exactly.
    /// </summary>
    public static class RonrikuFixtures
    {
        [MenuItem("RONRIKU/Export Core Fixtures")]
        public static void Export()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../packages/core/fixtures"));
            Directory.CreateDirectory(dir);

            var rng = new StringBuilder("{\n  \"cases\": [\n");
            ulong[] seeds = { 0, 1, 42, 0x524F4E52494B55, ulong.MaxValue };
            int[] maxes = { 2, 7, 24, 100, 1000003 };
            for (int s = 0; s < seeds.Length; s++)
            {
                var r = new DeterministicRandom(seeds[s]);
                var values = new List<string>();
                for (int i = 0; i < 40; i++) values.Add(r.NextInt(maxes[i % maxes.Length]).ToString(CultureInfo.InvariantCulture));
                rng.Append($"    {{ \"seed\": \"{seeds[s]}\", \"maxes\": [{string.Join(", ", maxes)}], \"values\": [{string.Join(", ", values)}] }}");
                rng.Append(s < seeds.Length - 1 ? ",\n" : "\n");
            }
            rng.Append("  ]\n}\n");
            File.WriteAllText(Path.Combine(dir, "rng.json"), rng.ToString());

            var figures = new StringBuilder($"{{\n  \"generatorVersion\": {FigureGenerator.Version},\n  \"figures\": [\n");
            var rows = new List<string>();
            for (ulong seed = 1; seed <= 30; seed++)
            for (int size = 3; size <= 5; size++)
            foreach (bool monster in new[] { false, true })
            {
                Figure f = FigureGenerator.Generate(seed, size, monster);
                rows.Add($"    {{ \"seed\": \"{seed}\", \"size\": {size}, \"monster\": {(monster ? "true" : "false")}, " +
                         $"\"name\": \"{f.Name}\", \"rarity\": \"{f.Rarity}\", \"filled\": {f.FilledCount}, \"encoding\": \"{f.Encode()}\" }}");
            }
            figures.Append(string.Join(",\n", rows)).Append("\n  ]\n}\n");
            File.WriteAllText(Path.Combine(dir, "figures.json"), figures.ToString());

            var daily = new StringBuilder("{\n  \"rulesVersion\": " + DailyCalendar.RulesVersion + ",\n  \"days\": [\n");
            rows.Clear();
            for (int day = 1; day <= 40; day++)
            {
                DailyPlan plan = DailyPlan.For(day);
                var trials = new List<string>();
                foreach (TrialSpec t in plan.Trials)
                    trials.Add($"{{ \"kind\": \"{t.Kind}\", \"difficulty\": \"{t.Difficulty}\", \"seed\": \"{t.Seed}\" }}");
                rows.Add($"    {{ \"day\": {day}, \"challengeId\": \"{plan.ChallengeId}\", \"seed\": \"{plan.Seed}\", \"trials\": [{string.Join(", ", trials)}] }}");
            }
            daily.Append(string.Join(",\n", rows)).Append("\n  ]\n}\n");
            File.WriteAllText(Path.Combine(dir, "daily.json"), daily.ToString());

            var puzzles = new StringBuilder("{\n");
            rows.Clear();
            foreach (PuzzleDifficulty d in Enum.GetValues(typeof(PuzzleDifficulty)))
            for (long seed = 1; seed <= 12; seed++)
            {
                SpatialPuzzleData sp = new SpatialPuzzleGenerator().Generate(seed, d, 0);
                var cubes = new List<string>();
                foreach (GridPoint c in sp.Cubes) cubes.Add($"[{c.X},{c.Y},{c.Z}]");
                rows.Add($"    {{ \"seed\": \"{seed}\", \"difficulty\": \"{d}\", \"hash\": \"{sp.Metadata.ContentHash}\", \"cubes\": [{string.Join(",", cubes)}], " +
                         $"\"start\": {sp.StartOrientation}, \"targetShadow\": {sp.TargetShadow}, \"par\": {sp.Par}, " +
                         $"\"solution\": [{string.Join(",", Moves(SpatialPuzzleSolver.Solve(sp)))}] }}");
            }
            puzzles.Append("  \"spatial\": [\n").Append(string.Join(",\n", rows)).Append("\n  ],\n");
            rows.Clear();
            foreach (PuzzleDifficulty d in Enum.GetValues(typeof(PuzzleDifficulty)))
            for (long seed = 1; seed <= 12; seed++)
            {
                PatternPuzzleData p = new PatternPuzzleGenerator().Generate(seed, d, 0);
                var ex = new List<string>();
                foreach (var (i, o) in p.Examples) ex.Add($"[{i},{o}]");
                rows.Add($"    {{ \"seed\": \"{seed}\", \"difficulty\": \"{d}\", \"hash\": \"{p.Metadata.ContentHash}\", \"examples\": [{string.Join(",", ex)}], " +
                         $"\"test\": {p.TestInput}, \"options\": [{string.Join(",", p.Options)}], \"correct\": {p.CorrectOption} }}");
            }
            puzzles.Append("  \"pattern\": [\n").Append(string.Join(",\n", rows)).Append("\n  ],\n");
            rows.Clear();
            foreach (PuzzleDifficulty d in Enum.GetValues(typeof(PuzzleDifficulty)))
            for (long seed = 1; seed <= 8; seed++)
            {
                LogicPuzzleData l = new LogicPuzzleGenerator().Generate(seed, d, 0);
                rows.Add($"    {{ \"seed\": \"{seed}\", \"difficulty\": \"{d}\", \"hash\": \"{l.Metadata.ContentHash}\", \"size\": {l.Size}, " +
                         $"\"checkpoints\": [{string.Join(",", l.Checkpoints)}], \"walls\": [{string.Join(",", l.Walls)}], " +
                         $"\"solution\": [{string.Join(",", LogicPuzzleSolver.Solve(l))}] }}");
            }
            puzzles.Append("  \"logic\": [\n").Append(string.Join(",\n", rows)).Append("\n  ]\n}\n");
            File.WriteAllText(Path.Combine(dir, "puzzles.json"), puzzles.ToString());

            string voxDir = Path.Combine(dir, "vox");
            Directory.CreateDirectory(voxDir);
            for (ulong seed = 1; seed <= 6; seed++)
            for (int size = 3; size <= 5; size++)
                File.WriteAllBytes(Path.Combine(voxDir, $"figure-{seed}-{size}.vox"), FigureGenerator.Generate(seed, size).ToVox());

            Debug.Log($"RONRIKU_FIXTURES_OK {dir}");
        }

        private static IEnumerable<string> Moves(IReadOnlyList<SpatialMove> moves)
        {
            foreach (SpatialMove m in moves) yield return ((int)m).ToString(CultureInfo.InvariantCulture);
        }
    }
}
