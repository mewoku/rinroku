import type { Currency, Rarity, Tier } from "./types";

/** PLAN_V2 §6 economy constants. Server-authoritative values live in the backend; these mirror them for display. */
export const LAMPORTS_PER_SOL = 1_000_000_000;

export const EARN = {
  levelClear: 20,
  perExtraStar: 10,
  dailyCompletion: 100,
  dailyStreakBonusPerDay: 10,
  dailyStreakBonusCap: 100,
  bossWinMin: 300,
  bossWinMax: 1000,
} as const;

export const BOSS_ENTRY = { shards: 150, lamports: 10_000_000 } as const; // 0.01 SOL

/** Devnet fee to mint a shard-bought (off-chain) figure to a wallet. PLAN §6 says "small"; 0.005 SOL. */
export const MINT_FEE_LAMPORTS = 5_000_000;

export type ShopItemId =
  | "figure-common"
  | "figure-rare"
  | "figure-epic"
  | "figure-legendary"
  | "boss-entry"
  | "mint-fee";

export interface ShopItem {
  /** shop_items.id when live; PLAN catalogue ids when offline. */
  id: string;
  label: string;
  blurb: string;
  rarity: Rarity | null;
  /** Shards price, or null when not purchasable with shards. */
  priceShards: number | null;
  /** Devnet lamports price, or null when not purchasable with SOL. */
  priceLamports: number | null;
  /** Figure tier minted / granted (random 3–5 when null). */
  tier: Tier | null;
  kind: "figure" | "boss" | "mint";
}

export const SHOP_ITEMS: readonly ShopItem[] = [
  { id: "figure-common", label: "Common figure", blurb: "A random voxel pal. Tier 3–5.", rarity: "Common", priceShards: 300, priceLamports: null, tier: null, kind: "figure" },
  { id: "figure-rare", label: "Rare figure", blurb: "Rarer traits, brighter outfits.", rarity: "Rare", priceShards: 800, priceLamports: null, tier: null, kind: "figure" },
  { id: "figure-epic", label: "Epic figure", blurb: "Epic hats, glow eyes, accessories.", rarity: "Epic", priceShards: 2000, priceLamports: null, tier: null, kind: "figure" },
  { id: "figure-legendary", label: "Legendary figure", blurb: "Devnet SOL only. Minted as a Metaplex Core NFT.", rarity: "Legendary", priceShards: null, priceLamports: 100_000_000, tier: 5, kind: "figure" },
  { id: "boss-entry", label: "Boss entry", blurb: "One attempt at the current boss event.", rarity: null, priceShards: BOSS_ENTRY.shards, priceLamports: BOSS_ENTRY.lamports, tier: null, kind: "boss" },
] as const;

export const SOL_PURCHASABLE: readonly ShopItemId[] = ["figure-legendary", "boss-entry", "mint-fee"];

export function shopItem(id: ShopItemId): ShopItem | undefined {
  return SHOP_ITEMS.find((i) => i.id === id);
}

/** Lamport price the server expects for a SOL purchase. */
export function solPriceLamports(id: ShopItemId): number | null {
  if (id === "mint-fee") return MINT_FEE_LAMPORTS;
  return shopItem(id)?.priceLamports ?? null;
}

/** Integer shards with thin-space thousands grouping: 2000 → "2,000". */
export function formatShards(n: number): string {
  if (!Number.isFinite(n)) return "—";
  return Math.trunc(n).toLocaleString("en-US");
}

/**
 * Lamports → SOL string without float drift and trailing zeros, min 2 decimals up to 9.
 * 100_000_000 → "0.10", 1_500_000_000 → "1.50", 5_000 → "0.000005".
 */
export function formatSol(lamports: number | bigint): string {
  const l = typeof lamports === "bigint" ? lamports : BigInt(Math.trunc(lamports));
  const neg = l < 0n;
  const abs = neg ? -l : l;
  const whole = abs / 1_000_000_000n;
  let frac = (abs % 1_000_000_000n).toString().padStart(9, "0").replace(/0+$/, "");
  if (frac.length < 2) frac = frac.padEnd(2, "0");
  return `${neg ? "-" : ""}${whole.toLocaleString("en-US")}.${frac}`;
}

export function formatPrice(amount: number, currency: Currency): string {
  return currency === "sol" ? `${formatSol(amount)} SOL` : `${formatShards(amount)} ◆`;
}

export function solToLamports(sol: string): number {
  if (!/^\d+(\.\d{1,9})?$/.test(sol.trim())) throw new Error("Invalid SOL amount.");
  const [w, f = ""] = sol.trim().split(".");
  return Number(BigInt(w!) * 1_000_000_000n + BigInt(f.padEnd(9, "0")));
}

export function rarityOrder(r: Rarity): number {
  return ["Common", "Rare", "Epic", "Legendary"].indexOf(r);
}
