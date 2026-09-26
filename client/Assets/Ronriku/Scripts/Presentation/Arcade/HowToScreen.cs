using System;
using Ronriku.Domain.Arcade;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Arcade
{
    /// <summary>
    /// One-time "how to play" card before a player's first run of each arcade mode: a big title, three
    /// short lines with icons, and one button. Shown once per mode (PlayerPrefs), skippable.
    /// </summary>
    public sealed class HowToScreen : VisualElement
    {
        private const string Key = "ronriku.howto.";

        public static bool Seen(LevelMode mode) => PlayerPrefs.GetInt(Key + mode, 0) == 1;
        public static void MarkSeen(LevelMode mode) => PlayerPrefs.SetInt(Key + mode, 1);

        public static (string icon, string text)[] Lines(LevelMode mode) => mode switch
        {
            LevelMode.Battle => new[]
            {
                ("bolt", "A PUZZLE CARD APPEARS. SOLVE IT TO HIT THE MONSTER"),
                ("star", "FAST + IN A ROW = CHIPS X MULT. BIG NUMBERS"),
                ("heart", "WRONG ANSWERS AND THE YELLOW TIMER COST HEARTS")
            },
            LevelMode.Cards => new[]
            {
                ("hand", "TAP UP TO 5 RUNES. PAIRS, LADDERS, SAME SIGN SCORE MORE"),
                ("star", "YOU SEE THE SCORE BEFORE YOU PLAY. HINT PICKS THE BEST"),
                ("heart", "BEAT THE MONSTER'S HP IN 4 HANDS. DISCARD BAD RUNES")
            },
            LevelMode.Dash => new[]
            {
                ("play", "SWIPE: YOUR HERO SLIDES UNTIL A WALL STOPS THEM"),
                ("gem", "GRAB EVERY GEM. THEN THE DOOR OPENS"),
                ("star", "RED SPIKES RESET YOU. FEWEST MOVES = 3 STARS")
            },
            LevelMode.Crawl => new[]
            {
                ("note", "MONSTERS MOVE ON THE BEAT. YOU GET ONE STEP PER BEAT"),
                ("bolt", "STEP INTO A MONSTER TO HIT IT. ARROWS SHOW THEIR NEXT STEP"),
                ("star", "CLEAR THE ROOM, THEN TAKE THE STAIRS")
            },
            _ => new[]
            {
                ("boss", "PHASE 1: A BATTLE WITH A TOUGH MONSTER"),
                ("hand", "PHASE 2: A RUNE HAND WITH TWO CHARMS"),
                ("star", "WIN BOTH TO CLEAR THE WORLD")
            }
        };

        public HowToScreen(LevelMode mode, Palette palette, Action back, Action play)
        {
            name = "how-to";
            style.flexGrow = 1;
            style.paddingLeft = style.paddingRight = RonrikuTheme.Gutter;
            style.paddingTop = 6;
            style.paddingBottom = 24;
            Add(ArcadeChrome.TopBar("HOW TO PLAY", back, out _));
            Add(UiFactory.Spacer());

            var title = new PixelLabel(LevelModes.Name(mode), palette.Accent, 6);
            title.style.height = 64;
            title.style.flexShrink = 0;
            Add(title);
            var sub = UiFactory.Heading(LevelModes.Hint(mode), 12, RonrikuTheme.Muted);
            sub.style.marginBottom = 26;
            Add(sub);

            int i = 0;
            foreach (var (icon, text) in Lines(mode))
            {
                var row = UiFactory.Panel(RonrikuTheme.WithAlpha(palette.Accent, 0.5f));
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 12;
                var glyph = new PixelIcon(icon, palette.Accent, 30);
                glyph.style.marginRight = 14;
                row.Add(glyph);
                var line = UiFactory.Paragraph(text, 14, RonrikuTheme.Text);
                line.style.flexShrink = 1;
                line.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.Add(line);
                Add(row);
                Juice.SlideIn(row, 420f + i * 90f, 0.3f + i * 0.08f);
                i++;
            }

            Add(UiFactory.Spacer());
            var go = UiFactory.GlowButton("LET'S GO", () =>
            {
                MarkSeen(mode);
                Feedback.Success();
                play();
            }, palette);
            go.name = "howto-go";
            go.style.height = 60;
            go.style.flexShrink = 0;
            Add(go);
        }
    }
}
