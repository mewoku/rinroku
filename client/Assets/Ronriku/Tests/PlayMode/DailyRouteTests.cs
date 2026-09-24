using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Ronriku.Composition;
using Ronriku.Domain.Puzzles;
using Ronriku.Domain.Player;
using Ronriku.Presentation.Screens;
using Ronriku.Presentation.Shell;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Ronriku.Tests
{
    public sealed class DailyRouteTests
    {
        private string _profileDir;

        [SetUp]
        public void SetUp()
        {
            _profileDir = Path.Combine(Path.GetTempPath(), "ronriku-playmode-" + Guid.NewGuid().ToString("N"));
            RuntimeConfig.ProfileDirectory = _profileDir;
            RuntimeConfig.OnlineEnabled = false;
            RuntimeConfig.UtcNowOverride = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
        }

        [TearDown]
        public void TearDown()
        {
            RuntimeConfig.Reset();
            if (Directory.Exists(_profileDir)) Directory.Delete(_profileDir, true);
        }

        [UnityTest]
        public IEnumerator Home_ShowsFreshProfile_AndBeginOpensTrialOne()
        {
            RonrikuBootstrap app = null;
            yield return Load(a => app = a);
            var root = Root(app);

            Assert.That(root.Q<Label>("profile-rating").text, Is.EqualTo("1200"));
            Assert.That(root.Q("play-map"), Is.Not.Null, "app opens on the adventure map");
            ShowTab(app, AppTab.Daily);
            yield return null;
            Assert.That(root.Q<Label>("daily-meta").text, Does.StartWith("DAILY 023"));
            Click(root.Q<Button>("begin-button"));
            yield return null;

            Assert.That(root.Q<PatternPuzzleScreen>(), Is.Not.Null, "trial 1 is Pattern");
            Assert.That(root.Q<Button>("option-0"), Is.Not.Null);
            Assert.That(root.Q<Button>("continue-button").resolvedStyle.visibility, Is.EqualTo(Visibility.Hidden));
        }

        [UnityTest]
        public IEnumerator FullDaily_SolvedThroughButtons_UpdatesProfile_AndReplayIsPractice()
        {
            RonrikuBootstrap app = null;
            yield return Load(a => app = a);
            var root = Root(app);
            ShowTab(app, AppTab.Daily);
            yield return null;

            Click(root.Q<Button>("begin-button"));
            yield return null;
            for (int trial = 0; trial < 3; trial++)
            {
                yield return SolveCurrent(app, root);
                Click(root.Q<Button>("continue-button"));
                yield return null;
            }

            Assert.That(root.Q<Label>("result-rating"), Is.Not.Null, "results screen shown");
            Assert.That(root.Q<Button>("home-button"), Is.Not.Null);
            yield return new WaitForSecondsRealtime(0.8f);
            string ratingText = root.Q<Label>("result-rating").text;
            Assert.That(ratingText, Does.StartWith("1200  >  "));

            Click(root.Q<Button>("home-button"));
            yield return null;
            Assert.That(root.Q<Label>("profile-rating").text, Is.Not.EqualTo("1200"), "rating persisted to Home");
            Assert.That(root.Q<Label>("daily-meta").text, Does.Contain("STREAK 1"));
            Assert.That(root.Q<Button>("begin-button").text, Is.EqualTo("PLAY AGAIN"));

            yield return Load(a => app = a);
            root = Root(app);
            ShowTab(app, AppTab.Daily);
            yield return null;
            Assert.That(root.Q<Label>("daily-meta").text, Does.Contain("STREAK 1"), "profile survives reload");
        }

        [UnityTest]
        public IEnumerator AdventureLevel_Solved_AwardsStarsAndShards_AndUnlocksNext()
        {
            RonrikuBootstrap app = null;
            yield return Load(a => app = a);
            var root = Root(app);
            var profile = Field<PlayerProfile>(app, "_profile");
            int shardsBefore = profile.shards;

            typeof(RonrikuBootstrap).GetMethod("PlayLevel", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(app, new object[] { 0, 0 });
            yield return null;
            Assert.That(root.Q("guardian"), Is.Not.Null, "level shows its guardian monster");
            yield return SolveCurrent(app, root);
            Click(root.Q<Button>("continue-button"));
            yield return null;

            Assert.That(root.Q("level-result"), Is.Not.Null);
            var record = profile.LevelRecordFor(0, 0);
            Assert.That(record, Is.Not.Null);
            Assert.That(record.stars, Is.InRange(1, 3));
            Assert.That(profile.shards, Is.GreaterThan(shardsBefore));
            Assert.That(Ronriku.Domain.Adventure.AdventureProgress.IsUnlocked(profile, 0, 1), Is.True);

            Click(root.Q<Button>("result-continue"));
            yield return null;
            Assert.That(root.Q("play-map"), Is.Not.Null);
        }

        internal static void ShowTab(RonrikuBootstrap app, AppTab tab) =>
            Field<AppShell>(app, "_shell").ShowTab(tab);

        private static IEnumerator Load(Action<RonrikuBootstrap> ready)
        {
            SceneManager.LoadScene("Bootstrap");
            yield return null;
            yield return null;
            var app = UnityEngine.Object.FindAnyObjectByType<RonrikuBootstrap>();
            Assert.That(app, Is.Not.Null);
            ready(app);
        }

        private static VisualElement Root(RonrikuBootstrap app) => app.GetComponent<UIDocument>().rootVisualElement;

        internal static IEnumerator SolveCurrent(RonrikuBootstrap app, VisualElement root)
        {
            if (Field<PatternPuzzleData>(app, "_currentPattern") is { } pattern)
            {
                Click(root.Q<Button>($"option-{pattern.CorrectOption}"));
                yield return null;
            }
            else if (Field<LogicPuzzleData>(app, "_currentLogic") is { } logic)
            {
                var screen = root.Q<LogicPuzzleScreen>();
                var path = LogicPuzzleSolver.Solve(logic);
                for (int i = 1; i < path.Count; i++) Assert.That(screen.TryExtend(path[i]), Is.True, $"step {i}");
                yield return null;
            }
            else
            {
                var puzzle = Field<SpatialPuzzleData>(app, "_currentPuzzle");
                Assert.That(puzzle, Is.Not.Null);
                foreach (SpatialMove move in SpatialPuzzleSolver.Solve(puzzle))
                {
                    Click(root.Q<Button>(ButtonName(move)));
                    yield return new WaitForSecondsRealtime(0.25f);
                }
            }
            yield return null;
            Assert.That(root.Q<Button>("continue-button").resolvedStyle.visibility, Is.EqualTo(Visibility.Visible));
            Assert.That(root.Q<Button>("continue-button").text, Is.EqualTo("CONTINUE"));
        }

        internal static T Field<T>(RonrikuBootstrap app, string name) where T : class =>
            typeof(RonrikuBootstrap).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(app) as T;

        internal static string ButtonName(SpatialMove move) => move switch
        {
            SpatialMove.TurnLeft => "turn-left-button",
            SpatialMove.TurnRight => "turn-right-button",
            SpatialMove.TipBack => "tip-back-button",
            _ => "tip-forward-button"
        };

        internal static void Click(Button button)
        {
            Assert.That(button, Is.Not.Null);
            using var submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }
    }
}
