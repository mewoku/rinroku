using System;
using Ronriku.Domain.Adventure;
using Ronriku.Domain.Figures;
using Ronriku.Domain.Player;
using Ronriku.Domain.Shop;
using Ronriku.Infrastructure.Online;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using Ronriku.Presentation.Voxels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Screens
{
    public sealed class MeScreen : VisualElement
    {
        private readonly PlayerProfile _profile;
        private readonly Action<OwnedFigure> _equip;
        private readonly VoxelView _hero;

        public MeScreen(PlayerProfile profile, int today, bool hapticsEnabled, Action<bool> setHaptics,
            Action<OwnedFigure> equip, OnlineService online)
        {
            _profile = profile;
            _equip = equip;
            name = "me";
            style.flexGrow = 1;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.mode = ScrollViewMode.Vertical;
            scroll.contentContainer.style.paddingLeft = scroll.contentContainer.style.paddingRight = RonrikuTheme.Gutter;
            scroll.contentContainer.style.paddingBottom = 24;
            Add(scroll);

            var hero = UiFactory.Row();
            hero.style.height = 168;
            _hero = new VoxelView(ShopCatalogue.Build(profile.Avatar), 96, 26f) { name = "me-avatar" };
            _hero.style.width = _hero.style.height = 160;
            _hero.style.flexShrink = 0;
            hero.Add(_hero);
            var identity = new VisualElement();
            identity.style.flexGrow = 1;
            identity.style.marginLeft = 8;
            var nameLabel = UiFactory.Heading(profile.displayName, 18, RonrikuTheme.Text);
            nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            identity.Add(nameLabel);
            var sub = UiFactory.Label($"LEVEL {profile.Level}  ·  {(online != null ? "ONLINE" : "LOCAL PROFILE")}", 11, RonrikuTheme.Muted);
            sub.style.unityTextAlign = TextAnchor.MiddleLeft;
            identity.Add(sub);
            identity.Add(XpBar(profile));
            var tip = UiFactory.Label("TAP YOUR FIGURE", 10, RonrikuTheme.Forest.Accent);
            tip.style.unityTextAlign = TextAnchor.MiddleLeft;
            tip.style.marginTop = 8;
            identity.Add(tip);
            hero.Add(identity);
            scroll.Add(hero);

            var stats = UiFactory.Row();
            stats.style.justifyContent = Justify.SpaceBetween;
            stats.style.marginTop = 4;
            int stars = 0;
            for (int w = 0; w < LevelDef.WorldCount; w++) stars += AdventureProgress.StarsIn(profile, w);
            stats.Add(Stat("RATING", profile.rating.ToString(), RonrikuTheme.Yellow));
            stats.Add(Stat("STREAK", profile.DisplayStreak(today).ToString(), RonrikuTheme.Link.Accent));
            stats.Add(Stat("STARS", stars.ToString(), RonrikuTheme.Frost.Accent));
            stats.Add(Stat("DAILIES", profile.completedDailies.ToString(), RonrikuTheme.Pattern.Accent));
            scroll.Add(stats);

            scroll.Add(Section($"COLLECTION  ·  {profile.figures.Count}"));
            var grid = new VisualElement { name = "collection" };
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            foreach (OwnedFigure owned in profile.figures) grid.Add(CollectionCard(owned));
            scroll.Add(grid);

            scroll.Add(Section("FRIENDS & RANKS"));
            var social = UiFactory.Panel(RonrikuTheme.Frost.Accent2);
            if (online != null) social.Add(new SocialPanel(online));
            else social.Add(UiFactory.Paragraph(
                "Add friends by handle, compare ratings and climb the global and daily leaderboards once you connect online.",
                12, RonrikuTheme.Muted));
            scroll.Add(social);

            scroll.Add(Section("SETTINGS"));
            var settings = UiFactory.Panel();
            settings.Add(ToggleRow("MUSIC", Ronriku.Presentation.Audio.Music.Enabled, v => Ronriku.Presentation.Audio.Music.Enabled = v));
            settings.Add(ToggleRow("SOUND FX", Feedback.SoundEnabled, v => Feedback.SoundEnabled = v));
            settings.Add(ToggleRow("HAPTICS", hapticsEnabled, setHaptics));
            settings.Add(ToggleRow(Tilt.Available ? "GYRO PARALLAX" : "GYRO  ·  NO SENSOR", MotionSettings.GyroEnabled && Tilt.Available,
                v => MotionSettings.GyroEnabled = v, Tilt.Available));
            settings.Add(ToggleRow("REDUCED MOTION", MotionSettings.ReducedMotion, v => MotionSettings.ReducedMotion = v));
            scroll.Add(settings);
        }

        private VisualElement CollectionCard(OwnedFigure owned)
        {
            Figure figure = ShopCatalogue.Build(owned);
            bool equipped = _profile.Avatar == owned;
            Color rarity = ShopScreen.RarityColour(figure.Rarity);
            var card = UiFactory.Panel(equipped ? RonrikuTheme.Forest.Accent : RonrikuTheme.WithAlpha(rarity, 0.5f));
            card.name = $"owned-{owned.id}";
            card.style.width = Length.Percent(31.3f);
            card.style.marginRight = Length.Percent(2);
            card.style.marginBottom = 8;
            card.style.paddingLeft = card.style.paddingRight = 4;
            card.style.paddingTop = card.style.paddingBottom = 6;
            card.style.alignItems = Align.Center;
            var view = new VoxelView(figure, 48, 18f, false);
            view.style.width = view.style.height = 76;
            card.Add(view);
            card.Add(UiFactory.Heading(figure.Name, 9, RonrikuTheme.Text));
            card.Add(UiFactory.Label(equipped ? "EQUIPPED" : $"{figure.Rarity.ToString().ToUpperInvariant()} {owned.size}×{owned.size}", 9,
                equipped ? RonrikuTheme.Forest.Accent : rarity));
            card.RegisterCallback<ClickEvent>(_ =>
            {
                if (equipped) return;
                _equip(owned);
                _hero.SetFigure(figure);
                Feedback.Success();
            });
            UiFactory.Pressable(card);
            return card;
        }

        private static VisualElement XpBar(PlayerProfile profile)
        {
            float progress = profile.xp % PlayerProfile.XpPerLevel / (float)PlayerProfile.XpPerLevel;
            var bar = new VisualElement();
            bar.style.height = 10;
            bar.style.marginTop = 8;
            bar.style.backgroundColor = RonrikuTheme.Surface2;
            UiFactory.SetBorder(bar, 2, RonrikuTheme.Line);
            var fill = new VisualElement();
            fill.style.width = Length.Percent(Mathf.Max(3f, progress * 100f));
            fill.style.flexGrow = 0;
            fill.style.height = Length.Percent(100);
            fill.style.backgroundImage = new StyleBackground(
                PixelTextures.DiagonalGradient(RonrikuTheme.Forest.Accent, RonrikuTheme.Frost.Accent));
            fill.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            bar.Add(fill);
            return bar;
        }

        private static VisualElement Stat(string label, string value, Color colour)
        {
            var tile = UiFactory.Panel(RonrikuTheme.WithAlpha(colour, 0.45f));
            tile.style.width = Length.Percent(23.5f);
            tile.style.paddingLeft = tile.style.paddingRight = 4;
            tile.style.alignItems = Align.Center;
            tile.Add(UiFactory.Heading(value, 16, colour));
            tile.Add(UiFactory.Label(label, 9, RonrikuTheme.Muted));
            return tile;
        }

        private static Label Section(string text)
        {
            var label = UiFactory.Heading(text, 12, RonrikuTheme.Muted);
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.marginTop = 18;
            label.style.marginBottom = 8;
            return label;
        }

        private static VisualElement ToggleRow(string label, bool value, Action<bool> changed, bool enabled = true)
        {
            var row = UiFactory.Row();
            row.style.height = 44;
            var text = UiFactory.Heading(label, 11, enabled ? RonrikuTheme.Text : RonrikuTheme.Muted);
            text.style.flexGrow = 1;
            text.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(text);
            var track = new VisualElement { name = "toggle-" + label.ToLowerInvariant().Replace(' ', '-') };
            track.style.width = 48;
            track.style.height = 24;
            UiFactory.SetBorder(track, 2, RonrikuTheme.Line);
            var knob = new VisualElement();
            knob.style.width = knob.style.height = 16;
            knob.style.marginTop = 2;
            track.Add(knob);
            bool state = value;
            void Render()
            {
                track.style.backgroundColor = state ? RonrikuTheme.WithAlpha(RonrikuTheme.Forest.Accent, 0.35f) : RonrikuTheme.Surface2;
                knob.style.backgroundColor = state ? RonrikuTheme.Forest.Accent : RonrikuTheme.Muted;
                knob.style.marginLeft = state ? 26 : 2;
            }
            Render();
            if (enabled)
            {
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    state = !state;
                    changed(state);
                    Render();
                    Feedback.Tap();
                });
            }
            row.Add(track);
            return row;
        }
    }
}
