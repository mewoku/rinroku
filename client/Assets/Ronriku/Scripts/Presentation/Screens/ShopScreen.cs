using System;
using System.Collections.Generic;
using Ronriku.Domain.Figures;
using Ronriku.Domain.Player;
using Ronriku.Domain.Shop;
using Ronriku.Presentation.Accessibility;
using Ronriku.Presentation.Components;
using Ronriku.Presentation.Voxels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ronriku.Presentation.Screens
{
    /// <summary>Daily figure shelf (shards; Legendary is devnet SOL via the online store) and marketplace entry.</summary>
    public sealed class ShopScreen : VisualElement
    {
        private readonly PlayerProfile _profile;
        private readonly Func<ShopItem, PurchaseResult> _buy;

        public ShopScreen(PlayerProfile profile, IReadOnlyList<ShopItem> shelf, Func<TimeSpan> untilRefresh,
            Func<ShopItem, PurchaseResult> buy)
        {
            _profile = profile;
            _buy = buy;
            name = "shop";
            style.flexGrow = 1;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.mode = ScrollViewMode.Vertical;
            scroll.contentContainer.style.paddingLeft = scroll.contentContainer.style.paddingRight = RonrikuTheme.Gutter;
            scroll.contentContainer.style.paddingBottom = 24;
            Add(scroll);

            var title = UiFactory.Row();
            title.style.height = 44;
            var heading = UiFactory.Heading("FIGURE SHOP", 20, RonrikuTheme.Pattern.Accent);
            heading.style.flexGrow = 1;
            heading.style.unityTextAlign = TextAnchor.MiddleLeft;
            title.Add(heading);
            var timer = UiFactory.Label(string.Empty, 11, RonrikuTheme.Muted);
            timer.name = "shop-timer";
            title.Add(timer);
            void Tick()
            {
                TimeSpan left = untilRefresh();
                timer.text = $"NEW IN {(int)left.TotalHours:00}:{left.Minutes:00}";
            }
            Tick();
            timer.schedule.Execute(Tick).Every(1000);
            scroll.Add(title);

            var grid = new VisualElement { name = "shop-grid" };
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.SpaceBetween;
            foreach (ShopItem item in shelf) grid.Add(Card(item));
            scroll.Add(grid);

            var market = UiFactory.Panel(RonrikuTheme.Pattern.Accent2);
            market.style.marginTop = 16;
            var marketTitle = UiFactory.Heading("PLAYER MARKET", 14, RonrikuTheme.Text);
            marketTitle.style.unityTextAlign = TextAnchor.MiddleLeft;
            market.Add(marketTitle);
            var marketText = UiFactory.Paragraph(
                "Trade figures with other players for shards or devnet SOL. Opens when you connect online.", 12, RonrikuTheme.Muted);
            marketText.style.marginTop = 6;
            market.Add(marketText);
            scroll.Add(market);
        }

        private VisualElement Card(ShopItem item)
        {
            Color rarity = RarityColour(item.Figure.Rarity);
            var card = UiFactory.Panel(RonrikuTheme.WithAlpha(rarity, 0.6f));
            card.name = $"shop-{item.Id}";
            card.style.width = Length.Percent(48.5f);
            card.style.marginBottom = 10;
            card.style.paddingTop = card.style.paddingBottom = 8;
            card.style.alignItems = Align.Center;

            var badge = UiFactory.Heading($"{item.Figure.Rarity.ToString().ToUpperInvariant()}  {item.Size}×{item.Size}", 9, rarity);
            card.Add(badge);
            var view = new VoxelView(item.Figure, 72, 30f);
            view.style.width = view.style.height = 120;
            card.Add(view);
            card.Add(UiFactory.Heading(item.Figure.Name, 13, RonrikuTheme.Text));

            bool owned = _profile.figures.Exists(f => f.id == item.Id);
            Button buy;
            if (owned) buy = UiFactory.FlatButton("OWNED", null);
            else if (item.SolOnly) buy = UiFactory.GlowButton("SOL · ONLINE", () => Toast.Show(this, "LEGENDARY FIGURES ARE DEVNET NFTS. CONNECT ONLINE TO BUY.", rarity), RonrikuTheme.Boss);
            else
            {
                buy = UiFactory.GlowButton(string.Empty, () => Purchase(item, card), RonrikuTheme.Pattern);
                var price = UiFactory.Row();
                price.style.justifyContent = Justify.Center;
                price.style.flexGrow = 1;
                price.pickingMode = PickingMode.Ignore;
                price.Add(new PixelIcon("shard", RonrikuTheme.Background, 12));
                var amount = UiFactory.Heading(item.PriceShards.ToString(), 13, RonrikuTheme.Background);
                amount.style.marginLeft = 6;
                price.Add(amount);
                buy.Add(price);
            }
            buy.name = "buy-button";
            buy.style.height = 36;
            buy.style.alignSelf = Align.Stretch;
            buy.style.marginTop = 8;
            buy.SetEnabled(!owned);
            card.Add(buy);
            return card;
        }

        private void Purchase(ShopItem item, VisualElement card)
        {
            switch (_buy(item))
            {
                case PurchaseResult.Ok:
                    Feedback.Win();
                    Toast.Show(this, $"{item.Figure.Name} JOINED YOUR COLLECTION", RarityColour(item.Figure.Rarity));
                    var owned = card.Q<Button>("buy-button");
                    owned.Clear();
                    owned.text = "OWNED";
                    owned.SetEnabled(false);
                    break;
                case PurchaseResult.InsufficientShards:
                    Feedback.Error();
                    Toast.Show(this, "NOT ENOUGH SHARDS. CLEAR LEVELS AND DAILIES TO EARN MORE.", RonrikuTheme.Red);
                    break;
                default:
                    Feedback.Error();
                    break;
            }
        }

        public static Color RarityColour(FigureRarity rarity) => rarity switch
        {
            FigureRarity.Rare => RonrikuTheme.Rare,
            FigureRarity.Epic => RonrikuTheme.Epic,
            FigureRarity.Legendary => RonrikuTheme.Legendary,
            _ => RonrikuTheme.Common
        };
    }
}
