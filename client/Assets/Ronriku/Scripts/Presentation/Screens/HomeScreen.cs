using System;
using System.Collections.Generic;
using Ronriku.Domain.Puzzles;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Screens
{
    public sealed class HomeViewModel
    {
        public string DisplayName;
        public int Level;
        public int Rating;
        public int DailyNumber;
        public int Streak;
        public bool CompletedToday;
        public bool LocalMode;
        public IReadOnlyList<GridPoint> PreviewCubes;
        public int PreviewOrientation;
        public Func<TimeSpan> UntilReset;
        public int RewardShards;
    }

    /// <summary>DAILY tab: today's three trials, reset countdown, streak and BEGIN.</summary>
    public sealed class HomeScreen : VisualElement
    {
        private readonly HomeViewModel _model;
        private readonly Label _meta;

        public HomeScreen(HomeViewModel model, IHapticsService haptics, Action begin)
        {
            _model = model;
            name = "daily";
            style.flexGrow = 1;
            style.paddingLeft = style.paddingRight = RonrikuTheme.Gutter;
            style.justifyContent = Justify.Center;

            var title = new PixelLabel("DAILY", RonrikuTheme.Frost.Accent, 8);
            title.style.height = 64;
            Add(title);
            var tagline = UiFactory.Heading("THREE TESTS. ONE MIND.", 11, RonrikuTheme.Muted);
            tagline.style.marginBottom = 8;
            Add(tagline);

            var board = new IsometricBoardElement(model.PreviewCubes, model.PreviewOrientation);
            board.style.height = 230;
            board.style.marginTop = -10;
            board.style.marginBottom = -20;
            Add(board);

            var trials = UiFactory.Row();
            trials.style.justifyContent = Justify.SpaceBetween;
            trials.style.marginTop = 8;
            trials.Add(TrialCard("PATTERN", "pattern", RonrikuTheme.Pattern, 1));
            trials.Add(TrialCard("SHADOW", "cube", RonrikuTheme.Lab, 2));
            trials.Add(TrialCard("LINK", "link", RonrikuTheme.Link, 3));
            Add(trials);

            _meta = UiFactory.Label(string.Empty, 12, RonrikuTheme.Muted);
            _meta.name = "daily-meta";
            _meta.style.height = 32;
            _meta.style.marginTop = 12;
            Add(_meta);

            var button = model.CompletedToday
                ? UiFactory.FlatButton("PLAY AGAIN", () => { haptics.Selection(); begin(); })
                : UiFactory.GlowButton("BEGIN", () => { haptics.Selection(); begin(); }, RonrikuTheme.Frost);
            button.name = "begin-button";
            button.style.height = 56;
            button.style.marginTop = 4;
            Add(button);

            var note = UiFactory.Label(model.CompletedToday
                ? "DONE TODAY  ·  REPLAYS ARE PRACTICE"
                : $"+{model.RewardShards} SHARDS{(model.LocalMode ? "" : "  ·  RANKED")}", 10, RonrikuTheme.Muted);
            note.style.marginTop = 10;
            Add(note);

            UpdateMeta();
            schedule.Execute(UpdateMeta).Every(1000);
        }

        private static VisualElement TrialCard(string title, string icon, Palette palette, int number)
        {
            var card = UiFactory.Panel(RonrikuTheme.WithAlpha(palette.Accent, 0.55f));
            card.style.width = Length.Percent(31.5f);
            card.style.alignItems = Align.Center;
            card.style.paddingTop = card.style.paddingBottom = 10;
            card.style.backgroundImage = new StyleBackground(
                PixelTextures.VerticalGradient(RonrikuTheme.WithAlpha(palette.Ambient, 0.9f), RonrikuTheme.WithAlpha(RonrikuTheme.Surface, 0.9f)));
            card.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            card.Add(UiFactory.Heading(number.ToString(), 10, RonrikuTheme.Muted));
            var pixel = new PixelIcon(icon, palette.Accent, 28);
            pixel.style.marginTop = 6;
            pixel.style.marginBottom = 6;
            card.Add(pixel);
            card.Add(UiFactory.Heading(title, 10, RonrikuTheme.Text));
            return card;
        }

        private void UpdateMeta()
        {
            TimeSpan left = _model.UntilReset?.Invoke() ?? TimeSpan.Zero;
            string streak = _model.Streak > 0 ? $"   //   STREAK {_model.Streak}" : string.Empty;
            _meta.text = $"DAILY {_model.DailyNumber:000}   //   RESETS {(int)left.TotalHours:00}:{left.Minutes:00}:{left.Seconds:00}{streak}";
        }
    }
}
