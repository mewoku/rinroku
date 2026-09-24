export type Rarity = "Common" | "Rare" | "Epic" | "Legendary";
export type Tier = 3 | 4 | 5;
export type Currency = "shards" | "sol";

export const RARITIES: readonly Rarity[] = ["Common", "Rare", "Epic", "Legendary"] as const;
export const TIERS: readonly Tier[] = [3, 4, 5] as const;

/** Shape of packages/core/fixtures/figures.json entries (used for demo data). */
export interface FigureFixture {
  seed: string;
  tier: number;
  monster: boolean;
  name: string;
  rarity: string;
  encoding: string;
}

/** `figures` row (PLAN §7). */
export interface FigureRecord {
  id: string;
  seed: string;
  tier: Tier;
  encoding: string;
  rarity: Rarity;
  name: string;
  ownerId: string | null;
  mintAddress: string | null;
  createdAt: string | null;
}

/** `profiles` row (PLAN §7) — public subset. */
export interface Profile {
  id: string;
  handle: string;
  displayName: string | null;
  avatarFigureId: string | null;
  avatarEncoding: string | null;
  rating: number;
  shards: number;
  streak: number;
  bestStreak: number;
  walletAddress: string | null;
  createdAt: string | null;
}

export interface Listing {
  id: string;
  figure: FigureRecord;
  sellerHandle: string;
  priceShards: number | null;
  priceLamports: number | null;
  status: string;
  createdAt: string | null;
}

export type LeaderboardScope = "global" | "daily" | "friends" | "boss";

export interface LeaderboardRow {
  rank: number;
  handle: string;
  rating: number;
  score: number;
  /** Boss scope: fastest winning time (lower is better). */
  elapsedMs: number | null;
  avatarEncoding: string | null;
}

export interface FriendRow {
  userId: string;
  handle: string;
  rating: number;
  status: "pending_in" | "pending_out" | "accepted";
  avatarEncoding: string | null;
}

export type DataSource = "live" | "demo";

export interface Sourced<T> {
  data: T;
  source: DataSource;
  /** Why demo data is shown (backend unreachable, not configured, ...). */
  reason?: string;
}
