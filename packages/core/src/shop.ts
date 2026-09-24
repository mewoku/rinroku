/**
 * Port of Ronriku.Domain.Shop.ShopCatalogue (C#): six figures rotate every UTC day and everyone sees
 * the same shelf.
 *   daySeed = Mix(DailyCalendar.Seed(day), 0x53484F50)          ("SHOP")
 *   item i  = FigureGenerator.Generate((ulong)Mix(daySeed, i), [3,3,4,4,5,5][i])
 *   id      = "fig-{seed as ulong decimal}-{size}", price by rarity (Legendary = SOL only)
 */
import { dailySeed, mix } from "./daily/index.js";
import { figurePrice } from "./economy.js";
import type { Figure } from "./figures/figure.js";
import { generateFigure } from "./figures/generator.js";
import { u64 } from "./rng.js";

export const SHELF_SIZE = 6;
const SHELF_SIZES = [3, 3, 4, 4, 5, 5];

export interface ShopShelfItem {
  id: string;
  /** Unsigned 64-bit generator seed. */
  seed: bigint;
  size: number;
  figure: Figure;
  /** Shard price, or -1 when SOL-only. */
  priceShards: number;
}

export function shopItem(seed: bigint, size: number): ShopShelfItem {
  const s = u64(seed);
  const figure = generateFigure(s, size);
  return { id: `fig-${s}-${size}`, seed: s, size, figure, priceShards: figurePrice(figure.rarity) };
}

export function shopShelf(day: number): ShopShelfItem[] {
  const daySeed = mix(dailySeed(day), 0x53484f50n);
  return SHELF_SIZES.map((size, i) => shopItem(mix(daySeed, BigInt(i)), size));
}
