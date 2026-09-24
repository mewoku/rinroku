using System;
using System.Collections.Generic;
using Ronriku.Domain.Adventure;
using Ronriku.Domain.Daily;
using Ronriku.Domain.Figures;
using Ronriku.Domain.Player;
using Ronriku.Domain.Puzzles;
using Ronriku.Domain.Shop;
using Ronriku.Infrastructure.Analytics;
using Ronriku.Infrastructure.Online;
using Ronriku.Infrastructure.Persistence;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using Ronriku.Presentation.Screens;
using Ronriku.Presentation.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Composition
{
    /// <summary>
    /// Composition root: builds services, the app shell and every flow (Daily, adventure levels, world
    /// bosses, boss raids, shop, profile). Screens are plain VisualElements; state lives in the profile.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class RonrikuBootstrap : MonoBehaviour
    {
        public const int ShardPickupsPerDay = 10;

        private readonly SpatialPuzzleGenerator _spatialGenerator = new SpatialPuzzleGenerator();
        private readonly PatternPuzzleGenerator _patternGenerator = new PatternPuzzleGenerator();
        private readonly LogicPuzzleGenerator _logicGenerator = new LogicPuzzleGenerator();
        private UIDocument _document;
        private AppShell _shell;
        private IAnalyticsService _analytics;
        private IHapticsService _haptics;
        private IProfileRepository _profiles;
        private PlayerProfile _profile;
        private DailySession _session;
        private SpatialPuzzleData _currentPuzzle;
        private PatternPuzzleData _currentPattern;
        private LogicPuzzleData _currentLogic;
        private OnlineService _online;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            _analytics = new LocalAnalyticsService();
            _haptics = new PlatformHapticsService { Enabled = PlayerPrefs.GetInt("ronriku.haptics", 1) == 1 };
            Feedback.Init(gameObject, _haptics);
            _profiles = new JsonFileProfileRepository(RuntimeConfig.ProfileDirectory ?? Application.persistentDataPath);
            _profile = _profiles.Load();
            _document = GetComponent<UIDocument>();
            ConfigurePanel();
            BuildRoot();
            _analytics.Track("app_opened", new Dictionary<string, string> { ["environment"] = RuntimeConfig.Environment });
            if (RuntimeConfig.OnlineEnabled)
            {
                _online = OnlineService.CreateFromConfig();
                if (_online != null) ConnectOnline();
            }
        }

        // ---------------------------------------------------------------- online

        private bool Online => _online != null && _online.Connected;

        private async void ConnectOnline()
        {
            bool connected = await _online.ConnectAsync(_profile);
            if (this == null) return;
            RuntimeConfig.Competitive = connected;
            if (!connected) return;
            Save();
            _shell.TopBar.Refresh();
            if (!_shell.InFullscreen) _shell.ShowTab(_shell.Current);
        }

        /// <summary>Runs a server call after a local result; on success re-pulls authoritative state.</summary>
        private async void Submit(string what, Func<System.Threading.Tasks.Task> call, Action onDone = null)
        {
            if (!Online) return;
            try
            {
                await call();
                await _online.Pull(_profile);
                if (this == null) return;
                Save();
                _shell.TopBar.Refresh();
                onDone?.Invoke();
            }
            catch (Exception e)
            {
                string code = e is OnlineException oe ? oe.Code : e.Message;
                Debug.LogWarning($"RONRIKU online: {what} failed: {code}");
                if (this != null) Toast.Show(_shell, $"SERVER: {code.ToUpperInvariant()}", RonrikuTheme.Red);
            }
        }

        private void ConfigurePanel()
        {
            var panel = _document.panelSettings;
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.name = "RONRIKU Runtime Panel Fallback";
                _document.panelSettings = panel;
            }
            // Design in dp: a 1080×2400 phone is 432×960 dp.
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(432, 960);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.clearColor = true;
            panel.colorClearValue = RonrikuTheme.Background;
        }

        private void BuildRoot()
        {
            var root = _document.rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;
            root.style.backgroundColor = RonrikuTheme.Background;
            root.style.unityFontDefinition = RonrikuTheme.Body;
            var topBar = new TopBar(TopBarModel, () => _shell.ShowTab(AppTab.Me));
            _shell = new AppShell(CreateTab, topBar);
            root.Add(_shell);
            root.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea());
            _shell.ShowTab(AppTab.Play);
            root.Add(new BootSplash());
        }

        private void ApplySafeArea()
        {
            Rect safe = Screen.safeArea;
            var root = _document.rootVisualElement;
            float sx = root.resolvedStyle.width / Screen.width;
            float sy = root.resolvedStyle.height / Screen.height;
            if (float.IsNaN(sx) || float.IsNaN(sy)) return;
            _shell.SetSafeArea(safe.xMin * sx, (Screen.height - safe.yMax) * sy, (Screen.width - safe.xMax) * sx, safe.yMin * sy);
        }

        private int Today => DailyCalendar.DayNumber(RuntimeConfig.UtcNow);

        private void Save() => _profiles.Save(_profile);

        private TopBarModel TopBarModel() => new TopBarModel
        {
            Name = _profile.displayName,
            Level = _profile.Level,
            Rating = _profile.rating,
            Shards = _profile.shards,
            Avatar = ShopCatalogue.Build(_profile.Avatar),
            AvatarKey = _profile.Avatar?.id,
            Online = RuntimeConfig.Competitive
        };

        // ---------------------------------------------------------------- tabs

        private VisualElement CreateTab(AppTab tab)
        {
            ClearCurrent();
            switch (tab)
            {
                case AppTab.Daily: return DailyTab();
                case AppTab.Bosses:
                    return new BossesScreen(BossEvent.ForWeek(RuntimeConfig.UtcNow), _profile.shards, () => RuntimeConfig.UtcNow, StartBossEvent);
                case AppTab.Shop:
                    return new ShopScreen(_profile, ShopCatalogue.ForDay(Today), () => DailyCalendar.UntilReset(RuntimeConfig.UtcNow), BuyFigure);
                case AppTab.Me:
                    return new MeScreen(_profile, Today, _haptics.Enabled, SetHaptics, EquipFigure, Online ? _online : null);
                default:
                    return new PlayMapScreen(_profile, ShopCatalogue.Build(_profile.Avatar), PlayLevel, CollectShard);
            }
        }

        private VisualElement DailyTab()
        {
            int today = Today;
            DailyPlan plan = DailyPlan.For(today);
            SpatialPuzzleData preview = _spatialGenerator.Generate(plan.PreviewSeed, PuzzleDifficulty.Standard, 0);
            _analytics.Track("daily_viewed", DailyProps(plan));
            return new HomeScreen(new HomeViewModel
            {
                DisplayName = _profile.displayName,
                Level = _profile.Level,
                Rating = _profile.rating,
                DailyNumber = today,
                Streak = _profile.DisplayStreak(today),
                CompletedToday = _profile.HasCompleted(today),
                LocalMode = !RuntimeConfig.Competitive,
                PreviewCubes = preview.Cubes,
                PreviewOrientation = preview.StartOrientation,
                UntilReset = () => DailyCalendar.UntilReset(RuntimeConfig.UtcNow),
                RewardShards = Economy.DailyReward(_profile.DisplayStreak(today) + 1)
            }, _haptics, BeginDaily);
        }

        private void SetHaptics(bool enabled)
        {
            _haptics.Enabled = enabled;
            PlayerPrefs.SetInt("ronriku.haptics", enabled ? 1 : 0);
        }

        private void EquipFigure(OwnedFigure figure)
        {
            _profile.avatarFigureId = figure.id;
            Save();
            _shell.TopBar.Refresh();
            Submit("avatar", () => _online.SetAvatar(figure.id));
        }

        private PurchaseResult BuyFigure(ShopItem item)
        {
            PurchaseResult result = ShopCatalogue.Buy(_profile, item);
            if (result == PurchaseResult.Ok)
            {
                Submit("purchase", () => _online.BuyFigure(item.Id));
                Save();
                _shell.TopBar.Refresh();
                _analytics.Track("figure_bought", new Dictionary<string, string> { ["rarity"] = item.Figure.Rarity.ToString(), ["size"] = item.Size.ToString() });
            }
            return result;
        }

        /// <summary>Floating map shards: +1 each, capped per UTC day so the idle toy can't farm.</summary>
        private bool CollectShard()
        {
            string key = "ronriku.pickups." + Today;
            int taken = PlayerPrefs.GetInt(key, 0);
            if (taken >= ShardPickupsPerDay) return false;
            PlayerPrefs.SetInt(key, taken + 1);
            _profile.shards += 1;
            Save();
            _shell.TopBar.Refresh();
            _shell.TopBar.Pulse();
            return true;
        }

        // ---------------------------------------------------------------- trials

        private void ClearCurrent()
        {
            _currentPuzzle = null;
            _currentPattern = null;
            _currentLogic = null;
        }

        private static Palette KindPalette(TrialKind kind) => kind switch
        {
            TrialKind.Pattern => RonrikuTheme.Pattern,
            TrialKind.Logic => RonrikuTheme.Link,
            _ => RonrikuTheme.Lab
        };

        private VisualElement CreateTrial(TrialKind kind, long seed, PuzzleDifficulty difficulty, TrialScreenContext context)
        {
            ClearCurrent();
            switch (kind)
            {
                case TrialKind.Pattern:
                    _currentPattern = _patternGenerator.Generate(seed, difficulty, 0);
                    return new PatternPuzzleScreen(_currentPattern, context);
                case TrialKind.Logic:
                    _currentLogic = _logicGenerator.Generate(seed, difficulty, 0);
                    return new LogicPuzzleScreen(_currentLogic, context);
                default:
                    _currentPuzzle = _spatialGenerator.Generate(seed, difficulty, 0);
                    return new SpatialPuzzleScreen(_currentPuzzle, context);
            }
        }

        private void ShowTrialScreen(VisualElement screen, Palette palette) => _shell.ShowFullscreen(screen, palette);

        // ---------------------------------------------------------------- daily

        private void BeginDaily()
        {
            _session = new DailySession(DailyPlan.For(Today));
            _analytics.Track("daily_started", DailyProps(_session.Plan));
            ShowDailyTrial();
        }

        private void ShowDailyTrial()
        {
            TrialSpec spec = _session.Current;
            _analytics.Track("puzzle_started", TrialProps(spec));
            var context = new TrialScreenContext
            {
                Haptics = _haptics,
                Back = () => { _analytics.Track("puzzle_abandoned", TrialProps(spec)); _shell.ShowTab(AppTab.Daily); },
                Completed = OnDailyTrialCompleted,
                Header = $"DAILY {_session.Plan.Day:000}  ·  TRIAL {spec.Index + 1}/{_session.Plan.Trials.Count}",
                Footer = $"DAILY {_session.Plan.Day:000}  ·  {RuntimeConfig.Environment.ToUpperInvariant()}",
                Palette = KindPalette(spec.Kind)
            };
            ShowTrialScreen(CreateTrial(spec.Kind, spec.Seed, spec.Difficulty, context), context.Palette);
        }

        private void OnDailyTrialCompleted(TrialOutcome outcome)
        {
            TrialSpec spec = _session.Current;
            TrackOutcome(TrialProps(spec), outcome);
            _session.Record(outcome);
            if (_session.IsComplete) FinishDaily();
            else ShowDailyTrial();
        }

        private void FinishDaily()
        {
            DailyResult result = DailyCompletion.Apply(_profile, _session);
            if (result.Counted)
            {
                Save();
                var outcomes = new List<TrialOutcome>(_session.Outcomes);
                int day = _session.Plan.Day;
                Submit("daily", () => _online.SubmitDaily(day, outcomes));
            }
            var props = DailyProps(_session.Plan);
            props["duration_ms"] = result.ElapsedMilliseconds.ToString();
            props["solved"] = result.Solved.ToString();
            props["counted"] = result.Counted ? "1" : "0";
            _analytics.Track("daily_completed", props);
            _analytics.Track("results_viewed", DailyProps(_session.Plan));
            ClearCurrent();
            _shell.ShowFullscreen(new DailyResultsScreen(result, _session.Plan.Day, !RuntimeConfig.Competitive, _haptics,
                () => _shell.ShowTab(AppTab.Daily)), RonrikuTheme.Frost);
        }

        // ---------------------------------------------------------------- adventure

        private void PlayLevel(int world, int index)
        {
            LevelDef def = LevelDef.For(world, index);
            if (def.IsBoss)
            {
                PlayBossStages($"W{world + 1} BOSS", def.Monster(), def.BossStages(), 0, 0, new List<TrialOutcome>(),
                    (elapsed, answers) => FinishWorldBoss(world, index, elapsed, answers), () => FailBoss(def.Monster(), () => PlayLevel(world, index), AppTab.Play));
                return;
            }

            Palette palette = RonrikuTheme.Worlds[world % RonrikuTheme.Worlds.Length];
            var context = new TrialScreenContext
            {
                Haptics = _haptics,
                Back = () => _shell.ShowTab(AppTab.Play),
                Header = $"W{world + 1}  ·  LEVEL {index + 1}",
                Footer = $"{LevelDef.WorldNames[world]}  ·  {def.Kind.ToString().ToUpperInvariant()}",
                Palette = palette,
                Monster = def.Monster()
            };
            context.Completed = outcome =>
            {
                TrackOutcome(new Dictionary<string, string> { ["world"] = world.ToString(), ["level"] = index.ToString() }, outcome);
                int stars = AdventureProgress.Stars(outcome);
                int earned = stars > 0 ? AdventureProgress.Complete(_profile, world, index, stars, outcome.ElapsedMilliseconds) : 0;
                if (stars > 0)
                {
                    Save();
                    Submit("level", () => _online.CompleteLevel(world, index, stars, outcome.ElapsedMilliseconds, new[] { outcome }));
                }
                ClearCurrent();
                _shell.ShowFullscreen(new LevelResultScreen(new LevelResultModel
                {
                    Won = stars > 0,
                    Title = def.Monster().Name,
                    Stars = stars,
                    ShardsEarned = earned,
                    ElapsedMs = outcome.ElapsedMilliseconds,
                    Monster = def.Monster(),
                    Palette = palette
                }, () => _shell.ShowTab(AppTab.Play), () => PlayLevel(world, index)), palette);
            };
            ShowTrialScreen(CreateTrial(def.Kind, def.Seed, def.Difficulty, context), palette);
        }

        /// <summary>Runs boss stages one after another; any skipped/failed stage loses the fight.</summary>
        private void PlayBossStages(string title, Figure monster, IReadOnlyList<(TrialKind kind, long seed)> stages, int stage,
            int elapsedSoFar, List<TrialOutcome> answers, Action<int, List<TrialOutcome>> won, Action lost)
        {
            var (kind, seed) = stages[stage];
            var context = new TrialScreenContext
            {
                Haptics = _haptics,
                Back = lost,
                Header = $"{title}  ·  STAGE {stage + 1}/{stages.Count}",
                Footer = monster.Name,
                Palette = RonrikuTheme.Boss,
                Monster = monster,
                HpSegments = stages.Count,
                HpRemaining = stages.Count - stage
            };
            context.Completed = outcome =>
            {
                TrackOutcome(new Dictionary<string, string> { ["boss"] = title, ["stage"] = stage.ToString() }, outcome);
                int elapsed = elapsedSoFar + outcome.ElapsedMilliseconds;
                answers.Add(outcome);
                if (!outcome.Solved) lost();
                else if (stage + 1 < stages.Count) PlayBossStages(title, monster, stages, stage + 1, elapsed, answers, won, lost);
                else won(elapsed, answers);
            };
            ShowTrialScreen(CreateTrial(kind, seed, PuzzleDifficulty.Standard, context), RonrikuTheme.Boss);
        }

        private void FinishWorldBoss(int world, int index, int elapsed, List<TrialOutcome> answers)
        {
            LevelDef def = LevelDef.For(world, index);
            int stars = AdventureProgress.BossStars(answers);
            int earned = AdventureProgress.Complete(_profile, world, index, stars, elapsed);
            Save();
            Submit("world boss", () => _online.CompleteLevel(world, index, stars, elapsed, answers));
            ClearCurrent();
            _shell.ShowFullscreen(new LevelResultScreen(new LevelResultModel
            {
                Won = true,
                Boss = true,
                Title = def.Monster().Name,
                Stars = stars,
                ShardsEarned = earned,
                ElapsedMs = elapsed,
                Monster = def.Monster(),
                Palette = RonrikuTheme.Boss,
                NextLabel = world + 1 < LevelDef.WorldCount ? "NEXT WORLD" : "CONTINUE"
            }, () =>
            {
                PlayMapScreen.LastWorld = Mathf.Min(world + 1, LevelDef.WorldCount - 1);
                _shell.ShowTab(AppTab.Play);
            }, null), RonrikuTheme.Boss);
        }

        private void FailBoss(Figure monster, Action retry, AppTab back)
        {
            ClearCurrent();
            _shell.ShowFullscreen(new LevelResultScreen(new LevelResultModel
            {
                Won = false,
                Boss = true,
                Title = monster.Name,
                Monster = monster,
                Palette = RonrikuTheme.Boss,
                NextLabel = "BACK"
            }, () => _shell.ShowTab(back), retry), RonrikuTheme.Boss);
        }

        // ---------------------------------------------------------------- boss raids

        private async void StartBossEvent(BossEvent boss)
        {
            if (!boss.TryEnter(_profile))
            {
                Feedback.Error();
                return;
            }
            Save();
            string attempt = null;
            if (Online)
            {
                try { attempt = await _online.EnterBoss(boss.Id); }
                catch (Exception e)
                {
                    Debug.LogWarning($"RONRIKU online: boss entry failed: {e.Message}");
                    Toast.Show(_shell, "SERVER BOSS ENTRY FAILED  ·  PLAYING LOCAL", RonrikuTheme.Red);
                }
                if (this == null) return;
            }
            _analytics.Track("boss_started", new Dictionary<string, string> { ["boss"] = boss.Id });
            PlayBossStages(boss.Name, boss.Monster, boss.Stages(), 0, 0, new List<TrialOutcome>(), (elapsed, answers) =>
            {
                int earned = boss.RecordWin(_profile);
                Save();
                if (attempt != null) Submit("boss", () => _online.FinishBoss(attempt, answers, elapsed));
                _analytics.Track("boss_completed", new Dictionary<string, string> { ["boss"] = boss.Id, ["duration_ms"] = elapsed.ToString() });
                ClearCurrent();
                _shell.ShowFullscreen(new LevelResultScreen(new LevelResultModel
                {
                    Won = true,
                    Boss = true,
                    Title = boss.Name,
                    Stars = 3,
                    ShardsEarned = earned,
                    ElapsedMs = elapsed,
                    Monster = boss.Monster,
                    Palette = RonrikuTheme.Boss
                }, () => _shell.ShowTab(AppTab.Bosses), null), RonrikuTheme.Boss);
            }, () => FailBoss(boss.Monster, null, AppTab.Bosses));
        }

        // ---------------------------------------------------------------- analytics

        private void TrackOutcome(Dictionary<string, string> props, TrialOutcome outcome)
        {
            props["puzzle_type"] = outcome.Kind.ToString().ToLowerInvariant();
            props["duration_ms"] = outcome.ElapsedMilliseconds.ToString();
            props["moves"] = outcome.Moves.ToString();
            props["par"] = outcome.Par.ToString();
            _analytics.Track(outcome.Solved ? "puzzle_solved" : "puzzle_failed", props);
        }

        private static Dictionary<string, string> DailyProps(DailyPlan plan) => new Dictionary<string, string>
        {
            ["challenge_id"] = plan.ChallengeId,
            ["challenge_version"] = DailyCalendar.RulesVersion.ToString(),
            ["environment"] = RuntimeConfig.Environment
        };

        private Dictionary<string, string> TrialProps(TrialSpec spec)
        {
            var props = DailyProps(_session.Plan);
            props["trial_index"] = spec.Index.ToString();
            props["puzzle_type"] = spec.Kind.ToString().ToLowerInvariant();
            props["difficulty"] = spec.Difficulty.ToString().ToLowerInvariant();
            return props;
        }
    }

    /// <summary>Pixel boot sequence replacing the engine splash: logo drops in, then the overlay fades.</summary>
    public sealed class BootSplash : VisualElement
    {
        public BootSplash()
        {
            name = "boot";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0;
            style.backgroundColor = RonrikuTheme.Background;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            var logo = new PixelLabel("RONRIKU", RonrikuTheme.Teal, 7);
            logo.style.height = 60;
            logo.style.width = Length.Percent(100);
            Add(logo);
            var tag = UiFactory.Heading("THREE TESTS. ONE MIND.", 11, RonrikuTheme.Muted);
            Add(tag);
            float start = Time.realtimeSinceStartup;
            schedule.Execute(() =>
            {
                float t = Time.realtimeSinceStartup - start;
                logo.style.translate = new Translate(0, Mathf.Max(0f, 1f - t / 0.35f) * -30f);
                style.opacity = Mathf.Clamp01(1f - (t - 0.9f) / 0.35f);
                if (t > 1.3f) RemoveFromHierarchy();
            }).Every(16);
        }
    }
}
