using System;
using System.Collections.Generic;
using Ronriku.Domain.Daily;
using Ronriku.Domain.Figures;
using Ronriku.Domain.Player;

namespace Ronriku.Domain.Adventure
{
    /// <summary>
    /// A weekly boss event: three chained trials, paid entry, bigger reward. Offline the roster is derived
    /// from the ISO-like week number (weeks start Monday 00:00 UTC); online it comes from `boss_events`.
    /// </summary>
    public sealed class BossEvent
    {
        public string Id { get; }
        public long Seed { get; }
        public int Tier { get; }
        public Figure Monster { get; }
        public int EntryShards { get; }
        public int Reward { get; }
        public DateTime EndsUtc { get; }

        public BossEvent(string id, long seed, int tier, DateTime endsUtc)
        {
            Id = id;
            Seed = seed;
            Tier = tier;
            Monster = FigureGenerator.Generate(unchecked((ulong)seed), 5, true);
            EntryShards = Economy.BossEventEntry + tier * 50;
            Reward = 450 + tier * 250;
            EndsUtc = endsUtc;
        }

        public string Name => Monster.Name;

        public IReadOnlyList<(TrialKind kind, long seed)> Stages() => new[]
        {
            (TrialKind.Pattern, DailyPlan.Mix(Seed, 201)),
            (TrialKind.Spatial, DailyPlan.Mix(Seed, 202)),
            (TrialKind.Logic, DailyPlan.Mix(Seed, 203))
        };

        public static IReadOnlyList<BossEvent> ForWeek(DateTime utcNow)
        {
            DateTime day = utcNow.Kind == DateTimeKind.Utc ? utcNow.Date : utcNow.ToUniversalTime().Date;
            int sinceMonday = ((int)day.DayOfWeek + 6) % 7;
            DateTime weekStart = day.AddDays(-sinceMonday);
            // Floor, not truncation: the epoch is a Tuesday, so the first week starts before it (week -1).
            int week = (int)Math.Floor((weekStart - DailyCalendar.Epoch).TotalDays / 7.0);
            long weekSeed = DailyPlan.Mix(unchecked((long)Hashing.Fnv1a("ronriku:boss:v1:" + week)), 0);
            var events = new List<BossEvent>();
            for (int tier = 0; tier < 3; tier++)
                events.Add(new BossEvent($"boss-w{week}-{tier}", DailyPlan.Mix(weekSeed, tier), tier, weekStart.AddDays(7)));
            return events;
        }

        /// <summary>Deducts the entry. Returns false when the player cannot afford it.</summary>
        public bool TryEnter(PlayerProfile profile)
        {
            if (profile.shards < EntryShards) return false;
            profile.shards -= EntryShards;
            return true;
        }
    }
}
