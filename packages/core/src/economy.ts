/**
 * Shard economy constants (PLAN_V2 §6), mirroring Ronriku.Domain.Player.Economy (C#). The database
 * RPCs (backend/supabase/migrations) enforce the same numbers server-side.
 */
import type { FigureRarity } from "./figures/figure.js";

export const LEVEL_BASE = 20;
export const STAR_BONUS = 10;
export const BOSS_CLEAR = 300;
export const BOSS_EVENT_ENTRY = 150;
export const BOSS_RETRY = 50;

/** First clear of a level: (boss ? 300 : 20) + (stars − 1)·10. Replays pay 10 per newly earned star. */
export const levelReward = (stars: number, boss: boolean) => (boss ? BOSS_CLEAR : LEVEL_BASE) + (stars - 1) * STAR_BONUS;

/** 100 + min(100, 10·streak). */
export const dailyReward = (streak: number) => 100 + Math.min(100, 10 * Math.max(0, streak));

/** Shard price by rarity; Legendary is SOL-only (−1). */
export const figurePrice = (rarity: FigureRarity) =>
  rarity === "Common" ? 300 : rarity === "Rare" ? 800 : rarity === "Epic" ? 2000 : -1;
