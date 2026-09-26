using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Ronriku.Composition;
using Ronriku.Domain.Arcade;
using Ronriku.Presentation.Arcade;
using Ronriku.Presentation.Shell;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Ronriku.Tests
{
    /// <summary>
    /// Records real gameplay into JPG frame sequences for the video ads (marketing/video/public/clips/raw).
    /// Not part of CI: run with the "Footage" category. Each clip writes fps.txt for ffmpeg.
    /// </summary>
    [Category("Footage")]
    public sealed class FootageTests
    {
        private const int Width = 720;
        private const int Height = 1600;
        private RenderTexture _target;
        private Texture2D _read;
        private string _root;

        private static void PlayLevel(RonrikuBootstrap app, int world, int index) =>
            typeof(RonrikuBootstrap).GetMethod("PlayLevel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, new object[] { world, index });

        private IEnumerator Record(string clip, float seconds, Func<IEnumerator> drive)
        {
            string dir = Path.Combine(_root, clip);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            int frames = 0;
            float start = Time.realtimeSinceStartup;
            bool done = false;
            IEnumerator Run()
            {
                yield return drive();
                done = true;
            }
            var runner = UnityEngine.Object.FindAnyObjectByType<RonrikuBootstrap>();
            runner.StartCoroutine(Run());
            while (Time.realtimeSinceStartup - start < seconds && !(done && Time.realtimeSinceStartup - start > 1.5f))
            {
                yield return null;
                var previous = RenderTexture.active;
                RenderTexture.active = _target;
                _read.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                _read.Apply(false);
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(dir, $"{frames:00000}.jpg"), _read.EncodeToJPG(88));
                frames++;
            }
            float elapsed = Time.realtimeSinceStartup - start;
            File.WriteAllText(Path.Combine(dir, "fps.txt"), (frames / elapsed).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        }

        [UnityTest]
        public IEnumerator RecordGameplayClips()
        {
            _root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../marketing/video/public/clips/raw"));
            Directory.CreateDirectory(_root);
            string profileDir = Path.Combine(Path.GetTempPath(), "ronriku-footage-" + Guid.NewGuid().ToString("N"));
            RuntimeConfig.ProfileDirectory = profileDir;
            RuntimeConfig.OnlineEnabled = false;
            RuntimeConfig.SkipHowTo = true;
            RuntimeConfig.UtcNowOverride = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
            try
            {
                SceneManager.LoadScene("Bootstrap");
                yield return null;
                var app = UnityEngine.Object.FindAnyObjectByType<RonrikuBootstrap>();
                var document = app.GetComponent<UIDocument>();
                _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                _read = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                document.panelSettings = UnityEngine.Object.Instantiate(document.panelSettings);
                document.panelSettings.targetTexture = _target;
                var root = document.rootVisualElement;
                yield return new WaitForSecondsRealtime(1.5f);

                // 1. Map
                yield return Record("map", 3.5f, () => Idle(3f));

                // 2. Battle: a streak of fast right answers.
                PlayLevel(app, 1, 4);
                yield return Record("battle", 16f, () => Battle(root));

                // 3. Rune Hand: charm pick, hinted hands scoring.
                PlayLevel(app, 0, 3);
                yield return Record("cards", 16f, () => Cards(root));

                // 4. Ice Dash solved at par.
                PlayLevel(app, 0, 2);
                yield return Record("dash", 10f, () => Dash(root));

                // 5. Beat Crawl hunting monsters on the beat.
                PlayLevel(app, 0, 5);
                yield return Record("crawl", 14f, () => Crawl(root));

                // 6. Daily pattern trial acting out its rule.
                DailyRouteTests.ShowTab(app, AppTab.Daily);
                yield return null;
                DailyRouteTests.Click(root.Q<Button>("begin-button"));
                yield return Record("daily", 6f, () => Idle(5.5f));

                document.panelSettings.targetTexture = null;
                _target.Release();
            }
            finally
            {
                RuntimeConfig.Reset();
                if (Directory.Exists(profileDir)) Directory.Delete(profileDir, true);
            }
        }

        private static IEnumerator Idle(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }

        private static IEnumerator Battle(VisualElement root)
        {
            var battle = root.Q<BattleScreen>();
            yield return new WaitForSecondsRealtime(1.0f);
            while (battle != null && !battle.State.Over)
            {
                var c = battle.State.Current;
                int index = battle.State.Index;
                yield return new WaitForSecondsRealtime(0.9f);
                var view = root.Q<ChallengeView>();
                if (view == null) continue;
                if (c.Kind == ChallengeKind.Sum)
                {
                    for (int i = 0; i < c.Items.Length; i++)
                        if ((c.Answer & (1 << i)) != 0) { DailyRouteTests.Click(view.Q<Button>($"tile-{i}")); yield return new WaitForSecondsRealtime(0.2f); }
                }
                else if (c.Kind == ChallengeKind.Memory)
                {
                    float until = Time.realtimeSinceStartup + 4f;
                    while (!(view.Q<Button>("cell-0")?.enabledSelf ?? true) && Time.realtimeSinceStartup < until) yield return null;
                    for (int i = 0; i < 16; i++)
                        if ((c.Answer & (1 << i)) != 0) { DailyRouteTests.Click(view.Q<Button>($"cell-{i}")); yield return new WaitForSecondsRealtime(0.15f); }
                }
                else if (c.Kind == ChallengeKind.Classic) yield return BigCardSolver.Solve(view);
                else DailyRouteTests.Click(view.Q<Button>($"option-{c.Answer}"));
                float wait = Time.realtimeSinceStartup + 4f;
                while (battle.State.Index == index && !battle.State.Over && Time.realtimeSinceStartup < wait) yield return null;
            }
            yield return new WaitForSecondsRealtime(1.5f);
        }

        private static IEnumerator Cards(VisualElement root)
        {
            yield return new WaitForSecondsRealtime(1.2f);
            DailyRouteTests.Click(root.Q<Button>("charm-1"));
            yield return new WaitForSecondsRealtime(1.4f);
            var cards = root.Q<CardsScreen>();
            for (int hand = 0; hand < 3 && cards != null && !cards.State.Over; hand++)
            {
                DailyRouteTests.Click(root.Q<Button>("hint"));
                yield return new WaitForSecondsRealtime(0.9f);
                DailyRouteTests.Click(root.Q<Button>("play"));
                float until = Time.realtimeSinceStartup + 6f;
                yield return new WaitForSecondsRealtime(0.3f);
                while (cards.Busy && Time.realtimeSinceStartup < until) yield return null;
                yield return new WaitForSecondsRealtime(0.4f);
            }
            yield return new WaitForSecondsRealtime(1.5f);
        }

        private static IEnumerator Dash(VisualElement root)
        {
            var dash = root.Q<DashScreen>();
            yield return new WaitForSecondsRealtime(1.0f);
            foreach (int dir in SolveDash(dash.State.Level))
            {
                dash.Move(dir);
                yield return new WaitForSecondsRealtime(0.75f);
            }
            yield return new WaitForSecondsRealtime(1.5f);
        }

        private static IEnumerator Crawl(VisualElement root)
        {
            var crawl = root.Q<CrawlScreen>();
            yield return new WaitForSecondsRealtime(2.6f);
            float until = Time.realtimeSinceStartup + 11f;
            int lastBeat = -1;
            while (Time.realtimeSinceStartup < until && !crawl.State.Won && !crawl.State.Lost)
            {
                if (crawl.State.Beat != lastBeat)
                {
                    lastBeat = crawl.State.Beat;
                    yield return new WaitForSecondsRealtime(0.05f);
                    int target = crawl.State.StairsOpen ? crawl.State.Level.Stairs : crawl.State.Enemies.OrderBy(e => CrawlLevel.Distance(e.Pos, crawl.State.Hero)).First().Pos;
                    crawl.Act(Toward(crawl.State, target));
                }
                yield return null;
            }
        }

        private static int Toward(CrawlState s, int target)
        {
            var prev = new Dictionary<int, int> { [s.Hero] = -1 };
            var q = new Queue<int>();
            q.Enqueue(s.Hero);
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                if (c == target) break;
                for (int d = 0; d < 4; d++)
                {
                    int n = CrawlLevel.Step(c, d);
                    if (n < 0 || s.Level.Walls[n] || prev.ContainsKey(n)) continue;
                    if (n != target && s.Enemies.Any(e => e.Pos == n)) continue;
                    prev[n] = c;
                    q.Enqueue(n);
                }
            }
            if (!prev.ContainsKey(target)) return 0;
            int step = target;
            while (prev[step] != s.Hero && prev[step] >= 0) step = prev[step];
            for (int d = 0; d < 4; d++) if (CrawlLevel.Step(s.Hero, d) == step) return d;
            return 0;
        }

        private static List<int> SolveDash(DashLevel level)
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
