using System;
using System.Collections.Generic;
using Ronriku.Domain.Adventure;
using Ronriku.Presentation.Components;
using Ronriku.Presentation.Voxels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Screens
{
    /// <summary>Weekly boss roster: monster, stages, entry price, reward, countdown, FIGHT.</summary>
    public sealed class BossesScreen : VisualElement
    {
        public BossesScreen(IReadOnlyList<BossEvent> bosses, int shards, Func<DateTime> utcNow, Action<BossEvent> fight)
        {
            name = "bosses";
            style.flexGrow = 1;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.mode = ScrollViewMode.Vertical;
            scroll.contentContainer.style.paddingLeft = scroll.contentContainer.style.paddingRight = RonrikuTheme.Gutter;
            scroll.contentContainer.style.paddingBottom = 24;
            Add(scroll);

            var header = UiFactory.Row();
            header.style.height = 44;
            var title = UiFactory.Heading("BOSS RAIDS", 20, RonrikuTheme.Boss.Accent);
            title.style.flexGrow = 1;
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            header.Add(title);
            var timer = UiFactory.Label(string.Empty, 11, RonrikuTheme.Muted);
            header.Add(timer);
            void Tick()
            {
                TimeSpan left = bosses.Count > 0 ? bosses[0].EndsUtc - utcNow() : TimeSpan.Zero;
                timer.text = $"ENDS {(int)left.TotalDays}D {left.Hours:00}H";
            }
            Tick();
            timer.schedule.Execute(Tick).Every(30000);
            scroll.Add(header);
            var sub = UiFactory.Paragraph("Three chained trials. Beat the timer, take the loot.", 12, RonrikuTheme.Muted);
            sub.style.unityTextAlign = TextAnchor.MiddleLeft;
            sub.style.marginBottom = 10;
            scroll.Add(sub);

            string[] tiers = { "HUNTER", "ELITE", "APEX" };
            foreach (BossEvent boss in bosses)
            {
                var card = UiFactory.Panel(RonrikuTheme.WithAlpha(RonrikuTheme.Boss.Accent, 0.6f));
                card.name = $"boss-{boss.Tier}";
                card.style.flexDirection = FlexDirection.Row;
                card.style.marginBottom = 12;
                card.style.backgroundImage = new StyleBackground(PixelTextures.VerticalGradient(
                    RonrikuTheme.WithAlpha(RonrikuTheme.Boss.Ambient, 0.95f), RonrikuTheme.WithAlpha(RonrikuTheme.Surface, 0.95f)));
                card.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));

                var view = new VoxelView(boss.Monster, 72, 35f);
                view.style.width = view.style.height = 116;
                view.style.flexShrink = 0;
                card.Add(view);

                var info = new VisualElement();
                info.style.flexGrow = 1;
                info.style.marginLeft = 10;
                var tier = UiFactory.Heading(tiers[Mathf.Clamp(boss.Tier, 0, 2)], 10, RonrikuTheme.Boss.Accent2);
                tier.style.unityTextAlign = TextAnchor.MiddleLeft;
                info.Add(tier);
                var name = UiFactory.Heading(boss.Name, 20, RonrikuTheme.Text);
                name.style.unityTextAlign = TextAnchor.MiddleLeft;
                info.Add(name);
                var stages = UiFactory.Row();
                stages.style.marginTop = 4;
                foreach (string icon in new[] { "pattern", "cube", "link" })
                {
                    var s = new PixelIcon(icon, RonrikuTheme.Text, 14);
                    s.style.marginRight = 6;
                    stages.Add(s);
                }
                var reward = UiFactory.Label($"WIN +{boss.Reward}", 11, RonrikuTheme.Gold);
                reward.style.marginLeft = 4;
                stages.Add(reward);
                info.Add(stages);

                bool affordable = shards >= boss.EntryShards;
                var go = UiFactory.GlowButton(string.Empty, () => fight(boss), RonrikuTheme.Boss);
                go.name = "boss-fight";
                go.style.height = 40;
                go.style.marginTop = 10;
                var price = UiFactory.Row();
                price.pickingMode = PickingMode.Ignore;
                price.style.flexGrow = 1;
                price.style.justifyContent = Justify.Center;
                price.Add(UiFactory.Heading("FIGHT", 13, RonrikuTheme.Background));
                price.Add(UiFactory.Spacer(10));
                price.Add(new PixelIcon("shard", RonrikuTheme.Background, 12));
                var cost = UiFactory.Heading(boss.EntryShards.ToString(), 13, RonrikuTheme.Background);
                cost.style.marginLeft = 4;
                price.Add(cost);
                go.Add(price);
                go.SetEnabled(affordable);
                info.Add(go);
                if (!affordable)
                {
                    var need = UiFactory.Label("EARN MORE SHARDS IN PLAY AND DAILY", 9, RonrikuTheme.Muted);
                    need.style.marginTop = 4;
                    info.Add(need);
                }
                card.Add(info);
                scroll.Add(card);
            }
        }
    }
}
