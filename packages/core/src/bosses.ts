/**
 * Port of Ronriku.Domain.Adventure.BossEvent (C#): three weekly bosses (tier 0..2). Weeks start
 * Monday 00:00 UTC.
 *   week      = floor((weekStart − 2026-09-01).TotalDays / 7)   — floor, so the week before the Tuesday epoch is -1
 *   weekSeed  = Mix((long)FNV1a64("ronriku:boss:v1:" + week), 0)
 *   boss seed = Mix(weekSeed, tier), id "boss-w{week}-{tier}", ends weekStart + 7 days
 *   monster   = Generate((ulong)seed, 5, monster: true); name = monster name
 *   entry     = 150 + 50·tier shards; reward = 450 + 250·tier shards
 *   stages    = Pattern Mix(seed, 201), Spatial Mix(seed, 202), Logic Mix(seed, 203), all Standard
 *
 * Weeks are floored, so 2026-08-31 is week -1 and 2026-09-07 is week 0.
 */
import { mix } from "./daily/index.js";
import type { Figure } from "./figures/figure.js";
import { generateFigure } from "./figures/generator.js";
import type { LevelStage } from "./levels/index.js";
import { fnv1a64, i64, u64 } from "./rng.js";

const EPOCH_MS = Date.UTC(2026, 8, 1);
const DAY_MS = 86_400_000;

export interface BossEventDef {
  id: string;
  week: number;
  tier: number;
  seed: bigint;
  monster: Figure;
  name: string;
  entryShards: number;
  reward: number;
  startsUtc: Date;
  endsUtc: Date;
  stages: LevelStage[];
}

export function bossStagesForEvent(seed: bigint): LevelStage[] {
  return (["Pattern", "Spatial", "Logic"] as const).map((kind, i) => ({ kind, difficulty: "Standard" as const, seed: mix(seed, BigInt(201 + i)) }));
}

export function bossEvent(week: number, tier: number, weekStart: Date): BossEventDef {
  const weekSeed = mix(i64(fnv1a64(`ronriku:boss:v1:${week}`)), 0n);
  const seed = mix(weekSeed, BigInt(tier));
  const monster = generateFigure(u64(seed), 5, true);
  return {
    id: `boss-w${week}-${tier}`, week, tier, seed, monster, name: monster.name,
    entryShards: 150 + tier * 50, reward: 450 + tier * 250,
    startsUtc: weekStart, endsUtc: new Date(weekStart.getTime() + 7 * DAY_MS), stages: bossStagesForEvent(seed),
  };
}

export function bossEventsForWeek(utcNow: Date = new Date()): BossEventDef[] {
  const dayMs = Math.floor(utcNow.getTime() / DAY_MS) * DAY_MS;
  const sinceMonday = (new Date(dayMs).getUTCDay() + 6) % 7;
  const weekStart = new Date(dayMs - sinceMonday * DAY_MS);
  const week = Math.floor((weekStart.getTime() - EPOCH_MS) / DAY_MS / 7);
  return [0, 1, 2].map((tier) => bossEvent(week, tier, weekStart));
}
