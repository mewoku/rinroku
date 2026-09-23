using System;
using System.Collections.Generic;

namespace Ronriku.Domain.Puzzles
{
    /// <summary>Primitive 4×4 grid transformations. Grids are 16-bit masks, bit index x + 4y, y down.</summary>
    public enum PatternRule
    {
        Rotate90 = 0,
        Rotate180 = 1,
        Rotate270 = 2,
        MirrorX = 3,
        MirrorY = 4,
        Transpose = 5,
        Invert = 6,
        ShiftRight = 7,
        ShiftDown = 8
    }

    public static class PatternGrid
    {
        public const int Size = 4;
        public const int CellCount = Size * Size;
        public const int Full = 0xFFFF;

        public static bool Get(int grid, int x, int y) => (grid & (1 << (x + Size * y))) != 0;

        public static int Count(int grid)
        {
            int count = 0;
            for (int i = 0; i < CellCount; i++) if ((grid & (1 << i)) != 0) count++;
            return count;
        }

        public static int Apply(PatternRule rule, int grid)
        {
            if (rule == PatternRule.Invert) return ~grid & Full;
            int result = 0;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (!Get(grid, x, y)) continue;
                int nx, ny;
                switch (rule)
                {
                    case PatternRule.Rotate90: nx = Size - 1 - y; ny = x; break;
                    case PatternRule.Rotate180: nx = Size - 1 - x; ny = Size - 1 - y; break;
                    case PatternRule.Rotate270: nx = y; ny = Size - 1 - x; break;
                    case PatternRule.MirrorX: nx = Size - 1 - x; ny = y; break;
                    case PatternRule.MirrorY: nx = x; ny = Size - 1 - y; break;
                    case PatternRule.Transpose: nx = y; ny = x; break;
                    case PatternRule.ShiftRight: nx = (x + 1) % Size; ny = y; break;
                    case PatternRule.ShiftDown: nx = x; ny = (y + 1) % Size; break;
                    default: throw new ArgumentOutOfRangeException(nameof(rule));
                }
                result |= 1 << (nx + Size * ny);
            }
            return result;
        }

        public static int Apply(IReadOnlyList<PatternRule> rules, int grid)
        {
            foreach (var rule in rules) grid = Apply(rule, grid);
            return grid;
        }
    }

    [Serializable]
    public sealed class PatternPuzzleData
    {
        public PuzzleMetadata Metadata { get; }
        /// <summary>Example (input, output) pairs shown to the player.</summary>
        public IReadOnlyList<(int input, int output)> Examples { get; }
        public int TestInput { get; }
        public IReadOnlyList<int> Options { get; }
        public int CorrectOption { get; }

        public PatternPuzzleData(PuzzleMetadata metadata, IReadOnlyList<(int, int)> examples, int testInput,
            IReadOnlyList<int> options, int correctOption)
        {
            Metadata = metadata;
            Examples = examples;
            TestInput = testInput;
            Options = options;
            CorrectOption = correctOption;
        }
    }

    /// <summary>
    /// Pattern trial: infer the transformation from example pairs and pick its result for the test grid.
    /// Easy uses one rotation or mirror, Standard adds transpose/invert/shift, Hard composes two rules.
    /// Every hypothesis in the full vocabulary (all singles and pairs) that fits the examples must
    /// produce the same test output, so the answer is logically unique.
    /// </summary>
    public sealed class PatternPuzzleGenerator : IPuzzleGenerator<PatternPuzzleData>
    {
        public const int CurrentRulesVersion = 1;
        public const int OptionCount = 4;
        private const int MaxAttempts = 2048;

        private static readonly PatternRule[] EasyRules =
            { PatternRule.Rotate90, PatternRule.Rotate180, PatternRule.Rotate270, PatternRule.MirrorX, PatternRule.MirrorY };

        private static readonly PatternRule[] StandardRules =
        {
            PatternRule.Rotate90, PatternRule.Rotate180, PatternRule.Rotate270, PatternRule.MirrorX,
            PatternRule.MirrorY, PatternRule.Transpose, PatternRule.Invert, PatternRule.ShiftRight, PatternRule.ShiftDown
        };

        /// <summary>Every rule sequence a player could plausibly hypothesise: all singles and ordered pairs.</summary>
        public static readonly IReadOnlyList<PatternRule[]> Hypotheses = BuildHypotheses();

        public static int ExampleCount(PuzzleDifficulty difficulty) => difficulty == PuzzleDifficulty.Hard ? 3 : 2;

        public PatternPuzzleData Generate(long seed, PuzzleDifficulty difficulty, long variantSeed)
        {
            var random = new DeterministicRandom(unchecked((ulong)seed ^ 0x5041545445524EUL ^ (ulong)difficulty));
            int exampleCount = ExampleCount(difficulty);

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                PatternRule[] rule = PickRule(ref random, difficulty);
                var inputs = new int[exampleCount + 1];
                bool ok = true;
                for (int i = 0; i < inputs.Length && ok; i++)
                {
                    inputs[i] = RandomGrid(ref random);
                    ok = IsInformative(inputs[i]) && Array.IndexOf(inputs, inputs[i], 0, i) < 0;
                }
                if (!ok) continue;

                var examples = new List<(int, int)>();
                for (int i = 0; i < exampleCount; i++) examples.Add((inputs[i], PatternGrid.Apply(rule, inputs[i])));
                int test = inputs[exampleCount];
                int answer = PatternGrid.Apply(rule, test);
                if (answer == test || !AnswerIsForced(examples, test, answer)) continue;

                List<int> decoys = PickDecoys(ref random, rule, test, answer);
                if (decoys == null) continue;

                int correct = random.NextInt(OptionCount);
                var options = new int[OptionCount];
                for (int i = 0, d = 0; i < OptionCount; i++) options[i] = i == correct ? answer : decoys[d++];

                string hash = ContentHash(seed, variantSeed, difficulty, examples, test, options, correct);
                var metadata = new PuzzleMetadata($"pattern-{seed:x16}-{(int)difficulty}", 1, "pattern", seed,
                    variantSeed, difficulty, CurrentRulesVersion, "FIND THE RULE. PICK THE MISSING OUTPUT",
                    TrialScoring.TimeLimitSeconds("pattern", difficulty),
                    new[] { "Pattern", "Speed" }, hash);
                return new PatternPuzzleData(metadata, examples.AsReadOnly(), test, options, correct);
            }

            throw new InvalidOperationException($"No valid Pattern puzzle for seed {seed} at {difficulty}.");
        }

        /// <summary>True when every hypothesis consistent with the examples maps the test input to <paramref name="answer"/>.</summary>
        public static bool AnswerIsForced(IReadOnlyList<(int input, int output)> examples, int test, int answer)
        {
            foreach (PatternRule[] hypothesis in Hypotheses)
            {
                bool fits = true;
                foreach (var (input, output) in examples)
                {
                    if (PatternGrid.Apply(hypothesis, input) == output) continue;
                    fits = false;
                    break;
                }
                if (fits && PatternGrid.Apply(hypothesis, test) != answer) return false;
            }
            return true;
        }

        private static PatternRule[] PickRule(ref DeterministicRandom random, PuzzleDifficulty difficulty)
        {
            switch (difficulty)
            {
                case PuzzleDifficulty.Easy:
                    return new[] { EasyRules[random.NextInt(EasyRules.Length)] };
                case PuzzleDifficulty.Standard:
                    return new[] { StandardRules[random.NextInt(StandardRules.Length)] };
                default:
                    while (true)
                    {
                        PatternRule a = StandardRules[random.NextInt(StandardRules.Length)];
                        PatternRule b = StandardRules[random.NextInt(StandardRules.Length)];
                        var pair = new[] { a, b };
                        if (a != b && !EquivalentToSingle(pair)) return pair;
                    }
            }
        }

        private static bool EquivalentToSingle(PatternRule[] pair)
        {
            const int probeA = 0x1237, probeB = 0x8C41;
            foreach (PatternRule single in StandardRules)
                if (PatternGrid.Apply(single, probeA) == PatternGrid.Apply(pair, probeA) &&
                    PatternGrid.Apply(single, probeB) == PatternGrid.Apply(pair, probeB))
                    return true;
            return PatternGrid.Apply(pair, probeA) == probeA && PatternGrid.Apply(pair, probeB) == probeB;
        }

        private static int RandomGrid(ref DeterministicRandom random)
        {
            int target = 5 + random.NextInt(4);
            int grid = 0;
            while (PatternGrid.Count(grid) < target) grid |= 1 << random.NextInt(PatternGrid.CellCount);
            return grid;
        }

        /// <summary>A grid is informative when no primitive rule leaves it unchanged.</summary>
        private static bool IsInformative(int grid)
        {
            foreach (PatternRule rule in StandardRules) if (PatternGrid.Apply(rule, grid) == grid) return false;
            return true;
        }

        private static List<int> PickDecoys(ref DeterministicRandom random, PatternRule[] rule, int test, int answer)
        {
            var near = new List<int>();
            var far = new List<int>();
            foreach (PatternRule[] hypothesis in Hypotheses)
            {
                int output = PatternGrid.Apply(hypothesis, test);
                if (output == answer || output == test || near.Contains(output) || far.Contains(output)) continue;
                bool related = hypothesis.Length == rule.Length && Array.IndexOf(rule, hypothesis[0]) < 0 ||
                               hypothesis.Length == 2 && rule.Length == 2 &&
                               (hypothesis[0] == rule[1] && hypothesis[1] == rule[0]);
                (related ? near : far).Add(output);
            }

            var decoys = new List<int>();
            Take(ref random, near, decoys);
            Take(ref random, far, decoys);
            return decoys.Count == OptionCount - 1 ? decoys : null;
        }

        private static void Take(ref DeterministicRandom random, List<int> pool, List<int> into)
        {
            while (into.Count < OptionCount - 1 && pool.Count > 0)
            {
                int index = random.NextInt(pool.Count);
                into.Add(pool[index]);
                pool.RemoveAt(index);
            }
        }

        private static IReadOnlyList<PatternRule[]> BuildHypotheses()
        {
            var list = new List<PatternRule[]>();
            foreach (PatternRule a in StandardRules) list.Add(new[] { a });
            foreach (PatternRule a in StandardRules)
            foreach (PatternRule b in StandardRules)
                list.Add(new[] { a, b });
            return list.AsReadOnly();
        }

        private static string ContentHash(long seed, long variantSeed, PuzzleDifficulty difficulty,
            IEnumerable<(int, int)> examples, int test, IEnumerable<int> options, int correct)
        {
            ulong hash = 14695981039346656037UL;
            void Add(long value)
            {
                unchecked
                {
                    for (int i = 0; i < 8; i++)
                    {
                        hash ^= (byte)((ulong)value >> (i * 8));
                        hash *= 1099511628211UL;
                    }
                }
            }
            Add(CurrentRulesVersion); Add(seed); Add(variantSeed); Add((int)difficulty); Add(test); Add(correct);
            foreach (var (i, o) in examples) { Add(i); Add(o); }
            foreach (int option in options) Add(option);
            return hash.ToString("x16");
        }
    }

    public sealed class PatternPuzzleValidator : IPuzzleValidator<PatternPuzzleData, int>
    {
        public bool IsCorrect(PatternPuzzleData data, int option) =>
            data != null && option >= 0 && option < data.Options.Count && option == data.CorrectOption;
    }

    public static class PatternPuzzleInvariants
    {
        public static string Check(PatternPuzzleData data)
        {
            if (data.Options.Count != PatternPuzzleGenerator.OptionCount) return "wrong option count";
            if (data.Examples.Count != PatternPuzzleGenerator.ExampleCount(data.Metadata.Difficulty)) return "wrong example count";
            var seen = new HashSet<int>();
            foreach (int option in data.Options) if (!seen.Add(option)) return "duplicate option";
            if (seen.Contains(data.TestInput)) return "option equals test input";
            int answer = data.Options[data.CorrectOption];
            if (!PatternPuzzleGenerator.AnswerIsForced(data.Examples, data.TestInput, answer)) return "answer not forced";
            for (int i = 0; i < data.Options.Count; i++)
                if (i != data.CorrectOption &&
                    PatternPuzzleGenerator.AnswerIsForced(data.Examples, data.TestInput, data.Options[i]))
                    return $"decoy {i} is also forced";
            bool anyFits = false;
            foreach (PatternRule[] h in PatternPuzzleGenerator.Hypotheses)
            {
                bool fits = true;
                foreach (var (input, output) in data.Examples) fits &= PatternGrid.Apply(h, input) == output;
                anyFits |= fits;
            }
            return anyFits ? null : "no hypothesis fits the examples";
        }
    }
}
