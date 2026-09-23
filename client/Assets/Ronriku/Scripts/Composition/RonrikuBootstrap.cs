using System.Collections.Generic;
using Ronriku.Domain.Daily;
using Ronriku.Domain.Player;
using Ronriku.Domain.Puzzles;
using Ronriku.Infrastructure.Analytics;
using Ronriku.Infrastructure.Persistence;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using Ronriku.Presentation.Screens;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Composition
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class RonrikuBootstrap : MonoBehaviour
    {
        private readonly SpatialPuzzleGenerator _spatialGenerator = new SpatialPuzzleGenerator();
        private readonly PatternPuzzleGenerator _patternGenerator = new PatternPuzzleGenerator();
        private readonly LogicPuzzleGenerator _logicGenerator = new LogicPuzzleGenerator();
        private UIDocument _document;
        private VisualElement _safeRoot;
        private IAnalyticsService _analytics;
        private IHapticsService _haptics;
        private IProfileRepository _profiles;
        private PlayerProfile _profile;
        private DailySession _session;
        private SpatialPuzzleData _currentPuzzle;
        private PatternPuzzleData _currentPattern;
        private LogicPuzzleData _currentLogic;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            _analytics = new LocalAnalyticsService();
            _haptics = new PlatformHapticsService();
            _profiles = new JsonFileProfileRepository(RuntimeConfig.ProfileDirectory ?? Application.persistentDataPath);
            _profile = _profiles.Load();
            _document = GetComponent<UIDocument>();
            ConfigurePanel();
            BuildRoot();
            ShowHome();
            _analytics.Track("app_opened", new Dictionary<string, string> { ["environment"] = RuntimeConfig.Environment });
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
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1080, 2400);
            panel.match = 0.5f;
            panel.clearColor = true;
            panel.colorClearValue = RonrikuTheme.Graphite;
        }

        private void BuildRoot()
        {
            var root = _document.rootVisualElement;
            root.style.flexGrow = 1;
            root.style.backgroundColor = RonrikuTheme.Graphite;
            _safeRoot = new VisualElement { name = "safe-area" };
            _safeRoot.style.flexGrow = 1;
            root.Add(_safeRoot);
            root.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea());
        }

        private void ApplySafeArea()
        {
            Rect safe = Screen.safeArea;
            float sx = _document.rootVisualElement.resolvedStyle.width / Screen.width;
            float sy = _document.rootVisualElement.resolvedStyle.height / Screen.height;
            _safeRoot.style.paddingLeft = safe.xMin * sx;
            _safeRoot.style.paddingRight = (Screen.width - safe.xMax) * sx;
            _safeRoot.style.paddingTop = (Screen.height - safe.yMax) * sy;
            _safeRoot.style.paddingBottom = safe.yMin * sy;
        }

        private int Today => DailyCalendar.DayNumber(RuntimeConfig.UtcNow);

        private void ShowHome()
        {
            _session = null;
            _currentPuzzle = null;
            _currentPattern = null;
            _currentLogic = null;
            int today = Today;
            DailyPlan plan = DailyPlan.For(today);
            SpatialPuzzleData preview = _spatialGenerator.Generate(plan.PreviewSeed, PuzzleDifficulty.Standard, 0);
            var model = new HomeViewModel
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
                UntilReset = () => DailyCalendar.UntilReset(RuntimeConfig.UtcNow)
            };
            _safeRoot.Clear();
            _safeRoot.Add(new HomeScreen(model, _haptics, BeginDaily));
            _analytics.Track("daily_viewed", DailyProps(plan));
        }

        private void BeginDaily()
        {
            _session = new DailySession(DailyPlan.For(Today));
            _analytics.Track("daily_started", DailyProps(_session.Plan));
            ShowTrial();
        }

        private void ShowTrial()
        {
            TrialSpec spec = _session.Current;
            _currentPuzzle = null;
            _currentPattern = null;
            _currentLogic = null;
            _analytics.Track("puzzle_started", TrialProps(spec));
            string header = $"TRIAL {spec.Index + 1} / {_session.Plan.Trials.Count}   //   {spec.Kind.ToString().ToUpperInvariant()}";
            string footer = $"DAILY {_session.Plan.Day:000}   //   {RuntimeConfig.Environment.ToUpperInvariant()}";
            var context = new TrialScreenContext
            {
                Haptics = _haptics,
                Back = AbandonDaily,
                Completed = OnTrialCompleted,
                Header = header,
                Footer = footer
            };

            _safeRoot.Clear();
            switch (spec.Kind)
            {
                case TrialKind.Pattern:
                    _currentPattern = _patternGenerator.Generate(spec.Seed, spec.Difficulty, 0);
                    _safeRoot.Add(new PatternPuzzleScreen(_currentPattern, context));
                    break;
                case TrialKind.Logic:
                    _currentLogic = _logicGenerator.Generate(spec.Seed, spec.Difficulty, 0);
                    _safeRoot.Add(new LogicPuzzleScreen(_currentLogic, context));
                    break;
                default:
                    _currentPuzzle = _spatialGenerator.Generate(spec.Seed, spec.Difficulty, 0);
                    _safeRoot.Add(new SpatialPuzzleScreen(_currentPuzzle, _haptics, AbandonDaily,
                        score => OnTrialCompleted(TrialOutcome.FromSpatial(_currentPuzzle, score)),
                        header, footer, "<  HOME"));
                    break;
            }
        }

        private void OnTrialCompleted(TrialOutcome outcome)
        {
            TrialSpec spec = _session.Current;
            var props = TrialProps(spec);
            props["duration_ms"] = outcome.ElapsedMilliseconds.ToString();
            props["moves"] = outcome.Moves.ToString();
            props["par"] = outcome.Par.ToString();
            props["resets"] = outcome.Resets.ToString();
            _analytics.Track(outcome.Solved ? "puzzle_solved" : "puzzle_failed", props);

            _session.Record(outcome);
            if (_session.IsComplete) FinishDaily();
            else ShowTrial();
        }

        private void AbandonDaily()
        {
            if (_session?.Current != null) _analytics.Track("puzzle_abandoned", TrialProps(_session.Current));
            ShowHome();
        }

        private void FinishDaily()
        {
            DailyResult result = DailyCompletion.Apply(_profile, _session);
            if (result.Counted) _profiles.Save(_profile);

            var props = DailyProps(_session.Plan);
            props["duration_ms"] = result.ElapsedMilliseconds.ToString();
            props["solved"] = result.Solved.ToString();
            props["counted"] = result.Counted ? "1" : "0";
            _analytics.Track("daily_completed", props);
            _analytics.Track("results_viewed", DailyProps(_session.Plan));

            _safeRoot.Clear();
            _safeRoot.Add(new DailyResultsScreen(result, _session.Plan.Day, !RuntimeConfig.Competitive, _haptics, ShowHome));
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
            props["rules_version"] = (spec.Kind switch
            {
                TrialKind.Pattern => PatternPuzzleGenerator.CurrentRulesVersion,
                TrialKind.Logic => LogicPuzzleGenerator.CurrentRulesVersion,
                _ => SpatialPuzzleGenerator.CurrentRulesVersion
            }).ToString();
            return props;
        }
    }
}
