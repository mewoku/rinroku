using System;
using System.Collections.Generic;

namespace Ronriku.Domain.Arcade
{
    public enum EnemyKind { Slime = 0, Bat = 1, Skeleton = 2 }

    public sealed class Enemy
    {
        public EnemyKind Kind;
        public int Pos;
        public int Hp;
        public int Dir;   // slime: 0 up / 2 down; bat: heading

        public Enemy Clone() => new Enemy { Kind = Kind, Pos = Pos, Hp = Hp, Dir = Dir };
    }

    /// <summary>
    /// Beat crawl: a one-room dungeon where every monster acts on the music's beat. The hero moves one
    /// tile per swipe; bumping a monster hits it. Clear the room, then take the stairs. Monsters show
    /// where they'll step next, so reading them is the puzzle:
    ///   slime    — hops up/down every second beat
    ///   bat      — flies straight every beat, turns right at walls
    ///   skeleton — steps toward you every second beat
    /// </summary>
    public sealed class CrawlLevel
    {
        public const int Width = 7;
        public const int Height = 9;

        public bool[] Walls { get; }
        public int Start { get; }
        public int Stairs { get; }
        public IReadOnlyList<Enemy> Enemies { get; }
        public long Seed { get; }

        private CrawlLevel(long seed, bool[] walls, int start, int stairs, List<Enemy> enemies)
        {
            Seed = seed;
            Walls = walls;
            Start = start;
            Stairs = stairs;
            Enemies = enemies;
        }

        public static CrawlLevel Generate(long seed, int tier)
        {
            var rng = new DeterministicRandom(unchecked((ulong)seed));
            int enemyCount = tier > 0 ? 5 : 3;
            for (int attempt = 0; attempt < 500; attempt++)
            {
                var walls = new bool[Width * Height];
                int pillars = 6 + rng.NextInt(6);
                for (int i = 0; i < pillars; i++)
                {
                    int x = 1 + rng.NextInt(Width - 2), y = 1 + rng.NextInt(Height - 3);
                    walls[x + Width * y] = true;
                }
                int start = 1 + rng.NextInt(Width - 2) + Width * (Height - 1);
                int stairs = 1 + rng.NextInt(Width - 2);
                if (walls[start] || walls[stairs]) continue;
                if (!Connected(walls, start)) continue;

                var enemies = new List<Enemy>();
                int guard = 0;
                while (enemies.Count < enemyCount && guard++ < 200)
                {
                    int cell = rng.NextInt(walls.Length);
                    if (walls[cell] || cell == start || cell == stairs || Distance(cell, start) < 4) continue;
                    if (enemies.Exists(e => e.Pos == cell)) continue;
                    var kind = (EnemyKind)(enemies.Count % 3);
                    enemies.Add(new Enemy
                    {
                        Kind = kind,
                        Pos = cell,
                        Hp = kind == EnemyKind.Skeleton && tier > 0 ? 2 : 1,
                        Dir = kind == EnemyKind.Bat ? rng.NextInt(4) : 0
                    });
                }
                if (enemies.Count < enemyCount) continue;
                return new CrawlLevel(seed, walls, start, stairs, enemies);
            }
            throw new InvalidOperationException("crawl generator exhausted");
        }

        public static int Distance(int a, int b) =>
            Math.Abs(a % Width - b % Width) + Math.Abs(a / Width - b / Width);

        private static bool Connected(bool[] walls, int from)
        {
            var seen = new bool[walls.Length];
            var stack = new Stack<int>();
            stack.Push(from);
            seen[from] = true;
            int reached = 1;
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                for (int d = 0; d < 4; d++)
                {
                    int n = Step(c, d);
                    if (n < 0 || walls[n] || seen[n]) continue;
                    seen[n] = true;
                    reached++;
                    stack.Push(n);
                }
            }
            int floor = 0;
            foreach (bool w in walls) if (!w) floor++;
            return reached == floor;
        }

        /// <summary>Neighbouring cell in a direction (0 up, 1 right, 2 down, 3 left) or -1 off the board.</summary>
        public static int Step(int cell, int dir)
        {
            var (dx, dy) = Challenges.Directions[dir];
            int x = cell % Width + dx, y = cell / Width + dy;
            if (x < 0 || y < 0 || x >= Width || y >= Height) return -1;
            return x + Width * y;
        }
    }

    public enum HeroAction { Moved, Attacked, Killed, Bumped, Won }

    public sealed class CrawlState
    {
        public const int MaxHearts = 3;

        private readonly List<Enemy> _enemies = new List<Enemy>();

        public CrawlLevel Level { get; }
        public int Hero { get; private set; }
        public int Hearts { get; private set; } = MaxHearts;
        public int Beat { get; private set; }
        public int Coins { get; private set; }
        public int Moves { get; private set; }
        public bool Won { get; private set; }
        public bool Lost => Hearts <= 0;
        public IReadOnlyList<Enemy> Enemies => _enemies;
        public bool StairsOpen => _enemies.Count == 0;
        /// <summary>Cells where an enemy hit the hero on the last beat (for flashes).</summary>
        public readonly List<int> HitsLastBeat = new List<int>();

        public CrawlState(CrawlLevel level)
        {
            Level = level;
            Hero = level.Start;
            foreach (var e in level.Enemies) _enemies.Add(e.Clone());
        }

        private bool Blocked(int cell) => cell < 0 || Level.Walls[cell];
        private Enemy EnemyAt(int cell) => _enemies.Find(e => e.Pos == cell);

        public HeroAction Act(int dir)
        {
            if (Won || Lost) return HeroAction.Bumped;
            int target = CrawlLevel.Step(Hero, dir);
            if (Blocked(target)) return HeroAction.Bumped;
            var enemy = EnemyAt(target);
            if (enemy != null)
            {
                enemy.Hp--;
                if (enemy.Hp > 0) return HeroAction.Attacked;
                _enemies.Remove(enemy);
                Coins++;
                return HeroAction.Killed;
            }
            Hero = target;
            Moves++;
            if (StairsOpen && Hero == Level.Stairs)
            {
                Won = true;
                return HeroAction.Won;
            }
            return HeroAction.Moved;
        }

        /// <summary>Where this enemy intends to step on the coming beat, or -1 if it waits.</summary>
        public int Intent(Enemy e) => Intent(e, Beat + 1);

        private int Intent(Enemy e, int beat)
        {
            switch (e.Kind)
            {
                case EnemyKind.Slime:
                    return beat % 2 == 0 ? e.Dir : -1;
                case EnemyKind.Bat:
                    return e.Dir;
                default:
                    if (beat % 2 == 0) return -1;
                    int hx = Hero % CrawlLevel.Width, hy = Hero / CrawlLevel.Width;
                    int ex = e.Pos % CrawlLevel.Width, ey = e.Pos / CrawlLevel.Width;
                    int horizontal = hx > ex ? 1 : hx < ex ? 3 : -1;
                    int vertical = hy > ey ? 2 : hy < ey ? 0 : -1;
                    int first = Math.Abs(hx - ex) >= Math.Abs(hy - ey) ? horizontal : vertical;
                    int second = first == horizontal ? vertical : horizontal;
                    if (first >= 0 && !Blocked(CrawlLevel.Step(e.Pos, first)) && EnemyAt(CrawlLevel.Step(e.Pos, first)) == null) return first;
                    if (second >= 0 && !Blocked(CrawlLevel.Step(e.Pos, second)) && EnemyAt(CrawlLevel.Step(e.Pos, second)) == null) return second;
                    return first;
            }
        }

        /// <summary>Advances one beat: every monster acts. Returns true if the hero got hit.</summary>
        public bool Tick()
        {
            HitsLastBeat.Clear();
            if (Won || Lost) return false;
            Beat++;
            foreach (var e in _enemies)
            {
                int dir = Intent(e, Beat);
                if (dir < 0) continue;
                int target = CrawlLevel.Step(e.Pos, dir);
                if (target == Hero)
                {
                    Hearts--;
                    HitsLastBeat.Add(e.Pos);
                    continue;
                }
                if (Blocked(target) || EnemyAt(target) != null)
                {
                    if (e.Kind == EnemyKind.Slime) e.Dir = e.Dir == 0 ? 2 : 0;
                    else if (e.Kind == EnemyKind.Bat) e.Dir = (e.Dir + 1) % 4;
                    continue;
                }
                e.Pos = target;
            }
            return HitsLastBeat.Count > 0;
        }

        public int Stars => !Won ? 0 : Math.Max(1, Hearts);
    }
}
