using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Ronriku.Domain.Daily;
using Ronriku.Domain.Player;
using Ronriku.Domain.Puzzles;
using Ronriku.Infrastructure.Persistence;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronriku.Tests
{
    public sealed class DailyTests
    {
        private static DateTime Utc(int y, int m, int d, int h = 0, int min = 0) =>
            new DateTime(y, m, d, h, min, 0, DateTimeKind.Utc);

        [Test]
        public void Calendar_NumbersDaysFromEpoch_AndResetsAtUtcMidnight()
        {
            Assert.That(DailyCalendar.DayNumber(Utc(2026, 9, 1)), Is.EqualTo(1));
            Assert.That(DailyCalendar.DayNumber(Utc(2026, 9, 23, 23, 59)), Is.EqualTo(23));
            Assert.That(DailyCalendar.DayNumber(Utc(2026, 9, 24)), Is.EqualTo(24));
            Assert.That(DailyCalendar.UntilReset(Utc(2026, 9, 23, 22, 30)), Is.EqualTo(TimeSpan.FromMinutes(90)));
            Assert.That(DailyCalendar.ChallengeId(23), Is.EqualTo("daily-20260923-v1"));
        }

        [Test]
        public void Plan_IsDeterministicPerDay_AndDiffersBetweenDays()
        {
            var seeds = new HashSet<long>();
            for (int day = 1; day <= 400; day++)
            {
                DailyPlan a = DailyPlan.For(day), b = DailyPlan.For(day);
                Assert.That(a.Trials.Count, Is.EqualTo(DailyPlan.TrialCount));
                for (int i = 0; i < a.Trials.Count; i++)
                {
                    Assert.That(b.Trials[i].Seed, Is.EqualTo(a.Trials[i].Seed));
                    Assert.That(seeds.Add(a.Trials[i].Seed), Is.True, $"seed collision day {day} trial {i}");
                }
            }
        }

        [Test]
        public void Plan_TrialsGenerateValidSpatialPuzzles()
        {
            var generator = new SpatialPuzzleGenerator();
            for (int day = 1; day <= 120; day++)
            foreach (TrialSpec spec in DailyPlan.For(day).Trials)
                Assert.That(SpatialPuzzleInvariants.Check(generator.Generate(spec.Seed, spec.Difficulty, 0)), Is.Null);
        }

        [Test]
        public void Session_RecordsInOrder_AndRejectsExtraOutcomes()
        {
            var session = new DailySession(DailyPlan.For(10));
            for (int i = 0; i < DailyPlan.TrialCount; i++)
            {
                Assert.That(session.Current.Index, Is.EqualTo(i));
                session.Record(Outcome(true));
            }
            Assert.That(session.IsComplete, Is.True);
            Assert.That(session.Current, Is.Null);
            Assert.Throws<InvalidOperationException>(() => session.Record(Outcome(true)));
        }

        [Test]
        public void Performance_RewardsSpeedAndPar_AndIsZeroWhenUnsolved()
        {
            double fastAtPar = ReasoningRating.Performance(Outcome(true, 5000, 3, 3));
            double slowOverPar = ReasoningRating.Performance(Outcome(true, 40000, 9, 3));
            Assert.That(fastAtPar, Is.GreaterThan(slowOverPar));
            Assert.That(fastAtPar, Is.InRange(0.0, 1.0));
            Assert.That(ReasoningRating.Performance(Outcome(false)), Is.Zero);
        }

        [Test]
        public void Rating_IsDeterministic_Bounded_AndMonotonic()
        {
            var good = new[] { Outcome(true, 5000, 3, 3), Outcome(true, 8000, 3, 3), Outcome(true, 12000, 4, 4) };
            var poor = new[] { Outcome(false), Outcome(true, 50000, 12, 3), Outcome(false) };

            int up = ReasoningRating.UpdateOverall(1200, 0, good);
            int down = ReasoningRating.UpdateOverall(1200, 0, poor);
            Assert.That(up, Is.GreaterThan(1200));
            Assert.That(down, Is.LessThan(1200));
            Assert.That(ReasoningRating.UpdateOverall(1200, 0, good), Is.EqualTo(up));
            Assert.That(Math.Abs(up - 1200), Is.LessThanOrEqualTo(ReasoningRating.ProvisionalK));
            Assert.That(Math.Abs(ReasoningRating.UpdateOverall(1200, 50, good) - 1200),
                Is.LessThanOrEqualTo(ReasoningRating.EstablishedK));
            var failed = new[] { Outcome(false), Outcome(false), Outcome(false) };
            var perfect = new[] { Outcome(true, 0, 3, 3), Outcome(true, 0, 3, 3), Outcome(true, 0, 3, 3) };
            Assert.That(ReasoningRating.UpdateOverall(ReasoningRating.Minimum, 0, failed), Is.EqualTo(ReasoningRating.Minimum));
            Assert.That(ReasoningRating.UpdateOverall(ReasoningRating.Maximum, 0, perfect), Is.EqualTo(ReasoningRating.Maximum));
        }

        [Test]
        public void Completion_FirstRunCounts_ReplayIsPractice()
        {
            var profile = PlayerProfile.CreateNew("abcd1234");
            DailyResult first = DailyCompletion.Apply(profile, Completed(30));
            Assert.That(first.Counted, Is.True);
            Assert.That(profile.completedDailies, Is.EqualTo(1));
            Assert.That(profile.streak, Is.EqualTo(1));
            Assert.That(first.RatingAfter, Is.EqualTo(profile.rating));
            Assert.That(first.Skills, Is.Not.Empty);

            int rating = profile.rating;
            DailyResult replay = DailyCompletion.Apply(profile, Completed(30));
            Assert.That(replay.Counted, Is.False);
            Assert.That(profile.rating, Is.EqualTo(rating));
            Assert.That(profile.completedDailies, Is.EqualTo(1));
        }

        [Test]
        public void Streak_ConsecutiveGapAndClockRewind()
        {
            var profile = PlayerProfile.CreateNew("abcd1234");
            DailyCompletion.Apply(profile, Completed(30));
            DailyCompletion.Apply(profile, Completed(31));
            DailyCompletion.Apply(profile, Completed(32));
            Assert.That(profile.streak, Is.EqualTo(3));
            Assert.That(profile.DisplayStreak(33), Is.EqualTo(3), "still alive the next day");
            Assert.That(profile.DisplayStreak(34), Is.EqualTo(0), "shown broken after a missed day");

            Assert.That(DailyCompletion.Apply(profile, Completed(29)).Counted, Is.False, "earlier day does not count");
            Assert.That(profile.streak, Is.EqualTo(3));

            DailyCompletion.Apply(profile, Completed(35));
            Assert.That(profile.streak, Is.EqualTo(1));
            Assert.That(profile.bestStreak, Is.EqualTo(3));
        }

        [Test]
        public void Profile_MigrationFillsSkills_AndRejectsUnknownSchema()
        {
            var profile = new PlayerProfile { playerId = "x", skills = null, history = null, rating = 99999 };
            Assert.That(profile.Migrate(), Is.True);
            Assert.That(profile.skills.Count, Is.EqualTo(SkillDimensions.All.Length));
            Assert.That(profile.rating, Is.EqualTo(ReasoningRating.Maximum));
            Assert.That(new PlayerProfile { playerId = "x", schemaVersion = 99 }.Migrate(), Is.False);
            Assert.That(new PlayerProfile { playerId = null }.Migrate(), Is.False);
        }

        [Test]
        public void Repository_RoundTrips_AndQuarantinesCorruptFiles()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ronriku-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var repo = new JsonFileProfileRepository(dir);
                PlayerProfile created = repo.Load();
                DailyCompletion.Apply(created, Completed(40));
                repo.Save(created);

                PlayerProfile loaded = new JsonFileProfileRepository(dir).Load();
                Assert.That(loaded.playerId, Is.EqualTo(created.playerId));
                Assert.That(loaded.rating, Is.EqualTo(created.rating));
                Assert.That(loaded.streak, Is.EqualTo(1));
                Assert.That(loaded.history.Count, Is.EqualTo(1));

                File.WriteAllText(repo.FilePath, "{ not json");
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("RONRIKU profile"));
                PlayerProfile fresh = new JsonFileProfileRepository(dir).Load();
                Assert.That(fresh.playerId, Is.Not.EqualTo(created.playerId));
                Assert.That(Directory.GetFiles(dir, "profile.corrupt-*.json"), Is.Not.Empty);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        private static TrialOutcome Outcome(bool solved, int elapsed = 20000, int moves = 3, int par = 3) =>
            new TrialOutcome(TrialKind.Spatial, PuzzleDifficulty.Standard, solved, elapsed, 45000, moves, par, 0,
                solved ? 1200 : 0);

        private static DailySession Completed(int day)
        {
            var session = new DailySession(DailyPlan.For(day));
            while (!session.IsComplete) session.Record(Outcome(true));
            return session;
        }
    }
}
