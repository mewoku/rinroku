using System;
using System.Collections.Generic;
using Ronriku.Domain.Puzzles;
using Ronriku.Infrastructure.Analytics;
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
        private UIDocument _document;
        private VisualElement _safeRoot;
        private IAnalyticsService _analytics;
        private IHapticsService _haptics;
        private SpatialPuzzleData _puzzle;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            _analytics = new LocalAnalyticsService();
            _haptics = new PlatformHapticsService();
            _puzzle = new SpatialPuzzleGenerator().Generate(0x524F4E52494B55L, PuzzleDifficulty.Standard, 24);
            _document = GetComponent<UIDocument>();
            ConfigurePanel();
            BuildRoot();
            ShowHome();
            _analytics.Track("app_opened", new Dictionary<string, string> { ["environment"] = "local" });
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

        private void ShowHome()
        {
            _safeRoot.Clear();
            _safeRoot.Add(new HomeScreen(_puzzle, _haptics, ShowPuzzle));
            _analytics.Track("daily_viewed", Props());
        }

        private void ShowPuzzle()
        {
            _analytics.Track("daily_started", Props());
            _analytics.Track("puzzle_started", Props());
            _safeRoot.Clear();
            _safeRoot.Add(new SpatialPuzzleScreen(_puzzle, _haptics, ShowHome));
        }

        private Dictionary<string, string> Props() => new Dictionary<string, string>
        {
            ["challenge_id"] = _puzzle.Metadata.Id,
            ["challenge_version"] = _puzzle.Metadata.Version.ToString(),
            ["puzzle_type"] = _puzzle.Metadata.Type,
            ["environment"] = "local"
        };
    }
}
