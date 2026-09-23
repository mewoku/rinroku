using System;
using System.Collections.Generic;
using Ronriku.Domain.Puzzles;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Screens
{
    public sealed class HomeScreen : VisualElement
    {
        public HomeScreen(SpatialPuzzleData preview, IHapticsService haptics, Action begin)
        {
            style.flexGrow = 1;
            style.backgroundColor = RonrikuTheme.Graphite;
            style.paddingLeft = style.paddingRight = 48;
            style.paddingTop = 34;

            Add(ProfileHeader());

            var hero = new VisualElement();
            hero.style.flexGrow = 1;
            hero.style.alignItems = Align.Center;
            hero.style.justifyContent = Justify.Center;
            Add(hero);

            var title = new PixelLabel("DAILY", RonrikuTheme.Teal, 14);
            title.style.height = 116;
            title.style.width = Length.Percent(100);
            hero.Add(title);

            var board = new IsometricBoardElement(preview.Cubes, preview.StartOrientation);
            board.style.width = Length.Percent(100);
            board.style.maxWidth = 620;
            board.style.height = 650;
            board.style.marginTop = -40;
            board.style.marginBottom = -45;
            hero.Add(board);

            var meta = UiFactory.Label("DAILY 024   //   RESETS 07:18   //   3,842 ACTIVE", 16, RonrikuTheme.Muted, FontStyle.Bold);
            meta.style.height = 44;
            hero.Add(meta);

            var button = UiFactory.Button("BEGIN", () => { haptics.Selection(); begin(); }, true);
            button.name = "begin-button";
            button.style.width = Length.Percent(100);
            button.style.maxWidth = 620;
            button.style.marginTop = 20;
            hero.Add(button);

            Add(BottomNavigation());
        }

        private static VisualElement ProfileHeader()
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
            avatar.Add(new PixelLabel("N", RonrikuTheme.Yellow, 6));
            row.Add(avatar);

            var identity = new VisualElement();
            identity.style.flexGrow = 1;
            identity.style.marginLeft = 22;
            var name = UiFactory.Label("NAVAL", 26, RonrikuTheme.OffWhite, FontStyle.Bold);
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            var level = UiFactory.Label("LEVEL 131", 15, RonrikuTheme.Muted, FontStyle.Bold);
            level.style.unityTextAlign = TextAnchor.MiddleLeft;
            identity.Add(name); identity.Add(level); row.Add(identity);

            var rating = new VisualElement();
            var ratingLabel = UiFactory.Label("REASONING", 12, RonrikuTheme.Muted, FontStyle.Bold);
            ratingLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            var ratingValue = UiFactory.Label("1821", 28, RonrikuTheme.OffWhite, FontStyle.Bold);
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

