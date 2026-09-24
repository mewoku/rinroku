using System;
using System.Collections.Generic;
using Ronriku.Domain.Daily;
using Ronriku.Domain.Figures;
using Ronriku.Domain.Player;
using Ronriku.Domain.Puzzles;

namespace Ronriku.Domain
{
    public static class Hashing
    {
        /// <summary>FNV-1a 64 over UTF-16 code units, as used by DailyCalendar.Seed.</summary>
        public static ulong Fnv1a(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in text)
            {
                unchecked
                {
                    hash ^= c;
                    hash *= 1099511628211UL;
                }
            }
            return hash;
        }
    }
}

namespace Ronriku.Domain.Adventure
{
    /// <summary>
    /// A level is never stored as a map: it is derived from (world, index). This is the contract shared
    /// with packages/core `levelDef`:
    ///   worldSeed = Mix(FNV1a("ronriku:world:v1:" + world), 0)
    ///   seed      = Mix(worldSeed, index)
    ///   kind      = [Pattern, Spatial, Logic][index % 3]; index 11 is the world boss
    ///   difficulty = Standard (middle band) for every level
    /// </summary>
    public sealed class LevelDef
    {
        public const int LevelsPerWorld = 12;
        public const int BossIndex = LevelsPerWorld - 1;
        public const int WorldCount = 5;

        public static readonly string[] WorldNames = { "LAB", "PRISM", "EMBER", "GROVE", "FROST" };

        public int World { get; }
        public int Index { get; }
        public long Seed { get; }
        public TrialKind Kind { get; }
        public PuzzleDifficulty Difficulty => PuzzleDifficulty.Standard;
        public bool IsBoss => Index == BossIndex;

        private LevelDef(int world, int index, long seed, TrialKind kind)
        {
            World = world;
            Index = index;
            Seed = seed;
            Kind = kind;
        }

        public static long WorldSeed(int world) =>
            DailyPlan.Mix(unchecked((long)Hashing.Fnv1a("ronriku:world:v1:" + world)), 0);

        public static LevelDef For(int world, int index)
        {
            if (world < 0 || world >= WorldCount) throw new ArgumentOutOfRangeException(nameof(world));
            if (index < 0 || index >= LevelsPerWorld) throw new ArgumentOutOfRangeException(nameof(index));
            long seed = DailyPlan.Mix(WorldSeed(world), index);
            TrialKind kind = (TrialKind)(index % 3);
            return new LevelDef(world, index, seed, kind);
        }

        /// <summary>Guardian monster: grows from 3×3 to 5×5 through the world; the boss is always 5×5.</summary>
        public Figure Monster() => FigureGenerator.Generate(unchecked((ulong)Seed), IsBoss ? 5 : 3 + Index * 2 / BossIndex, true);

        /// <summary>Boss fights chain three trials: Pattern, Spatial, Logic.</summary>
        public IReadOnlyList<(TrialKind kind, long seed)> BossStages() => new[]
        {
            (TrialKind.Pattern, DailyPlan.Mix(Seed, 101)),
            (TrialKind.Spatial, DailyPlan.Mix(Seed, 102)),
            (TrialKind.Logic, DailyPlan.Mix(Seed, 103))
        };
    }

    public static class AdventureProgress
    {
        public static bool IsUnlocked(PlayerProfile profile, int world, int index)
        {
            if (index == 0) return world == 0 || IsCleared(profile, world - 1, LevelDef.BossIndex);
            return IsCleared(profile, world, index - 1);
        }

        public static bool IsCleared(PlayerProfile profile, int world, int index) =>
            (profile.LevelRecordFor(world, index)?.stars ?? 0) > 0;

        /// <summary>The first unlocked, uncleared level, or the last level when everything is done.</summary>
        public static (int world, int index) Next(PlayerProfile profile)
        {
            for (int w = 0; w < LevelDef.WorldCount; w++)
            for (int i = 0; i < LevelDef.LevelsPerWorld; i++)
                if (!IsCleared(profile, w, i)) return (w, i);
            return (LevelDef.WorldCount - 1, LevelDef.BossIndex);
        }

        public static int StarsIn(PlayerProfile profile, int world)
        {
            int stars = 0;
            foreach (var r in profile.levels) if (r.world == world) stars += r.stars;
            return stars;
        }

        /// <summary>
        /// Records a clear. First clear pays the full reward; replays pay only for newly earned stars.
        /// Returns shards earned.
        /// </summary>
        public static int Complete(PlayerProfile profile, int world, int index, int stars, int elapsedMs)
        {
            stars = Math.Max(1, Math.Min(3, stars));
            LevelRecord record = profile.LevelRecordFor(world, index);
            int earned;
            if (record == null)
            {
                record = new LevelRecord { world = world, level = index, stars = stars, bestMs = elapsedMs };
                profile.levels.Add(record);
                earned = Economy.LevelReward(stars, index == LevelDef.BossIndex);
            }
            else
            {
                earned = Math.Max(0, stars - record.stars) * Economy.StarBonus;
                record.stars = Math.Max(record.stars, stars);
                record.bestMs = record.bestMs <= 0 ? elapsedMs : Math.Min(record.bestMs, elapsedMs);
            }
            profile.shards += earned;
            return earned;
        }

        /// <summary>1 star = solved, 2 = at par / no mistakes, 3 = also under the target time.</summary>
        public static int Stars(TrialOutcome outcome)
        {
            if (!outcome.Solved) return 0;
            bool clean = outcome.Par <= 0 || outcome.Moves <= outcome.Par;
            bool fast = outcome.ElapsedMilliseconds <= outcome.TargetMilliseconds;
            return clean ? (fast ? 3 : 2) : 1;
        }
    }
}

namespace Ronriku.Domain.Player
{
    /// <summary>Shard economy constants (docs/PLAN_V2.md §6). Server-authoritative once online.</summary>
    public static class Economy
    {
        public const int LevelBase = 20;
        public const int StarBonus = 10;
        public const int BossClear = 300;
        public const int BossEventEntry = 150;
        public const int BossRetry = 50;

        public static int LevelReward(int stars, bool boss) => (boss ? BossClear : LevelBase) + (stars - 1) * StarBonus;

        public static int DailyReward(int streak) => 100 + Math.Min(100, 10 * Math.Max(0, streak));

        /// <summary>Shard price by rarity; Legendary figures are SOL-only (returns -1).</summary>
        public static int FigurePrice(Figures.FigureRarity rarity) => rarity switch
        {
            Figures.FigureRarity.Common => 300,
            Figures.FigureRarity.Rare => 800,
            Figures.FigureRarity.Epic => 2000,
            _ => -1
        };
    }
}
