using System;
using System.Collections.Generic;
using Ronriku.Domain.Player;
using Ronriku.Domain.Puzzles;

namespace Ronriku.Domain.Daily
{
    /// <summary>
    /// UTC calendar for the Daily. Day 1 is 2026-09-01. The day boundary is 00:00 UTC.
    /// </summary>
    public static class DailyCalendar
    {
        public const int RulesVersion = 2;
        public static readonly DateTime Epoch = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        public static int DayNumber(DateTime utcNow) =>
            (int)Math.Floor((ToUtc(utcNow).Date - Epoch).TotalDays) + 1;

        public static DateTime DateOf(int day) => Epoch.AddDays(day - 1);

        public static TimeSpan UntilReset(DateTime utcNow)
        {
            DateTime utc = ToUtc(utcNow);
            return utc.Date.AddDays(1) - utc;
        }

        public static string ChallengeId(int day) => $"daily-{DateOf(day):yyyyMMdd}-v{RulesVersion}";

        public static long Seed(int day)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in $"ronriku:daily:{RulesVersion}:{DateOf(day):yyyy-MM-dd}")
            {
                unchecked
                {
                    hash ^= c;
                    hash *= 1099511628211UL;
                }
            }
            return unchecked((long)hash);
        }

        private static DateTime ToUtc(DateTime value) =>
            value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    public enum TrialKind
    {
        Pattern = 0,
        Spatial = 1,
        Logic = 2
    }

    public sealed class TrialSpec
    {
        public int Index { get; }
        public TrialKind Kind { get; }
        public PuzzleDifficulty Difficulty { get; }
        public long Seed { get; }

        public TrialSpec(int index, TrialKind kind, PuzzleDifficulty difficulty, long seed)
        {
            Index = index;
            Kind = kind;
            Difficulty = difficulty;
            Seed = seed;
        }
    }

    /// <summary>The three trials of one Daily. Identical for every player on the same UTC day.</summary>
    public sealed class DailyPlan
    {
        public const int TrialCount = 3;

        public int Day { get; }
        public string ChallengeId { get; }
        public long Seed { get; }
        public IReadOnlyList<TrialSpec> Trials { get; }

        private DailyPlan(int day, IReadOnlyList<TrialSpec> trials)
        {
            Day = day;
            ChallengeId = DailyCalendar.ChallengeId(day);
            Seed = DailyCalendar.Seed(day);
            Trials = trials;
        }

        /// <summary>
        /// Pattern, Spatial, Logic — all Standard difficulty. Changing this line-up requires bumping
        /// <see cref="DailyCalendar.RulesVersion"/>.
        /// </summary>
        public static DailyPlan For(int day)
        {
            long seed = DailyCalendar.Seed(day);
            var trials = new List<TrialSpec>
            {
                new TrialSpec(0, TrialKind.Pattern, PuzzleDifficulty.Standard, Mix(seed, 1)),
                new TrialSpec(1, TrialKind.Spatial, PuzzleDifficulty.Standard, Mix(seed, 2)),
                new TrialSpec(2, TrialKind.Logic, PuzzleDifficulty.Standard, Mix(seed, 3))
            };
            return new DailyPlan(day, trials.AsReadOnly());
        }

        /// <summary>Seed for decorative Home preview content that must not reveal any trial.</summary>
        public long PreviewSeed => Mix(Seed, 0x5052455649455700L);

        /// <summary>SplitMix64 finaliser over seed + stream.</summary>
        public static long Mix(long seed, long stream)
        {
            unchecked
            {
                ulong z = (ulong)seed + 0x9E3779B97F4A7C15UL * (ulong)(stream + 1);
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return (long)(z ^ (z >> 31));
            }
        }
    }

    /// <summary>Progress through one Daily run. Outcomes are recorded in trial order.</summary>
    public sealed class DailySession
    {
        private readonly List<TrialOutcome> _outcomes = new List<TrialOutcome>();

        public DailyPlan Plan { get; }
        public IReadOnlyList<TrialOutcome> Outcomes => _outcomes;
        public bool IsComplete => _outcomes.Count >= Plan.Trials.Count;
        public TrialSpec Current => IsComplete ? null : Plan.Trials[_outcomes.Count];

        public DailySession(DailyPlan plan) => Plan = plan ?? throw new ArgumentNullException(nameof(plan));

        public void Record(TrialOutcome outcome)
        {
            if (IsComplete) throw new InvalidOperationException("Daily session is already complete.");
            if (outcome.Kind != Current.Kind) throw new ArgumentException($"Expected {Current.Kind} outcome.");
            _outcomes.Add(outcome);
        }
    }
}
