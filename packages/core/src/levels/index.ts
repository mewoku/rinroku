/**
 * Adventure level definitions (PLAN_V2 §8). Port of Ronriku.Domain.Adventure.LevelDef (C#), which
 * is the source of truth. Levels are never stored; everything derives from (world, index):
 *
 *   world 0..WORLD_COUNT-1 (0-based), index 0..11 (0-based), index 11 is the world boss.
 *   worldSeed   = Mix((long)FNV1a64("ronriku:world:v1:" + world), 0)
 *   seed        = Mix(worldSeed, index)
 *   kind        = [Pattern, Spatial, Logic][index % 3]
 *   difficulty  = Standard (middle band) for every level and every boss stage
 *   monster     = FigureGenerator.Generate((ulong)seed, isBoss ? 5 : 3 + index * 2 / 11, monster: true)
 *   bossStages  = Pattern Mix(seed, 101), Spatial Mix(seed, 102), Logic Mix(seed, 103)
 *
 * Mix is DailyPlan.Mix (SplitMix64 finaliser); FNV1a64 is the DailyCalendar.Seed hash over UTF-16
 * code units. `world` is written in invariant decimal, e.g. "ronriku:world:v1:0".
 */
import { mix } from "../daily/index.js";
import { fnv1a64, i64, u64 } from "../rng.js";
import type { PuzzleDifficulty, TrialKind } from "../puzzles/types.js";

export const LEVELS_PER_WORLD = 12;
export const BOSS_INDEX = LEVELS_PER_WORLD - 1;
export const WORLD_COUNT = 5;
export const WORLD_NAMES = ["LAB", "PRISM", "EMBER", "GROVE", "FROST"] as const;
const KINDS: TrialKind[] = ["Pattern", "Spatial", "Logic"];

export interface LevelStage {
  kind: TrialKind;
  difficulty: PuzzleDifficulty;
  seed: bigint;
}

export interface LevelDef {
  world: number;
  index: number;
  /** Signed 64-bit (C# long). */
  seed: bigint;
  kind: TrialKind;
  difficulty: PuzzleDifficulty;
  isBoss: boolean;
  /** Normal levels: one stage {kind, seed}. Boss: the three chained stages. */
  stages: LevelStage[];
  /** Guardian monster figure: generateFigure(monster.seed, monster.tier, true). */
  monster: { seed: bigint; tier: number };
}

export function worldSeed(world: number): bigint {
  return mix(i64(fnv1a64(`ronriku:world:v1:${world}`)), 0n);
}

export function bossStages(seed: bigint): LevelStage[] {
  return KINDS.map((kind, i) => ({ kind, difficulty: "Standard" as const, seed: mix(seed, BigInt(101 + i)) }));
}

export function levelDef(world: number, index: number): LevelDef {
  if (!Number.isInteger(world) || world < 0 || world >= WORLD_COUNT) throw new RangeError(`world must be 0..${WORLD_COUNT - 1}`);
  if (!Number.isInteger(index) || index < 0 || index >= LEVELS_PER_WORLD) throw new RangeError(`index must be 0..${BOSS_INDEX}`);
  const seed = mix(worldSeed(world), BigInt(index));
  const kind = KINDS[index % 3];
  const isBoss = index === BOSS_INDEX;
  return {
    world, index, seed, kind, difficulty: "Standard", isBoss,
    stages: isBoss ? bossStages(seed) : [{ kind, difficulty: "Standard", seed }],
    monster: { seed: u64(seed), tier: isBoss ? 5 : 3 + Math.floor((index * 2) / BOSS_INDEX) },
  };
}
