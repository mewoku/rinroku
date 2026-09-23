using System;
using System.Collections.Generic;

namespace Ronriku.Domain.Puzzles
{
    [Serializable]
    public sealed class LogicPuzzleData
    {
        public PuzzleMetadata Metadata { get; }
        public int Size { get; }
        /// <summary>Checkpoint number (1-based) per cell index x + size·y, or 0 when the cell is plain.</summary>
        public IReadOnlyList<int> Checkpoints { get; }
        public int CheckpointCount { get; }

        /// <summary>Blocked edges between adjacent cells, as <see cref="EdgeKey"/> values.</summary>
        public IReadOnlyList<int> Walls { get; }

        public LogicPuzzleData(PuzzleMetadata metadata, int size, IReadOnlyList<int> checkpoints, int checkpointCount,
            IReadOnlyList<int> walls)
        {
            Metadata = metadata;
            Size = size;
            Checkpoints = checkpoints;
            CheckpointCount = checkpointCount;
            Walls = walls ?? Array.Empty<int>();
        }

        public static int EdgeKey(int a, int b, int cellCount) => Math.Min(a, b) * cellCount + Math.Max(a, b);

        public bool HasWall(int a, int b)
        {
            int key = EdgeKey(a, b, CellCount);
            foreach (int wall in Walls) if (wall == key) return true;
            return false;
        }

        public int CellCount => Size * Size;
        public int Index(int x, int y) => x + Size * y;
        public int StartCell => IndexOfCheckpoint(1);
        public int EndCell => IndexOfCheckpoint(CheckpointCount);

        public int IndexOfCheckpoint(int number)
        {
            for (int i = 0; i < Checkpoints.Count; i++) if (Checkpoints[i] == number) return i;
            return -1;
        }

        public bool Adjacent(int a, int b)
        {
            int ax = a % Size, ay = a / Size, bx = b % Size, by = b / Size;
            return Math.Abs(ax - bx) + Math.Abs(ay - by) == 1;
        }

        public bool CanStep(int a, int b) => Adjacent(a, b) && !HasWall(a, b);
    }

    /// <summary>
    /// Logic trial ("Link"): draw one path through every cell of an N×N grid, starting at 1 and
    /// passing the numbered cells in order, ending on the highest number. Generated from a random
    /// Hamiltonian path; checkpoints are added until the solver proves the solution is unique.
    /// </summary>
    public sealed class LogicPuzzleGenerator : IPuzzleGenerator<LogicPuzzleData>
    {
        public const int CurrentRulesVersion = 1;

        public static int GridSize(PuzzleDifficulty difficulty) => difficulty switch
        {
            PuzzleDifficulty.Easy => 4,
            PuzzleDifficulty.Standard => 5,
            _ => 6
        };

        /// <summary>Extra evenly spaced checkpoints for easier puzzles beyond what uniqueness needs.</summary>
        private static int ExtraSpacing(PuzzleDifficulty difficulty) => difficulty switch
        {
            PuzzleDifficulty.Easy => 4,
            PuzzleDifficulty.Standard => 7,
            _ => 0
        };

        /// <summary>How ambiguity is resolved: 0 = numbers only, 1 = alternate walls and numbers, 2 = walls first.</summary>
        private static int WallPreference(PuzzleDifficulty difficulty) => difficulty switch
        {
            PuzzleDifficulty.Easy => 0,
            PuzzleDifficulty.Standard => 1,
            _ => 2
        };

        public LogicPuzzleData Generate(long seed, PuzzleDifficulty difficulty, long variantSeed)
        {
            var random = new DeterministicRandom(unchecked((ulong)seed ^ 0x4C4F474943UL ^ ((ulong)difficulty << 40)));
            int size = GridSize(difficulty);
            int cells = size * size;
            List<int> path = RandomHamiltonianPath(ref random, size);

            var numbered = new SortedSet<int> { 0, cells - 1 };
            int spacing = ExtraSpacing(difficulty);
            if (spacing > 0)
                for (int i = spacing; i < cells - 2; i += spacing) numbered.Add(i + random.NextInt(2));

            var pathEdges = new HashSet<int>();
            for (int i = 1; i < path.Count; i++) pathEdges.Add(LogicPuzzleData.EdgeKey(path[i - 1], path[i], cells));
            var walls = new SortedSet<int>();
            int preference = WallPreference(difficulty);

            for (int guard = 0; guard < cells * 4; guard++)
            {
                int[] checkpoints = Checkpoints(path, numbered, cells);
                List<int> alternative = LogicPuzzleSolver.FindOtherSolution(size, checkpoints, numbered.Count, walls, path);
                if (alternative == null)
                {
                    var wallList = new List<int>(walls);
                    string hash = ContentHash(seed, variantSeed, difficulty, size, checkpoints, wallList);
                    var metadata = new PuzzleMetadata($"logic-{seed:x16}-{(int)difficulty}", 1, "logic", seed,
                        variantSeed, difficulty, CurrentRulesVersion, "CONNECT THE NUMBERS. FILL EVERY CELL",
                        TrialScoring.TimeLimitSeconds("logic", difficulty),
                        new[] { "Logic", "Planning", "Speed" }, hash);
                    return new LogicPuzzleData(metadata, size, checkpoints, numbered.Count, wallList.AsReadOnly());
                }

                bool useWall = preference == 2 || preference == 1 && guard % 2 == 0;
                if (useWall && TryAddWall(alternative, pathEdges, walls, cells)) continue;

                int diverge = 0;
                while (diverge < path.Count && path[diverge] == alternative[diverge]) diverge++;
                if (diverge >= cells - 1 || !numbered.Add(diverge)) numbered.Add(LargestGapMidpoint(numbered));
            }

            throw new InvalidOperationException($"No unique Logic puzzle for seed {seed} at {difficulty}.");
        }

        /// <summary>Blocks the first edge the alternative uses that the intended path does not.</summary>
        private static bool TryAddWall(List<int> alternative, HashSet<int> pathEdges, SortedSet<int> walls, int cells)
        {
            for (int i = 1; i < alternative.Count; i++)
            {
                if (alternative[i] < 0 || alternative[i - 1] < 0) return false;
                int key = LogicPuzzleData.EdgeKey(alternative[i - 1], alternative[i], cells);
                if (!pathEdges.Contains(key) && walls.Add(key)) return true;
            }
            return false;
        }

        private static int LargestGapMidpoint(SortedSet<int> numbered)
        {
            int best = -1, bestGap = 0, previous = -1;
            foreach (int index in numbered)
            {
                if (previous >= 0 && index - previous > bestGap)
                {
                    bestGap = index - previous;
                    best = previous + bestGap / 2;
                }
                previous = index;
            }
            return best;
        }

        private static int[] Checkpoints(List<int> path, SortedSet<int> numbered, int cells)
        {
            var checkpoints = new int[cells];
            int number = 1;
            foreach (int index in numbered) checkpoints[path[index]] = number++;
            return checkpoints;
        }

        /// <summary>Serpentine start, then random "backbite" moves, which preserve the Hamiltonian property.</summary>
        private static List<int> RandomHamiltonianPath(ref DeterministicRandom random, int size)
        {
            var path = new List<int>(size * size);
            for (int y = 0; y < size; y++)
            for (int i = 0; i < size; i++)
                path.Add((y % 2 == 0 ? i : size - 1 - i) + size * y);

            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            var position = new int[size * size];
            int moves = size * size * 30;
            for (int m = 0; m < moves; m++)
            {
                bool fromTail = random.NextInt(2) == 0;
                if (fromTail) path.Reverse();
                // The head is path[0]; pick a grid neighbour of it.
                int head = path[0];
                int d = random.NextInt(4);
                int nx = head % size + dx[d], ny = head / size + dy[d];
                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                int neighbour = nx + size * ny;
                for (int i = 0; i < path.Count; i++) position[path[i]] = i;
                int k = position[neighbour];
                if (k <= 1) continue;
                // Adding edge head–path[k] and removing edge path[k-1]–path[k] reverses path[0..k-1].
                path.Reverse(0, k);
            }
            return path;
        }

        private static string ContentHash(long seed, long variantSeed, PuzzleDifficulty difficulty, int size,
            IEnumerable<int> checkpoints, IEnumerable<int> walls)
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
            Add(CurrentRulesVersion); Add(seed); Add(variantSeed); Add((int)difficulty); Add(size);
            foreach (int c in checkpoints) Add(c);
            Add(-1);
            foreach (int w in walls) Add(w);
            return hash.ToString("x16");
        }
    }

    public static class LogicPuzzleSolver
    {
        public const int NodeBudget = 4_000_000;

        /// <summary>
        /// Returns a valid solution that differs from <paramref name="known"/>, or null if none exists.
        /// If the search budget runs out, returns <paramref name="known"/> reversed as a conservative
        /// "not proven unique" signal so the generator adds another checkpoint.
        /// </summary>
        public static List<int> FindOtherSolution(int size, int[] checkpoints, int checkpointCount,
            ICollection<int> walls, List<int> known)
        {
            var search = new Search(size, checkpoints, checkpointCount, walls, known);
            bool found = search.Run();
            if (found) return search.Result;
            if (search.Exhausted)
            {
                var fallback = new List<int>(known);
                fallback[fallback.Count - 1] = -1;
                return fallback;
            }
            return null;
        }

        /// <summary>Any one valid solution, or null.</summary>
        public static List<int> Solve(LogicPuzzleData data)
        {
            var checkpoints = new int[data.CellCount];
            for (int i = 0; i < checkpoints.Length; i++) checkpoints[i] = data.Checkpoints[i];
            var search = new Search(data.Size, checkpoints, data.CheckpointCount, new HashSet<int>(data.Walls), null);
            return search.Run() ? search.Result : null;
        }

        private sealed class Search
        {
            private readonly int _size;
            private readonly int _cells;
            private readonly int[] _checkpoints;
            private readonly int _last;
            private readonly List<int> _exclude;
            private readonly ICollection<int> _walls;
            private readonly bool[] _visited;
            private readonly List<int> _path = new List<int>();
            private readonly int[] _stack;
            private readonly bool[] _seen;
            private int _nodes;

            public List<int> Result { get; private set; }
            public bool Exhausted { get; private set; }

            public Search(int size, int[] checkpoints, int checkpointCount, ICollection<int> walls, List<int> exclude)
            {
                _walls = walls;
                _size = size;
                _cells = size * size;
                _checkpoints = checkpoints;
                _last = checkpointCount;
                _exclude = exclude;
                _visited = new bool[_cells];
                _stack = new int[_cells];
                _seen = new bool[_cells];
            }

            public bool Run()
            {
                int start = Array.IndexOf(_checkpoints, 1);
                if (start < 0) return false;
                _visited[start] = true;
                _path.Add(start);
                return Step(start, 2);
            }

            private bool Step(int cell, int next)
            {
                if (++_nodes > NodeBudget)
                {
                    Exhausted = true;
                    return false;
                }
                if (_path.Count == _cells)
                {
                    if (next != _last + 1) return false;
                    if (_exclude != null && SameAs(_exclude)) return false;
                    Result = new List<int>(_path);
                    return true;
                }
                if (!RemainderConnected(cell)) return false;

                int x = cell % _size, y = cell / _size;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= _size || ny >= _size) continue;
                    int n = nx + _size * ny;
                    if (_visited[n] || Blocked(cell, n)) continue;
                    int number = _checkpoints[n];
                    if (number != 0 && number != next) continue;
                    if (number == _last && _path.Count + 1 != _cells) continue;
                    _visited[n] = true;
                    _path.Add(n);
                    if (Step(n, number != 0 ? next + 1 : next)) return true;
                    _path.RemoveAt(_path.Count - 1);
                    _visited[n] = false;
                    if (Exhausted) return false;
                }
                return false;
            }

            private bool Blocked(int a, int b) =>
                _walls != null && _walls.Count > 0 && _walls.Contains(LogicPuzzleData.EdgeKey(a, b, _cells));

            private bool SameAs(List<int> other)
            {
                for (int i = 0; i < _path.Count; i++) if (_path[i] != other[i]) return false;
                return true;
            }

            /// <summary>All unvisited cells must be reachable from the current cell, with at most one dead end.</summary>
            private bool RemainderConnected(int from)
            {
                Array.Clear(_seen, 0, _seen.Length);
                int top = 0, reached = 0, remaining = _cells - _path.Count, deadEnds = 0;
                _stack[top++] = from;
                _seen[from] = true;
                while (top > 0)
                {
                    int c = _stack[--top];
                    int x = c % _size, y = c / _size, free = 0;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= _size || ny >= _size) continue;
                        int n = nx + _size * ny;
                        if (_visited[n] && n != from || Blocked(c, n)) continue;
                        free++;
                        if (_seen[n] || _visited[n]) continue;
                        _seen[n] = true;
                        reached++;
                        _stack[top++] = n;
                    }
                    if (c != from && free <= 1 && ++deadEnds > 1) return false;
                }
                return reached == remaining;
            }
        }
    }

    /// <summary>Validates a submitted cell path. The client submits the path, never a completed flag.</summary>
    public sealed class LogicPuzzleValidator : IPuzzleValidator<LogicPuzzleData, IReadOnlyList<int>>
    {
        public bool IsCorrect(LogicPuzzleData data, IReadOnlyList<int> path)
        {
            if (data == null || path == null || path.Count != data.CellCount) return false;
            var seen = new bool[data.CellCount];
            int next = 1;
            for (int i = 0; i < path.Count; i++)
            {
                int cell = path[i];
                if (cell < 0 || cell >= data.CellCount || seen[cell]) return false;
                if (i > 0 && !data.CanStep(path[i - 1], cell)) return false;
                seen[cell] = true;
                int number = data.Checkpoints[cell];
                if (number == 0) continue;
                if (number != next) return false;
                next++;
            }
            return next == data.CheckpointCount + 1 && data.Checkpoints[path[path.Count - 1]] == data.CheckpointCount;
        }
    }

    public static class LogicPuzzleInvariants
    {
        public static string Check(LogicPuzzleData data)
        {
            if (data.Size != LogicPuzzleGenerator.GridSize(data.Metadata.Difficulty)) return "wrong size";
            if (data.CheckpointCount < 2) return "fewer than two checkpoints";
            for (int n = 1; n <= data.CheckpointCount; n++)
                if (data.IndexOfCheckpoint(n) < 0) return $"missing checkpoint {n}";
            List<int> solution = LogicPuzzleSolver.Solve(data);
            if (solution == null) return "unsolvable";
            if (!new LogicPuzzleValidator().IsCorrect(data, solution)) return "solver path rejected by validator";
            var checkpoints = new int[data.CellCount];
            for (int i = 0; i < checkpoints.Length; i++) checkpoints[i] = data.Checkpoints[i];
            if (LogicPuzzleSolver.FindOtherSolution(data.Size, checkpoints, data.CheckpointCount,
                    new HashSet<int>(data.Walls), solution) != null)
                return "solution not unique";
            return null;
        }
    }
}
