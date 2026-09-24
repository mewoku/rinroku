import { dailySeed, dayNumber, figurePrice, generateFigure, mix, type Figure } from "@ronriku/core";
import type { Rarity, Tier } from "./types";

/** Mirrors client/Assets/Ronriku/Scripts/Domain/Shop/Shop.cs `ShopCatalogue.ForDay`. */
export const SHELF_SIZE = 6;
const SIZES: Tier[] = [3, 3, 4, 4, 5, 5];
const SHOP_STREAM = 0x53484f50n; // "SHOP"

export interface ShelfItem {
  id: string; // fig-{seed}-{size}, same as the Unity client
  slot: number;
  day: number;
  /** Unsigned 64-bit seed as decimal string. */
  seed: string;
  tier: Tier;
  name: string;
  rarity: Rarity;
  encoding: string;
  /** Shards, or null when SOL-only (Legendary). */
  priceShards: number | null;
}

export function shelfFigure(day: number, slot: number): { seed: bigint; tier: Tier; figure: Figure } {
  if (!Number.isInteger(slot) || slot < 0 || slot >= SHELF_SIZE) throw new RangeError("slot out of range");
  const daySeed = mix(dailySeed(day), SHOP_STREAM);
  const seed = BigInt.asUintN(64, mix(daySeed, BigInt(slot)));
  const tier = SIZES[slot]!;
  return { seed, tier, figure: generateFigure(seed, tier) };
}

export function shelfForDay(day: number = dayNumber()): ShelfItem[] {
  return Array.from({ length: SHELF_SIZE }, (_, slot) => {
    const { seed, tier, figure } = shelfFigure(day, slot);
    const price = figurePrice(figure.rarity);
    return {
      id: `fig-${seed}-${tier}`,
      slot,
      day,
      seed: seed.toString(),
      tier,
      name: figure.name,
      rarity: figure.rarity,
      encoding: figure.encode(),
      priceShards: price < 0 ? null : price,
    };
  });
}

export { dayNumber };
