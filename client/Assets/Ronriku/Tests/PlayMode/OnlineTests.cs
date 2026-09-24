using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Ronriku.Domain.Adventure;
using Ronriku.Domain.Daily;
using Ronriku.Domain.Player;
using Ronriku.Domain.Puzzles;
using Ronriku.Infrastructure.Online;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronriku.Tests
{
    /// <summary>
    /// End-to-end against the local Supabase (backend/README.md). Skipped when the backend is not running.
    /// Run: unity command run_tests --mode PlayMode --filter Online --filter_type category
    /// </summary>
    [Category("Online")]
    public sealed class OnlineTests
    {
        private static TrialOutcome PatternOutcome(int option, bool solved) =>
            new TrialOutcome(TrialKind.Pattern, PuzzleDifficulty.Standard, solved, 15000, 30000, 1, 0, 0, solved ? 1000 : 0,
                option.ToString());

        [UnityTest]
        public IEnumerator AnonymousPlayer_SolvesLevelOne_ServerAcceptsAnswer_AndPaysShards()
        {
            PlayerPrefs.DeleteKey("ronriku.session");
            OnlineService online = OnlineService.CreateFromConfig();
            Assert.That(online, Is.Not.Null, "ronriku-online.json missing");
            var profile = PlayerProfile.CreateNew("online-test");

            Task<bool> connect = online.ConnectAsync(profile);
            yield return new WaitUntil(() => connect.IsCompleted);
            if (!connect.Result) Assert.Ignore($"local backend unreachable: {online.LastError}");
            int before = profile.shards;
            Assert.That(profile.figures.Count, Is.GreaterThan(0), "server starter figure pulled");

            LevelDef def = LevelDef.For(0, 0);
            var data = new PatternPuzzleGenerator().Generate(def.Seed, def.Difficulty, 0);

            Task<LevelSubmitResult> wrong = online.CompleteLevel(0, 0, 3, 15000,
                new[] { PatternOutcome((data.CorrectOption + 1) % 4, true) });
            yield return new WaitUntil(() => wrong.IsCompleted);
            Assert.That(wrong.IsFaulted, Is.True, "wrong answer must be rejected");
            Assert.That(((OnlineException)wrong.Exception.InnerException).Code, Is.EqualTo("not_solved"));

            Task<LevelSubmitResult> right = online.CompleteLevel(0, 0, 3, 15000,
                new[] { PatternOutcome(data.CorrectOption, true) });
            yield return new WaitUntil(() => right.IsCompleted);
            Assert.That(right.IsFaulted, Is.False, right.Exception?.InnerException?.Message);
            Assert.That(right.Result.Earned, Is.GreaterThan(0));

            Task pull = online.Pull(profile);
            yield return new WaitUntil(() => pull.IsCompleted);
            Assert.That(profile.shards, Is.EqualTo(before + right.Result.Earned));
            Assert.That(profile.LevelRecordFor(0, 0)?.stars ?? 0, Is.GreaterThan(0));

            Task<List<LeaderboardRow>> board = online.Leaderboard("global", 50);
            yield return new WaitUntil(() => board.IsCompleted);
            Assert.That(board.IsFaulted, Is.False);
            Assert.That(board.Result.Count, Is.GreaterThan(0), "global board has rows");
            PlayerPrefs.DeleteKey("ronriku.session");
        }
    }
}
