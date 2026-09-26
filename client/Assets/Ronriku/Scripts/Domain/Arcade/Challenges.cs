using System;
using System.Collections.Generic;
using Ronriku.Domain.Puzzles;

namespace Ronriku.Domain.Arcade
{
    /// <summary>
    /// Micro-challenges: 3–10 second reasoning cards thrown at the hero during battles. Every one has a
    /// one-line prompt and a single tap/selection answer so nobody needs a tutorial.
    /// </summary>
    public enum ChallengeKind
    {
        Next = 0,    // what comes next in the row of tokens
        Odd = 1,     // three are the same shape turned; one is mirrored
        Sum = 2,     // tap the tiles that add up to the target
        Memory = 3,  // lights flash, tap them back
        Mirror = 4,  // pick the half that completes the mirror picture
        Arrows = 5,  // follow the arrows: where does the ball leave the board?
        Scales = 6,  // balance scales: how many small weights equal the question
        Classic = 7  // BIG CARD: a full classic trial (Items[0]: 0 Pattern, 1 Shadow, 2 Link); Answer 1 = solved
    }

    /// <summary>
    /// A token drawn in Next/Odd challenges, packed in an int:
    /// bits 0-2 shape, 3-5 color, 6-7 count-1, 8-9 rotation (quarter turns clockwise).
    /// </summary>
    public static class Token
    {
        public const int Circle = 0, Square = 1, Triangle = 2, Diamond = 3, Arrow = 4, ShapeCount = 5;
        public const int ColorCount = 5;

        public static int Make(int shape, int color, int count = 1, int rotation = 0) =>
            shape | (color << 3) | ((count - 1) << 6) | ((rotation & 3) << 8);

        public static int Shape(int token) => token & 7;
        public static int Color(int token) => (token >> 3) & 7;
        public static int Count(int token) => ((token >> 6) & 3) + 1;
        public static int Rotation(int token) => (token >> 8) & 3;
    }

    public sealed class Challenge
    {
        public ChallengeKind Kind { get; }
        public long Seed { get; }
        public int Tier { get; }
        /// <summary>Kind-specific payload: tokens, tiles, grid masks, arrow directions…</summary>
        public int[] Items { get; }
        /// <summary>Choice values (tokens, masks, numbers, cells). Empty for Sum/Memory (free selection).</summary>
        public int[] Options { get; }
        /// <summary>Expected answer: option index (Next/Odd/Mirror/Scales), cell (Arrows) or bitmask (Sum/Memory).</summary>
        public int Answer { get; }
        /// <summary>Sum target / Arrows start cell / Scales question shape.</summary>
        public int Target { get; }
        /// <summary>Sum: tiles to pick. Memory: lit cells. Scales: number of clues.</summary>
        public int Pick { get; }
        /// <summary>Scales clues: each is [lhsShape, rhs shapes...]. Question: [shapes...] = ? small.</summary>
        public IReadOnlyList<int[]> Groups { get; }

        public Challenge(ChallengeKind kind, long seed, int tier, int[] items, int[] options, int answer, int target = 0,
            int pick = 0, IReadOnlyList<int[]> groups = null)
        {
            Kind = kind;
            Seed = seed;
            Tier = tier;
            Items = items;
            Options = options;
            Answer = answer;
            Target = target;
            Pick = pick;
            Groups = groups ?? Array.Empty<int[]>();
        }

        public bool Check(int answer) => answer == Answer;

        public string Prompt => Kind switch
        {
            ChallengeKind.Next => "WHAT COMES NEXT?",
            ChallengeKind.Odd => "ONE IS FLIPPED. FIND IT",
            ChallengeKind.Sum => $"TAP {Pick} THAT MAKE {Target}",
            ChallengeKind.Memory => "REMEMBER THE LIGHTS",
            ChallengeKind.Mirror => "COMPLETE THE MIRROR",
            ChallengeKind.Arrows => "WHERE DOES THE BALL EXIT?",
            ChallengeKind.Classic => Items[0] == 0 ? "BIG CARD · PATTERN" : Items[0] == 1 ? "BIG CARD · SHADOW" : "BIG CARD · LINK",
            _ => "HOW MANY DOTS?"
        };
    }

    public static class Challenges
    {
        public const int ArrowSize = 4;
        public const int MemorySize = 4;

        /// <summary>Chiral shapes on a 4×4 grid (bit x + 4y): a mirror image is never a rotation.</summary>
        public static readonly int[] ChiralShapes =
        {
            Bits((0, 0), (0, 1), (0, 2), (1, 2)),          // L
            Bits((1, 0), (2, 0), (0, 1), (1, 1)),          // S
            Bits((1, 0), (2, 0), (0, 1), (1, 1), (1, 2)),  // F
            Bits((0, 0), (1, 0), (0, 1), (1, 1), (0, 2)),  // P
            Bits((1, 0), (1, 1), (0, 2), (1, 2), (1, 3)),  // Y
            Bits((0, 0), (0, 1), (1, 1), (1, 2), (1, 3)),  // N
            Bits((0, 0), (1, 0), (2, 0), (2, 1), (3, 1))   // long Z
        };

        private static int Bits(params (int x, int y)[] cells)
        {
            int mask = 0;
            foreach (var (x, y) in cells) mask |= 1 << (x + 4 * y);
            return mask;
        }

        public static Challenge Generate(ChallengeKind kind, long seed, int tier)
        {
            var rng = new DeterministicRandom(unchecked((ulong)seed));
            return kind switch
            {
                ChallengeKind.Next => Next(ref rng, seed, tier),
                ChallengeKind.Odd => Odd(ref rng, seed, tier),
                ChallengeKind.Sum => Sum(ref rng, seed, tier),
                ChallengeKind.Memory => Memory(ref rng, seed, tier),
                ChallengeKind.Mirror => Mirror(ref rng, seed, tier),
                ChallengeKind.Arrows => Arrows(ref rng, seed, tier),
                ChallengeKind.Classic => new Challenge(ChallengeKind.Classic, seed, tier, new[] { rng.NextInt(3) }, Array.Empty<int>(), 1),
                _ => Scales(ref rng, seed, tier)
            };
        }

        // ------------------------------------------------------------------ Next

        private static Challenge Next(ref DeterministicRandom rng, long seed, int tier)
        {
            // Each attribute either stays fixed or follows a cycle. Tier 0 varies one attribute, tier 1 two.
            int shape = rng.NextInt(Token.ShapeCount - 1);          // arrows only when rotation varies
            int color = rng.NextInt(Token.ColorCount);
            int[] shapeCycle = { shape };
            int[] colorCycle = { color };
            int[] countCycle = { 1 };
            int rotationStep = 0;

            int first = rng.NextInt(4);
            int second = tier > 0 ? (first + 1 + rng.NextInt(3)) % 4 : -1;
            foreach (int attribute in new[] { first, second })
            {
                switch (attribute)
                {
                    case 0: colorCycle = Cycle(ref rng, Token.ColorCount, color); break;
                    case 1: shapeCycle = Cycle(ref rng, Token.ShapeCount - 1, shape); break;
                    case 2: countCycle = rng.NextInt(2) == 0 ? new[] { 1, 2, 3 } : new[] { 1, 2 }; break;
                    case 3: rotationStep = 1; break;
                }
            }
            if (rotationStep != 0) shapeCycle = new[] { Token.Arrow };

            const int shown = 5;
            int startRotation = rng.NextInt(4);
            int TokenAt(int i) => Token.Make(shapeCycle[i % shapeCycle.Length], colorCycle[i % colorCycle.Length],
                countCycle[i % countCycle.Length], startRotation + rotationStep * i);

            var items = new int[shown];
            for (int i = 0; i < shown; i++) items[i] = TokenAt(i);
            int correct = TokenAt(shown);

            // Distractors break exactly one attribute of the right answer.
            var options = new List<int> { correct };
            int guard = 0;
            while (options.Count < 3 && guard++ < 64)
            {
                int s = Token.Shape(correct), c = Token.Color(correct), n = Token.Count(correct), r = Token.Rotation(correct);
                switch (rng.NextInt(4))
                {
                    case 0: c = (c + 1 + rng.NextInt(Token.ColorCount - 1)) % Token.ColorCount; break;
                    case 1: if (s != Token.Arrow) s = (s + 1 + rng.NextInt(Token.ShapeCount - 2)) % (Token.ShapeCount - 1); else r = (r + 2) % 4; break;
                    case 2: n = n % 3 + 1; break;
                    case 3: if (s == Token.Arrow) r = (r + 1 + rng.NextInt(3)) % 4; else n = (n + 1) % 3 + 1; break;
                }
                int candidate = Token.Make(s, c, n, r);
                if (!options.Contains(candidate)) options.Add(candidate);
            }
            return Shuffled(ChallengeKind.Next, seed, tier, items, options, ref rng);
        }

        private static int[] Cycle(ref DeterministicRandom rng, int range, int start)
        {
            int length = 2 + rng.NextInt(2);
            var cycle = new int[length];
            cycle[0] = start;
            for (int i = 1; i < length; i++)
            {
                int v;
                do v = rng.NextInt(range); while (Array.IndexOf(cycle, v, 0, i) >= 0);
                cycle[i] = v;
            }
            return cycle;
        }

        // ------------------------------------------------------------------ Odd

        private static Challenge Odd(ref DeterministicRandom rng, long seed, int tier)
        {
            int shapes = tier > 0 ? ChiralShapes.Length : 4;
            int shape = ChiralShapes[rng.NextInt(shapes)];
            int oddIndex = rng.NextInt(4);
            var options = new int[4];
            int lastRotation = -1;
            for (int i = 0; i < 4; i++)
            {
                int rotation = rng.NextInt(4);
                if (rotation == lastRotation) rotation = (rotation + 1) % 4;
                lastRotation = rotation;
                int grid = i == oddIndex ? PatternGrid.Apply(PatternRule.MirrorX, shape) : shape;
                options[i] = Normalize(Rotate(grid, rotation));
            }
            return new Challenge(ChallengeKind.Odd, seed, tier, Array.Empty<int>(), options, oddIndex);
        }

        private static int Rotate(int grid, int quarterTurns)
        {
            for (int i = 0; i < quarterTurns; i++) grid = PatternGrid.Apply(PatternRule.Rotate90, grid);
            return grid;
        }

        /// <summary>Moves a shape to the top-left so the four pictures line up.</summary>
        public static int Normalize(int grid)
        {
            if (grid == 0) return 0;
            while ((grid & 0x000F) == 0) grid >>= 4;
            while ((grid & 0x1111) == 0) grid = PatternGrid.Apply(PatternRule.ShiftRight, PatternGrid.Apply(PatternRule.ShiftRight, PatternGrid.Apply(PatternRule.ShiftRight, grid)));
            return grid;
        }

        // ------------------------------------------------------------------ Sum

        private static Challenge Sum(ref DeterministicRandom rng, long seed, int tier)
        {
            int tiles = tier > 0 ? 6 : 5;
            int pick = tier > 0 ? 3 : 2;
            for (int attempt = 0; attempt < 200; attempt++)
            {
                var items = new int[tiles];
                for (int i = 0; i < tiles; i++) items[i] = 1 + rng.NextInt(9);
                int answer = 0;
                while (PopCount(answer) < pick) answer |= 1 << rng.NextInt(tiles);
                int target = 0;
                for (int i = 0; i < tiles; i++) if ((answer & (1 << i)) != 0) target += items[i];
                if (CountSubsets(items, pick, target) == 1)
                    return new Challenge(ChallengeKind.Sum, seed, tier, items, Array.Empty<int>(), answer, target, pick);
            }
            // Always-unique fallback: distinct powers make every subset sum different.
            var fallback = tier > 0 ? new[] { 1, 2, 4, 8, 3, 5 } : new[] { 1, 2, 4, 8, 3 };
            int mask = (1 << 0) | (1 << 3) | (tier > 0 ? 1 << 1 : 0);
            int sum = 0;
            for (int i = 0; i < fallback.Length; i++) if ((mask & (1 << i)) != 0) sum += fallback[i];
            return new Challenge(ChallengeKind.Sum, seed, tier, fallback, Array.Empty<int>(), mask, sum, pick);
        }

        public static int SumOf(int[] tiles, int mask)
        {
            int sum = 0;
            for (int i = 0; i < tiles.Length; i++) if ((mask & (1 << i)) != 0) sum += tiles[i];
            return sum;
        }

        /// <summary>
        /// Sum answers are judged by value, not identity: any selection of the right size and total counts
        /// (the generator keeps it unique anyway).
        /// </summary>
        public static bool CheckSum(Challenge c, int mask) => PopCount(mask) == c.Pick && SumOf(c.Items, mask) == c.Target;

        private static int CountSubsets(int[] items, int pick, int target)
        {
            int count = 0;
            for (int mask = 0; mask < 1 << items.Length; mask++)
                if (PopCount(mask) == pick && SumOf(items, mask) == target) count++;
            return count;
        }

        public static int PopCount(int v)
        {
            int c = 0;
            while (v != 0) { v &= v - 1; c++; }
            return c;
        }

        // ------------------------------------------------------------------ Memory

        private static Challenge Memory(ref DeterministicRandom rng, long seed, int tier)
        {
            int lit = tier > 0 ? 5 : 4;
            int mask = 0;
            while (PopCount(mask) < lit) mask |= 1 << rng.NextInt(MemorySize * MemorySize);
            return new Challenge(ChallengeKind.Memory, seed, tier, Array.Empty<int>(), Array.Empty<int>(), mask, 0, lit);
        }

        // ------------------------------------------------------------------ Mirror

        /// <summary>
        /// Picture is 4 columns × 4 rows mirrored around the middle. Items[0] = left half (bits x + 2y,
        /// x∈{0,1}); options are right halves in the same encoding. The correct right half is the mirror.
        /// </summary>
        private static Challenge Mirror(ref DeterministicRandom rng, long seed, int tier)
        {
            int left;
            do left = rng.NextInt(256); while (PopCount(left) < 3 || PopCount(left) > 6 || MirrorHalf(left) == left);
            int correct = MirrorHalf(left);
            var options = new List<int> { correct, left }; // "copied, not mirrored" is the classic trap
            int guard = 0;
            while (options.Count < 3 && guard++ < 64)
            {
                int flipped = correct ^ (1 << rng.NextInt(8));
                if (tier > 0) flipped ^= 1 << rng.NextInt(8);
                if (flipped != 0 && !options.Contains(flipped)) options.Add(flipped);
            }
            return Shuffled(ChallengeKind.Mirror, seed, tier, new[] { left }, options, ref rng);
        }

        public static int MirrorHalf(int half)
        {
            int result = 0;
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 2; x++)
                if ((half & (1 << (x + 2 * y))) != 0) result |= 1 << ((1 - x) + 2 * y);
            return result;
        }

        // ------------------------------------------------------------------ Arrows

        public static readonly (int dx, int dy)[] Directions = { (0, -1), (1, 0), (0, 1), (-1, 0) };

        /// <summary>Items = 16 directions (0 up, 1 right, 2 down, 3 left). Target = start cell. Options = 3 cells.</summary>
        private static Challenge Arrows(ref DeterministicRandom rng, long seed, int tier)
        {
            int minSteps = tier > 0 ? 6 : 4;
            for (int attempt = 0; attempt < 3000; attempt++)
            {
                var dirs = new int[ArrowSize * ArrowSize];
                for (int i = 0; i < dirs.Length; i++) dirs[i] = rng.NextInt(4);
                int start = rng.NextInt(dirs.Length);
                int exit = FollowArrows(dirs, start, out int steps);
                if (exit < 0 || steps < minSteps || steps > minSteps + 5) continue;

                var options = new List<int> { exit };
                int guard = 0;
                while (options.Count < 3 && guard++ < 64)
                {
                    int cell = rng.NextInt(dirs.Length);
                    if (!options.Contains(cell) && cell != start && IsEdge(cell)) options.Add(cell);
                }
                options.Sort();
                return new Challenge(ChallengeKind.Arrows, seed, tier, dirs, options.ToArray(), options.IndexOf(exit), start);
            }
            // Fallback: a straight corridor.
            var line = new int[ArrowSize * ArrowSize];
            for (int i = 0; i < line.Length; i++) line[i] = 1;
            return new Challenge(ChallengeKind.Arrows, seed, tier, line, new[] { 3, 7, 11 }, 0, 0);
        }

        private static bool IsEdge(int cell)
        {
            int x = cell % ArrowSize, y = cell / ArrowSize;
            return x == 0 || y == 0 || x == ArrowSize - 1 || y == ArrowSize - 1;
        }

        /// <summary>Returns the last cell inside the board before the ball leaves, or -1 on a loop.</summary>
        public static int FollowArrows(int[] dirs, int start, out int steps)
        {
            int cell = start;
            steps = 0;
            var seen = new bool[dirs.Length];
            while (true)
            {
                if (seen[cell]) return -1;
                seen[cell] = true;
                var (dx, dy) = Directions[dirs[cell]];
                int x = cell % ArrowSize + dx, y = cell / ArrowSize + dy;
                if (x < 0 || y < 0 || x >= ArrowSize || y >= ArrowSize) return cell;
                cell = x + ArrowSize * y;
                steps++;
            }
        }

        // ------------------------------------------------------------------ Scales

        /// <summary>
        /// Shapes 0 (dot, weight 1), 1 and 2 with hidden weights. Groups hold clue equations
        /// [lhs, rhs...]; Items is the question (shapes to weigh); options are dot counts.
        /// </summary>
        private static Challenge Scales(ref DeterministicRandom rng, long seed, int tier)
        {
            int b = 2 + rng.NextInt(2);                  // square = 2..3 dots
            int c = b + 1 + rng.NextInt(2);              // triangle = square + 1..2 dots
            var clues = new List<int[]> { Repeat(1, 0, b) };
            int[] question;
            if (tier == 0)
            {
                question = rng.NextInt(2) == 0 ? new[] { 1, 1 } : new[] { 1, 0 };
            }
            else
            {
                var rhs = new List<int> { 1 };
                for (int i = 0; i < c - b; i++) rhs.Add(0);
                var clue = new List<int> { 2 };
                clue.AddRange(rhs);
                clues.Add(clue.ToArray());
                question = rng.NextInt(2) == 0 ? new[] { 2, 1 } : new[] { 2, 0 };
            }
            int answer = 0;
            foreach (int shape in question) answer += shape == 0 ? 1 : shape == 1 ? b : c;
            var options = new List<int> { answer, answer + 1, answer - 1 };
            if (rng.NextInt(2) == 0) options[2] = answer + 2;
            options.Sort();
            return new Challenge(ChallengeKind.Scales, seed, tier, question, options.ToArray(), options.IndexOf(answer), 0,
                clues.Count, clues);
        }

        private static int[] Repeat(int lhs, int value, int times)
        {
            var result = new int[times + 1];
            result[0] = lhs;
            for (int i = 1; i <= times; i++) result[i] = value;
            return result;
        }

        // ------------------------------------------------------------------ helpers

        private static Challenge Shuffled(ChallengeKind kind, long seed, int tier, int[] items, List<int> options,
            ref DeterministicRandom rng)
        {
            int correct = options[0];
            for (int i = options.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                (options[i], options[j]) = (options[j], options[i]);
            }
            return new Challenge(kind, seed, tier, items, options.ToArray(), options.IndexOf(correct));
        }

        /// <summary>Judges an answer the way the UI submits it for each kind.</summary>
        public static bool IsCorrect(Challenge c, int answer) => c.Kind == ChallengeKind.Sum ? CheckSum(c, answer) : c.Check(answer);
    }
}
