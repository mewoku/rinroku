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
    }

    public sealed class HomeScreen : VisualElement
    {
        private readonly HomeViewModel _model;
        private readonly Label _meta;

        public HomeScreen(HomeViewModel model, IHapticsService haptics, Action begin)
        {
            _model = model;
            style.flexGrow = 1;
            style.backgroundColor = RonrikuTheme.Graphite;
            style.paddingLeft = style.paddingRight = 48;
            style.paddingTop = 34;

            Add(ProfileHeader(model));

            var hero = new VisualElement();
            hero.style.flexGrow = 1;
            hero.style.alignItems = Align.Center;
            hero.style.justifyContent = Justify.Center;
            Add(hero);

            var title = new PixelLabel("DAILY", RonrikuTheme.Teal, 16);
            title.style.height = 132;
            title.style.width = Length.Percent(100);
            hero.Add(title);

            var board = new IsometricBoardElement(model.PreviewCubes, model.PreviewOrientation);
            board.style.width = Length.Percent(100);
            board.style.maxWidth = 820;
            board.style.height = 760;
            board.style.marginTop = -24;
            board.style.marginBottom = -40;
            hero.Add(board);

            _meta = UiFactory.Label(string.Empty, 17, RonrikuTheme.Muted, FontStyle.Bold);
            _meta.name = "daily-meta";
            _meta.style.height = 48;
            hero.Add(_meta);

            var button = UiFactory.Button(model.CompletedToday ? "PLAY AGAIN" : "BEGIN",
                () => { haptics.Selection(); begin(); }, !model.CompletedToday);
            button.name = "begin-button";
            button.style.width = Length.Percent(100);
            button.style.maxWidth = 640;
            button.style.height = 88;
            button.style.marginTop = 16;
            hero.Add(button);

            var note = UiFactory.Label(Note(model), 13, RonrikuTheme.BlueGrey, FontStyle.Bold);
            note.style.height = 44;
            hero.Add(note);

            Add(BottomNavigation());
            UpdateMeta();
            schedule.Execute(UpdateMeta).Every(1000);
        }

        private static string Note(HomeViewModel model)
        {
            if (model.CompletedToday) return "COMPLETED TODAY   //   REPLAYS ARE PRACTICE";
            return model.LocalMode ? "THREE TRIALS   //   LOCAL MODE" : "THREE TRIALS";
        }

        private void UpdateMeta()
        {
            TimeSpan left = _model.UntilReset?.Invoke() ?? TimeSpan.Zero;
            string streak = _model.Streak > 0 ? $"   //   STREAK {_model.Streak}" : string.Empty;
            _meta.text = $"DAILY {_model.DailyNumber:000}   //   RESETS {(int)left.TotalHours:00}:{left.Minutes:00}:{left.Seconds:00}{streak}";
        }

        private static VisualElement ProfileHeader(HomeViewModel model)
        {
            var row = new VisualElement();
            row.style.height = 108;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var avatar = new VisualElement();
            avatar.style.width = avatar.style.height = 76;
            avatar.style.backgroundColor = RonrikuTheme.NearBlack;
            avatar.style.borderLeftWidth = avatar.style.borderRightWidth = 4;
            avatar.style.borderTopWidth = avatar.style.borderBottomWidth = 4;
            avatar.style.borderLeftColor = avatar.style.borderRightColor = RonrikuTheme.Teal;
            avatar.style.borderTopColor = avatar.style.borderBottomColor = RonrikuTheme.Teal;
            var initial = new PixelLabel(model.DisplayName.Substring(0, 1), RonrikuTheme.Yellow, 6);
            initial.style.flexGrow = 1;
            avatar.Add(initial);
            row.Add(avatar);

            var identity = new VisualElement();
            identity.style.flexGrow = 1;
            identity.style.marginLeft = 22;
            var name = UiFactory.Label(model.DisplayName, 26, RonrikuTheme.OffWhite, FontStyle.Bold);
            name.name = "profile-name";
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            var level = UiFactory.Label($"LEVEL {model.Level}", 15, RonrikuTheme.Muted, FontStyle.Bold);
            level.style.unityTextAlign = TextAnchor.MiddleLeft;
            identity.Add(name); identity.Add(level); row.Add(identity);

            var rating = new VisualElement();
            var ratingLabel = UiFactory.Label("REASONING", 12, RonrikuTheme.Muted, FontStyle.Bold);
            ratingLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            var ratingValue = UiFactory.Label(model.Rating.ToString(), 28, RonrikuTheme.OffWhite, FontStyle.Bold);
            ratingValue.name = "profile-rating";
            ratingValue.style.unityTextAlign = TextAnchor.MiddleRight;
            rating.Add(ratingLabel); rating.Add(ratingValue); row.Add(rating);
            return row;
        }

        private static VisualElement BottomNavigation()
        {
            var nav = new VisualElement();
            nav.style.height = 118;
            nav.style.flexDirection = FlexDirection.Row;
            nav.style.alignItems = Align.Center;
            nav.style.borderTopWidth = 2;
            nav.style.borderTopColor = RonrikuTheme.BlueGrey;
            string[] labels = { "DAILY", "BOSSES", "CREATE", "RANK", "PROFILE" };
            foreach (string text in labels)
            {
                var item = UiFactory.Button(text, () => { });
                item.SetEnabled(text == "DAILY");
                item.style.flexGrow = 1;
                item.style.height = 72;
                item.style.fontSize = 12;
                item.style.backgroundColor = Color.clear;
                item.style.color = text == "DAILY" ? RonrikuTheme.Teal : RonrikuTheme.Muted;
                nav.Add(item);
            }
            return nav;
        }
    }
}
