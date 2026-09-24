using System;
using System.Collections.Generic;
using NUnit.Framework;
using Ronriku.Domain.Adventure;
using Ronriku.Domain.Daily;
using Ronriku.Domain.Figures;
using Ronriku.Domain.Player;
using Ronriku.Domain.Puzzles;
using Ronriku.Domain.Shop;

namespace Ronriku.Tests
{
    public sealed class AdventureShopTests
    {
        [Test]
        public void LevelDefs_AreDeterministic_RotateKinds_AndGenerateValidPuzzles()
        {
            var seeds = new HashSet<long>();
            for (int w = 0; w < LevelDef.WorldCount; w++)
            for (int i = 0; i < LevelDef.LevelsPerWorld; i++)
            {
                LevelDef a = LevelDef.For(w, i), b = LevelDef.For(w, i);
                Assert.That(b.Seed, Is.EqualTo(a.Seed));
                Assert.That(seeds.Add(a.Seed), Is.True);
                Assert.That(a.Kind, Is.EqualTo((TrialKind)(i % 3)));
                Assert.That(a.IsBoss, Is.EqualTo(i == LevelDef.BossIndex));
                Assert.That(a.Difficulty, Is.EqualTo(PuzzleDifficulty.Standard), "middle band everywhere");
                IEnumerable<(TrialKind kind, long seed)> puzzles = a.IsBoss ? a.BossStages() : new[] { (a.Kind, a.Seed) };
                foreach (var (kind, seed) in puzzles)
                {
                    string violation = kind switch
                    {
                        TrialKind.Pattern => PatternPuzzleInvariants.Check(new PatternPuzzleGenerator().Generate(seed, PuzzleDifficulty.Standard, 0)),
                        TrialKind.Logic => LogicPuzzleInvariants.Check(new LogicPuzzleGenerator().Generate(seed, PuzzleDifficulty.Standard, 0)),
                        _ => SpatialPuzzleInvariants.Check(new SpatialPuzzleGenerator().Generate(seed, PuzzleDifficulty.Standard, 0))
                    };
                    Assert.That(violation, Is.Null, $"w{w} l{i} {kind}");
                }
                Assert.That(a.Monster().Size, Is.EqualTo(a.IsBoss ? 5 : 3 + i * 2 / LevelDef.BossIndex));
            }
        }

        [Test]
        public void Progress_UnlocksSequentially_AndRewardsOnlyNewStars()
        {
            var p = PlayerProfile.CreateNew("hero1234");
            int start = p.shards;
            Assert.That(AdventureProgress.IsUnlocked(p, 0, 0), Is.True);
            Assert.That(AdventureProgress.IsUnlocked(p, 0, 1), Is.False);
            Assert.That(AdventureProgress.IsUnlocked(p, 1, 0), Is.False);

            Assert.That(AdventureProgress.Complete(p, 0, 0, 1, 30000), Is.EqualTo(Economy.LevelReward(1, false)));
            Assert.That(AdventureProgress.IsUnlocked(p, 0, 1), Is.True);
            Assert.That(AdventureProgress.Complete(p, 0, 0, 1, 20000), Is.Zero, "replay without new stars pays nothing");
            Assert.That(AdventureProgress.Complete(p, 0, 0, 3, 10000), Is.EqualTo(2 * Economy.StarBonus));
            Assert.That(p.LevelRecordFor(0, 0).bestMs, Is.EqualTo(10000));
            Assert.That(p.shards, Is.EqualTo(start + Economy.LevelReward(1, false) + 2 * Economy.StarBonus));

            for (int i = 1; i < LevelDef.LevelsPerWorld; i++) AdventureProgress.Complete(p, 0, i, 2, 40000);
            Assert.That(AdventureProgress.IsUnlocked(p, 1, 0), Is.True, "clearing the boss opens the next world");
            Assert.That(AdventureProgress.Next(p), Is.EqualTo((1, 0)));
        }

        [Test]
        public void Stars_FollowSolveParAndTime()
        {
            TrialOutcome O(bool solved, int ms, int moves, int par) =>
                new TrialOutcome(TrialKind.Spatial, PuzzleDifficulty.Standard, solved, ms, 45000, moves, par, 0, 1000);
            Assert.That(AdventureProgress.Stars(O(false, 1000, 1, 3)), Is.Zero);
            Assert.That(AdventureProgress.Stars(O(true, 60000, 9, 3)), Is.EqualTo(1));
            Assert.That(AdventureProgress.Stars(O(true, 60000, 3, 3)), Is.EqualTo(2));
            Assert.That(AdventureProgress.Stars(O(true, 20000, 3, 3)), Is.EqualTo(3));
        }

        [Test]
        public void Shop_IsDailyDeterministic_AndPurchasesAreValidated()
        {
            IReadOnlyList<ShopItem> a = ShopCatalogue.ForDay(30), b = ShopCatalogue.ForDay(30), c = ShopCatalogue.ForDay(31);
            Assert.That(a.Count, Is.EqualTo(ShopCatalogue.ShelfSize));
            for (int i = 0; i < a.Count; i++) Assert.That(b[i].Id, Is.EqualTo(a[i].Id));
            Assert.That(c[0].Id, Is.Not.EqualTo(a[0].Id));

            var p = PlayerProfile.CreateNew("buyer123");
            ShopItem affordable = null;
            for (int day = 1; affordable == null && day < 400; day++)
                foreach (ShopItem item in ShopCatalogue.ForDay(day))
                    if (!item.SolOnly) { affordable = item; break; }
            Assert.That(affordable, Is.Not.Null);

            p.shards = affordable.PriceShards - 1;
            Assert.That(ShopCatalogue.Buy(p, affordable), Is.EqualTo(PurchaseResult.InsufficientShards));
            p.shards = affordable.PriceShards;
            Assert.That(ShopCatalogue.Buy(p, affordable), Is.EqualTo(PurchaseResult.Ok));
            Assert.That(p.shards, Is.Zero);
            Assert.That(ShopCatalogue.Buy(p, affordable), Is.EqualTo(PurchaseResult.AlreadyOwned));
            Assert.That(ShopCatalogue.Build(p.figures[p.figures.Count - 1]).Encode(), Is.EqualTo(affordable.Figure.Encode()));
        }

        [Test]
        public void Legendary_IsSolOnly()
        {
            Assert.That(Economy.FigurePrice(FigureRarity.Legendary), Is.EqualTo(-1));
            Assert.That(Economy.FigurePrice(FigureRarity.Common), Is.LessThan(Economy.FigurePrice(FigureRarity.Epic)));
        }

        [Test]
        public void Bosses_RotateWeekly_AndChargeEntry()
        {
            var monday = new DateTime(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc);
            IReadOnlyList<BossEvent> week = BossEvent.ForWeek(monday.AddDays(3));
            IReadOnlyList<BossEvent> same = BossEvent.ForWeek(monday.AddHours(1));
            IReadOnlyList<BossEvent> next = BossEvent.ForWeek(monday.AddDays(7));
            Assert.That(week.Count, Is.EqualTo(3));
            Assert.That(same[0].Id, Is.EqualTo(week[0].Id));
            Assert.That(next[0].Id, Is.Not.EqualTo(week[0].Id));
            Assert.That(week[0].EndsUtc, Is.EqualTo(monday.AddDays(7)));

            var p = PlayerProfile.CreateNew("raider12");
            p.shards = week[0].EntryShards - 1;
            Assert.That(week[0].TryEnter(p), Is.False);
            p.shards = week[0].EntryShards;
            Assert.That(week[0].TryEnter(p), Is.True);
            Assert.That(p.shards, Is.Zero);
            Assert.That(week[0].RecordWin(p), Is.EqualTo(week[0].Reward), "first win pays");
            Assert.That(week[0].RecordWin(p), Is.Zero, "repeat wins pay nothing");
            Assert.That(p.shards, Is.EqualTo(week[0].Reward));
        }

        [Test]
        public void Profile_V1Migrates_WithStarterFigure_AndDailyPaysShards()
        {
            var v1 = new PlayerProfile { schemaVersion = 1, playerId = "legacy01", figures = null, levels = null };
            Assert.That(v1.Migrate(), Is.True);
            Assert.That(v1.schemaVersion, Is.EqualTo(PlayerProfile.CurrentSchemaVersion));
            Assert.That(v1.figures.Count, Is.EqualTo(1));
            Assert.That(v1.Avatar, Is.Not.Null);
            Assert.That(v1.shards, Is.GreaterThan(0));

            int before = v1.shards;
            var session = new DailySession(DailyPlan.For(50));
            while (!session.IsComplete)
                session.Record(new TrialOutcome(session.Current.Kind, PuzzleDifficulty.Standard, true, 20000, 45000, 3, 3, 0, 1000));
            DailyResult result = DailyCompletion.Apply(v1, session);
            Assert.That(result.ShardsEarned, Is.EqualTo(Economy.DailyReward(1)));
            Assert.That(v1.shards, Is.EqualTo(before + result.ShardsEarned));
        }
    }
}
