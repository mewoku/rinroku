import { createClient, type SupabaseClient } from "@supabase/supabase-js";
import {
  dailyPlan, dayNumber, generateLogic, generatePattern, generateSpatial, solveSpatial, type LevelStage, type TrialSpec,
} from "@ronriku/core";
import { supabaseEnv } from "../scripts/env.ts";

export const env = supabaseEnv();
export const admin = createClient(env.url, env.serviceRoleKey, { auth: { persistSession: false, autoRefreshToken: false } });
export const anon = () => createClient(env.url, env.anonKey, { auth: { persistSession: false, autoRefreshToken: false } });

export interface Player {
  db: SupabaseClient;
  id: string;
  handle: string;
}

/** Anonymous sign-in → a fresh player with a profile (created by the auth trigger). */
export async function newPlayer(): Promise<Player> {
  const db = anon();
  const { data, error } = await db.auth.signInAnonymously();
  if (error || !data.user) throw error ?? new Error("no user");
  const profile = await rpc(db, "ensure_profile");
  return { db, id: data.user.id, handle: profile.handle };
}

/** Calls an RPC and throws on error. */
export async function rpc<T = any>(db: SupabaseClient, fn: string, args?: Record<string, unknown>): Promise<T> {
  const { data, error } = await db.rpc(fn, args);
  if (error) throw new Error(error.message);
  return data as T;
}

/** Calls an RPC expecting failure; returns the error message. */
export async function rpcError(db: SupabaseClient, fn: string, args?: Record<string, unknown>): Promise<string> {
  const { error } = await db.rpc(fn, args);
  if (!error) throw new Error(`${fn} unexpectedly succeeded`);
  return error.message;
}

export async function setShards(id: string, shards: number): Promise<void> {
  const { error } = await admin.from("profiles").update({ shards }).eq("id", id);
  if (error) throw error;
}

export async function shards(id: string): Promise<number> {
  const { data, error } = await admin.from("profiles").select("shards").eq("id", id).single();
  if (error) throw error;
  return data.shards;
}

/** Correct answer for a stage, computed with @ronriku/core like a real client would. */
export function answerFor(stage: Pick<LevelStage | TrialSpec, "kind" | "seed" | "difficulty">): number | number[] {
  if (stage.kind === "Pattern") return generatePattern(stage.seed, stage.difficulty).correctOption;
  if (stage.kind === "Spatial") {
    const p = generateSpatial(stage.seed, stage.difficulty);
    return solveSpatial(p.cubes, p.startOrientation, p.targetShadow)!;
  }
  return generateLogic(stage.seed, stage.difficulty).solution;
}

export const today = () => dayNumber(new Date());

/** Daily payload in the exact Unity shape: {kind, elapsed_ms, resets, moves, answer}. */
export function dailyOutcomes(day: number, elapsedMs = 20_000, solve: boolean[] = [true, true, true]) {
  return dailyPlan(day).trials.map((t, i) => {
    const answer = solve[i] ? answerFor(t) : null;
    const moves = answer === null ? 0 : t.kind === "Pattern" ? 1 : t.kind === "Spatial" ? (answer as number[]).length : (answer as number[]).length - 1;
    return { kind: t.kind as string, elapsed_ms: elapsedMs, resets: 0, moves, answer };
  });
}

/** Marks the player as having non-trivial progress (cleared level 0-0), for marketplace gates. */
export async function giveProgress(id: string): Promise<void> {
  const { error } = await admin.from("level_progress").upsert({ user_id: id, world: 0, level: 0, stars: 1, best_ms: 5000 });
  if (error) throw error;
}
