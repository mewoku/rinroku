using System;
using System.Collections.Generic;

namespace Ronriku.Domain.Arcade
{
    public enum DashTile : byte { Floor = 0, Wall = 1, Spike = 2 }

    /// <summary>
    /// Ice dash: the hero slides until something stops them. Grab every gem, then the exit opens and
    /// catches the hero. Spikes send you back to the start. Levels are generated from a seed and solved
    /// by BFS so every one is beatable and has a known par.
    /// </summary>
    public sealed class DashLevel
    {
        public const int Width = 7;
        public const int Height = 9;

        public DashTile[] Tiles { get; }
        public int Start { get; }
        public int Exit { get; }
        public int[] Gems { get; }
        public int Par { get; }
        public long Seed { get; }

        public DashLevel(long seed, DashTile[] tiles, int start, int exit, int[] gems, int par)
        {
            Seed = seed;
            Tiles = tiles;
            Start = start;
            Exit = exit;
            Gems = gems;
            Par = par;
        }

        public int AllGems => (1 << Gems.Length) - 1;

        public static DashLevel Generate(long seed, int tier)
        {
            var rng = new DeterministicRandom(unchecked((ulong)seed));
            int gemCount = tier > 0 ? 3 : 2;
            int minPar = tier > 0 ? 6 : 4;
            int maxPar = tier > 0 ? 12 : 9;
            for (int attempt = 0; attempt < 2000; attempt++)
            {
                var tiles = new DashTile[Width * Height];
                int walls = 9 + rng.NextInt(6);
                for (int i = 0; i < walls; i++) tiles[rng.NextInt(tiles.Length)] = DashTile.Wall;
                int spikes = tier > 0 ? 2 + rng.NextInt(3) : rng.NextInt(2);
                for (int i = 0; i < spikes; i++)
                {
                    int s = rng.NextInt(tiles.Length);
                    if (tiles[s] == DashTile.Floor) tiles[s] = DashTile.Spike;
                }
                int start = FreeCell(ref rng, tiles, -1);
                int exit = FreeCell(ref rng, tiles, start);
                var gems = new int[gemCount];
                bool ok = true;
                for (int g = 0; g < gemCount && ok; g++)
                {
                    int cell = FreeCell(ref rng, tiles, start);
                    if (cell == exit || Array.IndexOf(gems, cell, 0, g) >= 0) ok = false;
                    gems[g] = cell;
                }
                if (!ok) continue;
                var level = new DashLevel(seed, tiles, start, exit, gems, 0);
                int par = Solve(level);
                if (par < minPar || par > maxPar) continue;
                return new DashLevel(seed, tiles, start, exit, gems, par);
            }
            throw new InvalidOperationException("dash generator exhausted for seed " + seed);
        }

        private static int FreeCell(ref DeterministicRandom rng, DashTile[] tiles, int avoid)
        {
            while (true)
            {
                int c = rng.NextInt(tiles.Length);
                if (tiles[c] == DashTile.Floor && c != avoid) return c;
            }
        }

        /// <summary>Minimum moves to win, or -1.</summary>
        public static int Solve(DashLevel level)
        {
            var seen = new HashSet<int>();
            var queue = new Queue<(int pos, int gems, int depth)>();
            queue.Enqueue((level.Start, 0, 0));
            seen.Add(level.Start * 64);
            while (queue.Count > 0)
            {
                var (pos, gems, depth) = queue.Dequeue();
                for (int dir = 0; dir < 4; dir++)
                {
                    var slide = DashState.Slide(level, pos, gems, dir);
                    if (slide.Path.Count == 0 || slide.Dead) continue;
                    if (slide.Won) return depth + 1;
                    int key = slide.End * 64 + slide.Gems;
                    if (seen.Add(key)) queue.Enqueue((slide.End, slide.Gems, depth + 1));
                }
                if (depth > 20) break;
            }
            return -1;
        }
    }

    public sealed class DashSlide
    {
        public readonly List<int> Path = new List<int>();
        public int End;
        public int Gems;
        public bool Dead;
        public bool Won;
        public readonly List<int> Collected = new List<int>();
    }

    public sealed class DashState
    {
        public DashLevel Level { get; }
        public int Pos { get; private set; }
        public int Gems { get; private set; }
        public int Moves { get; private set; }
        public int Deaths { get; private set; }
        public bool Won { get; private set; }
        public readonly List<int> Log = new List<int>();

        public DashState(DashLevel level)
        {
            Level = level;
            Pos = level.Start;
        }

        public bool ExitOpen => Gems == Level.AllGems;

        public static DashSlide Slide(DashLevel level, int from, int gems, int dir)
        {
            var slide = new DashSlide { End = from, Gems = gems };
            var (dx, dy) = Challenges.Directions[dir];
            int x = from % DashLevel.Width, y = from / DashLevel.Width;
            while (true)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= DashLevel.Width || ny >= DashLevel.Height) break;
                int next = nx + DashLevel.Width * ny;
                if (level.Tiles[next] == DashTile.Wall) break;
                x = nx;
                y = ny;
                slide.Path.Add(next);
                slide.End = next;
                if (level.Tiles[next] == DashTile.Spike)
                {
                    slide.Dead = true;
                    break;
                }
                int gemIndex = Array.IndexOf(level.Gems, next);
                if (gemIndex >= 0 && (slide.Gems & (1 << gemIndex)) == 0)
                {
                    slide.Gems |= 1 << gemIndex;
                    slide.Collected.Add(next);
                }
                if (next == level.Exit && slide.Gems == level.AllGems)
                {
                    slide.Won = true;
                    break;
                }
            }
            return slide;
        }

        public DashSlide Move(int dir)
        {
            if (Won) return new DashSlide { End = Pos, Gems = Gems };
            var slide = Slide(Level, Pos, Gems, dir);
            if (slide.Path.Count == 0) return slide;
            Log.Add(dir);
            Moves++;
            if (slide.Dead)
            {
                Deaths++;
                Pos = Level.Start;
                Gems = 0;
                return slide;
            }
            Pos = slide.End;
            Gems = slide.Gems;
            Won = slide.Won;
            return slide;
        }

        public void Restart()
        {
            Pos = Level.Start;
            Gems = 0;
            Moves = 0;
            Log.Add(-1);
        }

        /// <summary>Par → ★★★, par+3 → ★★, else ★ (RESTART zeroes the count; spikes keep it).</summary>
        public int Stars => !Won ? 0 : Moves <= Level.Par ? 3 : Moves <= Level.Par + 3 ? 2 : 1;
    }
}
