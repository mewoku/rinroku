/**
 * Trusted content publisher (service role). SQL cannot run the deterministic generators, so this
 * script uses @ronriku/core to write everything the RPCs need to stay server-authoritative:
 *
 *   puzzle_keys   answer keys for Daily trials (a day window), every adventure level stage and every
 *                 boss event's stages → RPCs verify submitted answers without trusting the client.
 *   boss_events   the weekly roster (C# BossEvent.ForWeek) for this and next week, upserted by code.
 *   shop_shelf    the daily shelf (C# ShopCatalogue.ForDay) with RF1 encodings and prices.
 *   figure_pool   random-seed 4×4 Common figures for the sign-up starter grant.
 *
 * Idempotent: safe to run repeatedly (e.g. a daily cron in production).
 * Usage: node scripts/publish.ts [--days-back 2] [--days-ahead 30]
 */
import { randomBytes } from "node:crypto";
import { fileURLToPath } from "node:url";
import { createClient, type SupabaseClient } from "@supabase/supabase-js";
import {
  LEVELS_PER_WORLD, ORIENTATION_COUNT, WORLD_COUNT, bossEventsForWeek, dailyPlan, dayNumber, generateFigure,
  generateLogic, generatePattern, generateSpatial, i64, levelDef, shadow, shopShelf, targetMilliseconds,
  type PuzzleDifficulty, type TrialKind,
} from "@ronriku/core";
import { supabaseEnv } from "./env.ts";

interface Stage {
  kind: TrialKind;
  seed: bigint;
  difficulty: PuzzleDifficulty;
}

/** One puzzle_keys row. Seeds are sent as decimal strings (bigint). */
export function puzzleKey(source: string, stage: number, s: Stage): Record<string, unknown> {
  const base = { source, stage, kind: s.kind, seed: s.seed.toString() };
  if (s.kind === "Pattern") {
    const p = generatePattern(s.seed, s.difficulty);
    return { ...base, content_hash: p.metadata.contentHash, par: 0, target_ms: targetMilliseconds("pattern", s.difficulty), correct_option: p.correctOption };
  }
  if (s.kind === "Spatial") {
    const p = generateSpatial(s.seed, s.difficulty);
    const solved: number[] = [];
    for (let o = 0; o < ORIENTATION_COUNT; o++) if (shadow(p.cubes, o) === p.targetShadow) solved.push(o);
    return {
      ...base, content_hash: p.metadata.contentHash, par: p.par, target_ms: targetMilliseconds("spatial", s.difficulty),
      start_orientation: p.startOrientation, solved_orientations: solved,
    };
  }
  const p = generateLogic(s.seed, s.difficulty);
  // Logic par = cells − 1 (a perfect path); over-par steps cost 15 points (LogicPuzzleScreen).
  return { ...base, content_hash: p.metadata.contentHash, par: p.size * p.size - 1, target_ms: targetMilliseconds("logic", s.difficulty), solution: p.solution };
}

async function upsert(db: SupabaseClient, table: string, rows: Record<string, unknown>[], onConflict: string): Promise<void> {
  for (let i = 0; i < rows.length; i += 200) {
    const { error } = await db.from(table).upsert(rows.slice(i, i + 200), { onConflict });
    if (error) throw new Error(`${table}: ${error.message}`);
  }
}

export interface PublishOptions {
  daysBack?: number;
  daysAhead?: number;
  starterPool?: number;
  now?: Date;
  log?: (msg: string) => void;
}

export async function publish(db: SupabaseClient, options: PublishOptions = {}): Promise<void> {
  const now = options.now ?? new Date();
  const log = options.log ?? (() => {});
  const today = dayNumber(now);
  const days: number[] = [];
  for (let d = today - (options.daysBack ?? 2); d <= today + (options.daysAhead ?? 30); d++) if (d >= 1) days.push(d);

  // Weekly boss roster (this week and next).
  const roster = [...bossEventsForWeek(now), ...bossEventsForWeek(new Date(now.getTime() + 7 * 86_400_000))];
  await upsert(db, "boss_events", roster.map((b) => ({
    code: b.id, tier: b.tier, name: b.name, palette: "boss", seed: b.seed.toString(),
    stages: b.stages.map((s) => ({ kind: s.kind, seed: s.seed.toString() })),
    entry_shards: b.entryShards, entry_lamports: 10_000_000, reward_shards: b.reward, time_limit_ms: 300_000,
    starts_at: b.startsUtc.toISOString(), ends_at: b.endsUtc.toISOString(),
  })), "code");
  log(`boss_events: ${roster.length} roster events`);

  // Puzzle keys.
  const keys: Record<string, unknown>[] = [];
  for (const day of days) dailyPlan(day).trials.forEach((t, i) => keys.push(puzzleKey(`daily:${day}`, i, t)));
  for (let w = 0; w < WORLD_COUNT; w++)
    for (let i = 0; i < LEVELS_PER_WORLD; i++) levelDef(w, i).stages.forEach((s, k) => keys.push(puzzleKey(`level:${w}:${i}`, k, s)));
  const { data: bosses, error } = await db.from("boss_events").select("id, stages");
  if (error) throw new Error(error.message);
  for (const b of bosses ?? [])
    (b.stages as { kind: TrialKind; seed: string }[]).forEach((s, k) =>
      keys.push(puzzleKey(`boss:${b.id}`, k, { kind: s.kind, seed: BigInt(s.seed), difficulty: "Standard" })));
  await upsert(db, "puzzle_keys", keys, "source,stage");
  log(`puzzle_keys: ${keys.length} stages (days ${days[0]}..${days[days.length - 1]}, 5 worlds, ${bosses?.length ?? 0} bosses)`);

  // Daily shop shelves.
  const shelf = days.flatMap((day) => shopShelf(day).map((item, slot) => ({
    day, slot, item_id: item.id, seed: i64(item.seed).toString(), tier: item.size, rarity: item.figure.rarity,
    name: item.figure.name, encoding: item.figure.encode(),
    price_shards: item.priceShards > 0 ? item.priceShards : null,
    price_lamports: item.priceShards > 0 ? null : 100_000_000,
  })));
  await upsert(db, "shop_shelf", shelf, "day,slot");
  log(`shop_shelf: ${shelf.length} items`);

  // Starter pool: random seeds, 4×4 Common.
  const target = options.starterPool ?? 200;
  const { count } = await db.from("figure_pool").select("id", { count: "exact", head: true }).eq("tier", 4).eq("rarity", "Common");
  const pool: Record<string, unknown>[] = [];
  while ((count ?? 0) + pool.length < target) {
    const seed = randomBytes(8).readBigInt64LE();
    const f = generateFigure(seed, 4);
    if (f.rarity === "Common") pool.push({ seed: seed.toString(), tier: 4, rarity: f.rarity, name: f.name, encoding: f.encode() });
  }
  await upsert(db, "figure_pool", pool, "seed,tier");
  log(`figure_pool: +${pool.length} starter figures`);
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const arg = (name: string) => {
    const i = process.argv.indexOf(name);
    return i > 0 ? Number(process.argv[i + 1]) : undefined;
  };
  const env = supabaseEnv();
  const db = createClient(env.url, env.serviceRoleKey, { auth: { persistSession: false } });
  await publish(db, { daysBack: arg("--days-back"), daysAhead: arg("--days-ahead"), log: console.log });
}
