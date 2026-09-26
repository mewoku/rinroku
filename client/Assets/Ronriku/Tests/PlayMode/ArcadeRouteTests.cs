using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Ronriku.Composition;
using Ronriku.Domain.Adventure;
using Ronriku.Domain.Arcade;
using Ronriku.Domain.Player;
using Ronriku.Presentation.Arcade;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Ronriku.Tests
{
    /// <summary>Drives each arcade mode through its real UI (buttons, swipes) end to end.</summary>
    public sealed class ArcadeRouteTests
    {
        private string _profileDir;

        [SetUp]
        public void SetUp()
        {
            _profileDir = Path.Combine(Path.GetTempPath(), "ronriku-arcade-" + Guid.NewGuid().ToString("N"));
            RuntimeConfig.ProfileDirectory = _profileDir;
            RuntimeConfig.OnlineEnabled = false;
            RuntimeConfig.SkipHowTo = true;
            RuntimeConfig.UtcNowOverride = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
        }

        [TearDown]
        public void TearDown()
        {
            RuntimeConfig.Reset();
            try { Directory.Delete(_profileDir, true); } catch { /* best effort */ }
        }

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

        private static void PlayLevel(RonrikuBootstrap app, int world, int index) =>
            typeof(RonrikuBootstrap).GetMethod("PlayLevel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, new object[] { world, index });

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail("timed out waiting for " + what);
                yield return null;
            }
        }

        private static void Click(Button b)
        {
            Assert.That(b, Is.Not.Null);
            using var e = NavigationSubmitEvent.GetPooled();
            e.target = b;
            b.SendEvent(e);
        }

        /// <summary>Answers the visible challenge correctly by tapping like a player.</summary>
        private static IEnumerator Answer(VisualElement root, Challenge c)
        {
            var view = root.Q<ChallengeView>();
            switch (c.Kind)
            {
                case ChallengeKind.Sum:
                    for (int i = 0; i < c.Items.Length; i++)
                        if ((c.Answer & (1 << i)) != 0) Click(view.Q<Button>($"tile-{i}"));
                    break;
                case ChallengeKind.Memory:
                    yield return WaitFor(() => view.Q<Button>("cell-0")?.enabledSelf ?? true, 5f, "memory unlock");
                    for (int i = 0; i < 16; i++)
                        if ((c.Answer & (1 << i)) != 0) Click(view.Q<Button>($"cell-{i}"));
                    break;
                default:
                    Click(view.Q<Button>($"option-{c.Answer}"));
                    break;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Battle_AnsweringRight_KillsTheMonster_AndRecordsStars()
        {
            RonrikuBootstrap app = null;
            yield return Load(a => app = a);
            var root = Root(app);
            var profile = DailyRouteTests.Field<PlayerProfile>(app, "_profile");
            int shardsBefore = profile.shards;

            PlayLevel(app, 0, 0);
            var battle = root.Q<BattleScreen>();
            Assert.That(battle, Is.Not.Null, "level 1 is a battle");
            Assert.That(root.Q("monster-hp"), Is.Not.Null);
            var seen = new HashSet<ChallengeKind>();
            while (!battle.State.Over)
            {
                yield return WaitFor(() => root.Q<ChallengeView>() is { } v && v.Challenge == battle.State.Current || battle.State.Over, 5f, "next challenge");
                if (battle.State.Over) break;
                seen.Add(battle.State.Current.Kind);
                int index = battle.State.Index;
                yield return Answer(root, battle.State.Current);
                yield return WaitFor(() => battle.State.Index > index || battle.State.Over, 5f, "answer judged");
            }
            Assert.That(battle.State.Won, Is.True);
            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(2), "a fight mixes challenge kinds");
            yield return WaitFor(() => root.Q("level-result") != null, 5f, "result screen");

            var record = profile.LevelRecordFor(0, 0);
            Assert.That(record, Is.Not.Null);
            Assert.That(record.stars, Is.InRange(1, 3));
            Assert.That(profile.shards, Is.GreaterThan(shardsBefore));
            Assert.That(AdventureProgress.IsUnlocked(profile, 0, 1), Is.True);
            Click(root.Q<Button>("result-continue"));
            yield return null;
            Assert.That(root.Q("play-map"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator FirstRun_ShowsHowTo_ThenTheLevel_OnlyOnce()
        {
            RuntimeConfig.SkipHowTo = false;
            PlayerPrefs.DeleteKey("ronriku.howto.Dash");
            RonrikuBootstrap app = null;
            yield return Load(a => app = a);
            var root = Root(app);
            PlayLevel(app, 0, 2);
            Assert.That(root.Q("how-to"), Is.Not.Null, "first dash shows how to play");
            Click(root.Q<Button>("howto-go"));
            yield return null;
            Assert.That(root.Q<DashScreen>(), Is.Not.Null);
            PlayLevel(app, 0, 2);
            Assert.That(root.Q("how-to"), Is.Null, "only once");
            Assert.That(root.Q<DashScreen>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Cards_PickCharm_PlayHintedHands_ReachesAResult()
        {
            RonrikuBootstrap app = null;
            yield return Load(a => app = a);
            var root = Root(app);

            PlayLevel(app, 0, 3);
            Assert.That(root.Q("charm-pick"), Is.Not.Null, "card fights start with a charm pick");
            Click(root.Q<Button>("charm-0"));
            yield return WaitFor(() => root.Q<CardsScreen>() != null, 3f, "cards screen");
            var cards = root.Q<CardsScreen>();
            Assert.That(root.Query<CardElement>().ToList().Count, Is.EqualTo(cards.State.HandSize));
            while (!cards.State.Over)
            {
                int hands = cards.State.HandsLeft;
                Click(root.Q<Button>("hint"));
                yield return null;
                Assert.That(root.Q<Button>("play").enabledSelf, Is.True, "hint selects a playable hand");
                Click(root.Q<Button>("play"));
                yield return WaitFor(() => cards.State.HandsLeft < hands, 2f, "hand played");
                yield return WaitFor(() => !cards.Busy, 8f, "hand scored");
            }
            yield return WaitFor(() => root.Q("level-result") != null, 8f, "result screen");
        }

        [UnityTest]
        public IEnumerator Dash_SwipingTheSolution_ClearsAtPar()
        {
            RonrikuBootstrap app = null;
            yield return Load(a => app = a);
            var root = Root(app);
            var profile = DailyRouteTests.Field<PlayerProfile>(app, "_profile");

            PlayLevel(app, 0, 2);
            var dash = root.Q<DashScreen>();
            Assert.That(dash, Is.Not.Null, "level 3 is ice dash");
            List<int> path = Solve(dash.State.Level);
            Assert.That(path.Count, Is.EqualTo(dash.State.Level.Par));
            foreach (int dir in path)
            {
                int moves = dash.State.Moves;
                dash.Move(dir);
                yield return WaitFor(() => dash.State.Moves > moves, 1f, "move");
                yield return new WaitForSecondsRealtime(0.6f);
            }
            Assert.That(dash.State.Won && dash.State.Stars == 3);
            yield return WaitFor(() => root.Q("level-result") != null, 5f, "result screen");
            Assert.That(profile.LevelRecordFor(0, 2)?.stars, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator Crawl_StartsOnTheBeat_AndMonstersAct()
        {
            RonrikuBootstrap app = null;
            yield return Load(a => app = a);
            var root = Root(app);

            PlayLevel(app, 0, 5);
            var crawl = root.Q<CrawlScreen>();
            Assert.That(crawl, Is.Not.Null, "level 6 is beat crawl");
            // READY banner, then two grace beats where monsters hold still, then they act on the beat.
            yield return WaitFor(() => crawl.State.Beat > 0, 10f, "monsters act on the beat");
            int hero = crawl.State.Hero;
            for (int d = 0; d < 4 && crawl.State.Hero == hero; d++)
            {
                crawl.Act(d);
                yield return new WaitForSecondsRealtime(0.9f);
            }
            Assert.That(crawl.State.Hero != hero || crawl.State.Coins > 0, "hero can move or hit");
            Click(root.Q<Button>("back-button"));
            yield return null;
            Assert.That(root.Q("play-map"), Is.Not.Null);
        }

        private static List<int> Solve(DashLevel level)
        {
            var prev = new Dictionary<int, (int key, int dir)>();
            var queue = new Queue<(int pos, int gems)>();
            prev[level.Start * 64] = (-1, -1);
            queue.Enqueue((level.Start, 0));
            while (queue.Count > 0)
            {
                var (pos, gems) = queue.Dequeue();
                int key = pos * 64 + gems;
                for (int dir = 0; dir < 4; dir++)
                {
                    var slide = DashState.Slide(level, pos, gems, dir);
                    if (slide.Path.Count == 0 || slide.Dead) continue;
                    if (slide.Won)
                    {
                        var path = new List<int> { dir };
                        for (int k = key; prev[k].key >= 0; k = prev[k].key) path.Insert(0, prev[k].dir);
                        return path;
                    }
                    int next = slide.End * 64 + slide.Gems;
                    if (prev.ContainsKey(next)) continue;
                    prev[next] = (key, dir);
                    queue.Enqueue((slide.End, slide.Gems));
                }
            }
            return new List<int>();
        }
    }
}
