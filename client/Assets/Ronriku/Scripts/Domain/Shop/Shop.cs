using System;
using System.Collections.Generic;
using Ronriku.Domain.Daily;
using Ronriku.Domain.Figures;
using Ronriku.Domain.Player;

namespace Ronriku.Domain.Shop
{
    public sealed class ShopItem
    {
        public string Id { get; }
        public ulong Seed { get; }
        public int Size { get; }
        public Figure Figure { get; }
        /// <summary>Shard price, or -1 when the item is SOL-only.</summary>
        public int PriceShards { get; }

        public ShopItem(ulong seed, int size)
        {
            Seed = seed;
            Size = size;
            Id = $"fig-{seed}-{size}";
            Figure = FigureGenerator.Generate(seed, size);
            PriceShards = Economy.FigurePrice(Figure.Rarity);
        }

        public bool SolOnly => PriceShards < 0;
    }

    public enum PurchaseResult { Ok, AlreadyOwned, InsufficientShards, SolOnly }

    /// <summary>Six figures rotate every UTC day; everyone sees the same shelf.</summary>
    public static class ShopCatalogue
    {
        public const int ShelfSize = 6;
        private static readonly int[] Sizes = { 3, 3, 4, 4, 5, 5 };

        public static IReadOnlyList<ShopItem> ForDay(int day)
        {
            long daySeed = DailyPlan.Mix(DailyCalendar.Seed(day), 0x53484F50);
            var items = new List<ShopItem>(ShelfSize);
            for (int i = 0; i < ShelfSize; i++)
                items.Add(new ShopItem(unchecked((ulong)DailyPlan.Mix(daySeed, i)), Sizes[i]));
            return items;
        }

        public static PurchaseResult Buy(PlayerProfile profile, ShopItem item)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            foreach (OwnedFigure f in profile.figures) if (f.id == item.Id) return PurchaseResult.AlreadyOwned;
            if (item.SolOnly) return PurchaseResult.SolOnly;
            if (profile.shards < item.PriceShards) return PurchaseResult.InsufficientShards;
            profile.shards -= item.PriceShards;
            profile.figures.Add(new OwnedFigure { id = item.Id, seed = item.Seed.ToString(), size = item.Size, acquired = "shop" });
            return PurchaseResult.Ok;
        }

        public static Figure Build(OwnedFigure owned) =>
            FigureGenerator.Generate(ulong.TryParse(owned.seed, out ulong s) ? s : 1UL, owned.size);
    }
}
