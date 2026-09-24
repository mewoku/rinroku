/**
 * Port of Ronriku.Domain.Daily (DailyCalendar + DailyPlan). Seeds are C# `long` values and are
 * returned as signed 64-bit bigints.
 */
import { fnv1a64, i64, u64 } from "../rng.js";
import type { PuzzleDifficulty, TrialKind } from "../puzzles/types.js";

export const RULES_VERSION = 2;
export const TRIAL_COUNT = 3;
const EPOCH_MS = Date.UTC(2026, 8, 1); // 2026-09-01T00:00:00Z = day 1
const DAY_MS = 86_400_000;

/** UTC day number; day 1 is 2026-09-01, the boundary is 00:00 UTC. */
export function dayNumber(utcNow: Date = new Date()): number {
  return Math.floor((utcNow.getTime() - EPOCH_MS) / DAY_MS) + 1;
}

export function dateOf(day: number): Date {
  return new Date(EPOCH_MS + (day - 1) * DAY_MS);
}

/** Milliseconds until the next 00:00 UTC. */
export function untilResetMs(utcNow: Date = new Date()): number {
  const t = utcNow.getTime();
  return (Math.floor(t / DAY_MS) + 1) * DAY_MS - t;
}

function ymd(day: number, sep: string): string {
  const d = dateOf(day);
  const p = (n: number, w = 2) => String(n).padStart(w, "0");
  return `${p(d.getUTCFullYear(), 4)}${sep}${p(d.getUTCMonth() + 1)}${sep}${p(d.getUTCDate())}`;
}

export function challengeId(day: number): string {
  return `daily-${ymd(day, "")}-v${RULES_VERSION}`;
}

/** FNV-1a over `ronriku:daily:{RulesVersion}:{yyyy-MM-dd}`, reinterpreted as signed 64-bit. */
export function dailySeed(day: number): bigint {
  return i64(fnv1a64(`ronriku:daily:${RULES_VERSION}:${ymd(day, "-")}`));
}

/** SplitMix64 finaliser over seed + stream (C# DailyPlan.Mix). Returns signed 64-bit. */
export function mix(seed: bigint, stream: bigint): bigint {
  let z = u64(u64(seed) + u64(0x9e3779b97f4a7c15n * u64(i64(stream + 1n))));
  z = u64((z ^ (z >> 30n)) * 0xbf58476d1ce4e5b9n);
  z = u64((z ^ (z >> 27n)) * 0x94d049bb133111ebn);
  return i64(z ^ (z >> 31n));
}

export interface TrialSpec {
  index: number;
  kind: TrialKind;
  difficulty: PuzzleDifficulty;
  seed: bigint;
}

export interface DailyPlan {
  day: number;
  challengeId: string;
  seed: bigint;
  trials: TrialSpec[];
  /** Seed for decorative preview content that must not reveal any trial. */
  previewSeed: bigint;
}

/** Pattern, Spatial, Logic — all Standard. Changing this line-up requires bumping RULES_VERSION. */
export function dailyPlan(day: number): DailyPlan {
  const seed = dailySeed(day);
  return {
    day,
    challengeId: challengeId(day),
    seed,
    trials: [
      { index: 0, kind: "Pattern", difficulty: "Standard", seed: mix(seed, 1n) },
      { index: 1, kind: "Spatial", difficulty: "Standard", seed: mix(seed, 2n) },
      { index: 2, kind: "Logic", difficulty: "Standard", seed: mix(seed, 3n) },
    ],
    previewSeed: mix(seed, 0x5052455649455700n),
  };
}
