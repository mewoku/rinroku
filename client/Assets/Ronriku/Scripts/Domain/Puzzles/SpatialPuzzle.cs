using System;
using System.Collections.Generic;

namespace Ronriku.Domain.Puzzles
{
    [Serializable]
    public readonly struct GridPoint : IEquatable<GridPoint>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public GridPoint(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(GridPoint other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is GridPoint other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ (Y * 31) ^ Z;
        public override string ToString() => $"({X},{Y},{Z})";
    }

    /// <summary>Discrete 90° player moves. Z is up; the floor is the XY plane.</summary>
    public enum SpatialMove
    {
        TurnLeft = 0,
        TurnRight = 1,
        TipBack = 2,
        TipForward = 3
    }

    /// <summary>
    /// The 24 proper rotations of a cube, indexed deterministically by breadth-first
    /// expansion from identity. Rotations act about the centre (1,1,1) of a 3×3×3 box,
    /// so every in-box cell maps to an in-box cell.
    /// </summary>
    public static class CubeOrientations
    {
        public const int Count = 24;
        public const int MoveCount = 4;
        public const int BoxSize = 3;

        private static readonly int[][] Matrices;
        private static readonly int[,] Transitions;

        static CubeOrientations()
        {
            int[][] generators =
            {
                new[] { 0, -1, 0, 1, 0, 0, 0, 0, 1 },  // TurnLeft: +90° about Z
                new[] { 0, 1, 0, -1, 0, 0, 0, 0, 1 },  // TurnRight: -90° about Z
                new[] { 0, 0, -1, 0, 1, 0, 1, 0, 0 },  // TipBack: top moves toward -X
                new[] { 0, 0, 1, 0, 1, 0, -1, 0, 0 }   // TipForward: top moves toward +X
            };

            var found = new List<int[]> { new[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 } };
            var queue = new Queue<int>();
            queue.Enqueue(0);
            var edges = new List<(int from, int move, int to)>();
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                for (int move = 0; move < MoveCount; move++)
                {
                    int[] next = Multiply(generators[move], found[current]);
                    int index = found.FindIndex(m => SameMatrix(m, next));
                    if (index < 0)
                    {
                        found.Add(next);
                        index = found.Count - 1;
                        queue.Enqueue(index);
                    }
                    edges.Add((current, move, index));
                }
            }

            if (found.Count != Count)
                throw new InvalidOperationException($"Expected {Count} orientations, found {found.Count}.");

            Matrices = found.ToArray();
            Transitions = new int[Count, MoveCount];
            foreach (var edge in edges) Transitions[edge.from, edge.move] = edge.to;
        }

        public static int Apply(int orientation, SpatialMove move) => Transitions[orientation, (int)move];

        /// <summary>Row-major 3×3 rotation matrix. Callers must not mutate the result.</summary>
        public static IReadOnlyList<int> Matrix(int orientation) => Matrices[orientation];

        public static GridPoint Rotate(int orientation, GridPoint point)
        {
            int[] m = Matrices[orientation];
            int x = point.X - 1, y = point.Y - 1, z = point.Z - 1;
            return new GridPoint(
                m[0] * x + m[1] * y + m[2] * z + 1,
                m[3] * x + m[4] * y + m[5] * z + 1,
                m[6] * x + m[7] * y + m[8] * z + 1);
        }

        /// <summary>Top-down shadow as a 9-bit mask, bit index x + 3y.</summary>
        public static int Shadow(IReadOnlyList<GridPoint> cubes, int orientation)
        {
            int mask = 0;
            foreach (var cube in cubes)
            {
                GridPoint p = Rotate(orientation, cube);
                mask |= 1 << (p.X + BoxSize * p.Y);
            }
            return mask;
        }

        public static SpatialMove Inverse(SpatialMove move) => move switch
        {
            SpatialMove.TurnLeft => SpatialMove.TurnRight,
            SpatialMove.TurnRight => SpatialMove.TurnLeft,
            SpatialMove.TipBack => SpatialMove.TipForward,
            _ => SpatialMove.TipBack
        };

        private static int[] Multiply(int[] a, int[] b)
        {
            var r = new int[9];
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
                r[row * 3 + col] = a[row * 3] * b[col] + a[row * 3 + 1] * b[3 + col] + a[row * 3 + 2] * b[6 + col];
            return r;
        }

        private static bool SameMatrix(int[] a, int[] b)
        {
            for (int i = 0; i < 9; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }

    [Serializable]
    public sealed class SpatialPuzzleData
    {
        public PuzzleMetadata Metadata { get; }
        public IReadOnlyList<GridPoint> Cubes { get; }
        public int StartOrientation { get; }
        public int TargetShadow { get; }
        public int Par { get; }

        public SpatialPuzzleData(PuzzleMetadata metadata, IReadOnlyList<GridPoint> cubes,
            int startOrientation, int targetShadow, int par)
        {
            Metadata = metadata;
            Cubes = cubes;
            StartOrientation = startOrientation;
            TargetShadow = targetShadow;
            Par = par;
        }

        public int ShadowAt(int orientation) => CubeOrientations.Shadow(Cubes, orientation);
        public bool IsSolvedAt(int orientation) => ShadowAt(orientation) == TargetShadow;
    }

    /// <summary>
    /// Shadow Match: rotate a small cube structure with discrete 90° turns and tips
    /// until its top-down shadow covers exactly the target floor tiles.
    /// </summary>
    public sealed class SpatialPuzzleGenerator : IPuzzleGenerator<SpatialPuzzleData>
    {
        public const int CurrentRulesVersion = 2;
        private const int MaxAttempts = 512;

        /// <summary>Cube count, minimum par and maximum par per difficulty.</summary>
        public static (int cubes, int minPar, int maxPar) Limits(PuzzleDifficulty difficulty) => difficulty switch
        {
            PuzzleDifficulty.Easy => (4, 1, 2),
            PuzzleDifficulty.Standard => (5, 2, 3),
            _ => (6, 3, 5)
        };

        /// <summary>At most this many of the 24 orientations may produce the target shadow.</summary>
        public const int MaxMatchingOrientations = 4;

        /// <summary>A structure must cast at least this many distinct shadows.</summary>
        public const int MinDistinctShadows = 6;

        public SpatialPuzzleData Generate(long seed, PuzzleDifficulty difficulty, long variantSeed)
        {
            var random = new DeterministicRandom(unchecked((ulong)seed ^ ((ulong)difficulty * 0x9E3779B97F4A7C15UL)));
            var (cubeCount, minPar, maxPar) = Limits(difficulty);
            bool requireTip = difficulty != PuzzleDifficulty.Easy;

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                List<GridPoint> cubes = GrowPolycube(ref random, cubeCount);
                var shadows = new int[CubeOrientations.Count];
                var shadowCounts = new Dictionary<int, int>();
                for (int o = 0; o < CubeOrientations.Count; o++)
                {
                    shadows[o] = CubeOrientations.Shadow(cubes, o);
                    shadowCounts[shadows[o]] = shadowCounts.TryGetValue(shadows[o], out int c) ? c + 1 : 1;
                }
                if (shadowCounts.Count < MinDistinctShadows) continue;

                int start = random.NextInt(CubeOrientations.Count);
                int[] distance = SpatialPuzzleSolver.Distances(start);
                var turnOnly = TurnOnlyShadows(cubes, start);

                var candidates = new List<int>();
                foreach (var pair in shadowCounts)
                {
                    int mask = pair.Key;
                    if (mask == shadows[start] || pair.Value > MaxMatchingOrientations) continue;
                    if (requireTip && turnOnly.Contains(mask)) continue;
                    int par = int.MaxValue;
                    for (int o = 0; o < CubeOrientations.Count; o++)
                        if (shadows[o] == mask) par = Math.Min(par, distance[o]);
                    if (par >= minPar && par <= maxPar) candidates.Add(mask);
                }
                if (candidates.Count == 0) continue;

                candidates.Sort();
                int target = candidates[random.NextInt(candidates.Count)];
                int finalPar = SpatialPuzzleSolver.Par(cubes, start, target);
                string hash = ContentHash(seed, variantSeed, difficulty, cubes, start, target);
                var metadata = new PuzzleMetadata(
                    $"spatial-{seed:x16}-{(int)difficulty}", 1, "spatial", seed, variantSeed, difficulty,
                    CurrentRulesVersion, "MAKE THE SHADOW MATCH THE TARGET", 90,
                    new[] { "Spatial", "Planning", "Speed" }, hash);
                return new SpatialPuzzleData(metadata, cubes.AsReadOnly(), start, target, finalPar);
            }

            throw new InvalidOperationException($"No valid Spatial puzzle for seed {seed} at {difficulty}.");
        }

        private static List<GridPoint> GrowPolycube(ref DeterministicRandom random, int count)
        {
            GridPoint[] directions =
            {
                new GridPoint(1, 0, 0), new GridPoint(-1, 0, 0), new GridPoint(0, 1, 0),
                new GridPoint(0, -1, 0), new GridPoint(0, 0, 1), new GridPoint(0, 0, -1)
            };
            var cubes = new List<GridPoint> { new GridPoint(1, 1, 1) };
            int guard = 0;
            while (cubes.Count < count && guard++ < 256)
            {
                GridPoint from = cubes[random.NextInt(cubes.Count)];
                GridPoint d = directions[random.NextInt(directions.Length)];
                var next = new GridPoint(from.X + d.X, from.Y + d.Y, from.Z + d.Z);
                if (next.X < 0 || next.Y < 0 || next.Z < 0 ||
                    next.X >= CubeOrientations.BoxSize || next.Y >= CubeOrientations.BoxSize ||
                    next.Z >= CubeOrientations.BoxSize || cubes.Contains(next)) continue;
                cubes.Add(next);
            }
            return cubes;
        }

        private static HashSet<int> TurnOnlyShadows(IReadOnlyList<GridPoint> cubes, int start)
        {
            var result = new HashSet<int>();
            int o = start;
            for (int i = 0; i < 4; i++)
            {
                result.Add(CubeOrientations.Shadow(cubes, o));
                o = CubeOrientations.Apply(o, SpatialMove.TurnLeft);
            }
            return result;
        }

        private static string ContentHash(long seed, long variantSeed, PuzzleDifficulty difficulty,
            IEnumerable<GridPoint> cubes, int start, int target)
        {
            ulong hash = 14695981039346656037UL;
            void Add(long value)
            {
                unchecked
                {
                    ulong raw = (ulong)value;
                    for (int i = 0; i < 8; i++)
                    {
                        hash ^= (byte)(raw >> (i * 8));
                        hash *= 1099511628211UL;
                    }
                }
            }
            Add(CurrentRulesVersion); Add(seed); Add(variantSeed); Add((int)difficulty); Add(start); Add(target);
            foreach (var point in cubes) { Add(point.X); Add(point.Y); Add(point.Z); }
            return hash.ToString("x16");
        }
    }

    /// <summary>Breadth-first solver over the 24-state orientation graph.</summary>
    public static class SpatialPuzzleSolver
    {
        public static int[] Distances(int start)
        {
            var distance = new int[CubeOrientations.Count];
            for (int i = 0; i < distance.Length; i++) distance[i] = -1;
            distance[start] = 0;
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                for (int move = 0; move < CubeOrientations.MoveCount; move++)
                {
                    int next = CubeOrientations.Apply(current, (SpatialMove)move);
                    if (distance[next] >= 0) continue;
                    distance[next] = distance[current] + 1;
                    queue.Enqueue(next);
                }
            }
            return distance;
        }

        public static int Par(IReadOnlyList<GridPoint> cubes, int start, int targetShadow)
        {
            IReadOnlyList<SpatialMove> path = Solve(cubes, start, targetShadow);
            return path == null ? -1 : path.Count;
        }

        public static IReadOnlyList<SpatialMove> Solve(SpatialPuzzleData data) =>
            Solve(data.Cubes, data.StartOrientation, data.TargetShadow);

        /// <summary>Shortest move sequence to the target shadow, or null if unreachable.</summary>
        public static IReadOnlyList<SpatialMove> Solve(IReadOnlyList<GridPoint> cubes, int start, int targetShadow)
        {
            var parent = new int[CubeOrientations.Count];
            var via = new SpatialMove[CubeOrientations.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = -2;
            parent[start] = -1;
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (CubeOrientations.Shadow(cubes, current) == targetShadow)
                {
                    var path = new List<SpatialMove>();
                    for (int o = current; parent[o] >= 0; o = parent[o]) path.Add(via[o]);
                    path.Reverse();
                    return path;
                }
                for (int move = 0; move < CubeOrientations.MoveCount; move++)
                {
                    int next = CubeOrientations.Apply(current, (SpatialMove)move);
                    if (parent[next] != -2) continue;
                    parent[next] = current;
                    via[next] = (SpatialMove)move;
                    queue.Enqueue(next);
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Validates a submitted move sequence by replaying it from the issued start state.
    /// The client submits actions, never a completed flag.
    /// </summary>
    public sealed class SpatialPuzzleValidator : IPuzzleValidator<SpatialPuzzleData, IReadOnlyList<SpatialMove>>
    {
        public const int MaxMoves = 64;

        public bool IsCorrect(SpatialPuzzleData data, IReadOnlyList<SpatialMove> moves)
        {
            if (data == null || moves == null || moves.Count > MaxMoves) return false;
            int orientation = data.StartOrientation;
            foreach (SpatialMove move in moves)
            {
                if ((int)move < 0 || (int)move >= CubeOrientations.MoveCount) return false;
                orientation = CubeOrientations.Apply(orientation, move);
            }
            return data.IsSolvedAt(orientation);
        }
    }

    /// <summary>Generator invariants shared by tests and build verification.</summary>
    public static class SpatialPuzzleInvariants
    {
        /// <summary>Returns null when the puzzle is valid, otherwise a description of the violation.</summary>
        public static string Check(SpatialPuzzleData data)
        {
            var (cubeCount, minPar, maxPar) = SpatialPuzzleGenerator.Limits(data.Metadata.Difficulty);
            if (data.Cubes.Count != cubeCount) return $"cube count {data.Cubes.Count} != {cubeCount}";

            var seen = new HashSet<GridPoint>();
            foreach (var cube in data.Cubes)
            {
                if (cube.X < 0 || cube.Y < 0 || cube.Z < 0 || cube.X > 2 || cube.Y > 2 || cube.Z > 2)
                    return $"cube {cube} outside box";
                if (!seen.Add(cube)) return $"duplicate cube {cube}";
            }
            if (!IsConnected(data.Cubes)) return "structure is not face-connected";

            if (data.IsSolvedAt(data.StartOrientation)) return "start is already solved";
            IReadOnlyList<SpatialMove> solution = SpatialPuzzleSolver.Solve(data);
            if (solution == null) return "unsolvable";
            if (solution.Count != data.Par) return $"par {data.Par} != solver {solution.Count}";
            if (data.Par < minPar || data.Par > maxPar) return $"par {data.Par} outside {minPar}..{maxPar}";
            if (!new SpatialPuzzleValidator().IsCorrect(data, solution)) return "solver path rejected by validator";

            int matching = 0;
            for (int o = 0; o < CubeOrientations.Count; o++) if (data.IsSolvedAt(o)) matching++;
            if (matching > SpatialPuzzleGenerator.MaxMatchingOrientations) return $"{matching} orientations match";
            return null;
        }

        private static bool IsConnected(IReadOnlyList<GridPoint> cubes)
        {
            var remaining = new HashSet<GridPoint>(cubes);
            var stack = new Stack<GridPoint>();
            stack.Push(cubes[0]);
            remaining.Remove(cubes[0]);
            while (stack.Count > 0)
            {
                GridPoint p = stack.Pop();
                GridPoint[] neighbours =
                {
                    new GridPoint(p.X + 1, p.Y, p.Z), new GridPoint(p.X - 1, p.Y, p.Z),
                    new GridPoint(p.X, p.Y + 1, p.Z), new GridPoint(p.X, p.Y - 1, p.Z),
                    new GridPoint(p.X, p.Y, p.Z + 1), new GridPoint(p.X, p.Y, p.Z - 1)
                };
                foreach (var n in neighbours) if (remaining.Remove(n)) stack.Push(n);
            }
            return remaining.Count == 0;
        }
    }

    internal struct DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed) => _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            ulong value = _state;
            value ^= value >> 12;
            value ^= value << 25;
            value ^= value >> 27;
            _state = value;
            return (int)((value * 2685821657736338717UL) % (ulong)exclusiveMax);
        }
    }
}
