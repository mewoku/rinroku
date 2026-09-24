import "server-only";

import { Figure, generateFigure, type FigureTraits } from "@ronriku/core";
import { renderFigurePng } from "@ronriku/core/render";
import { findDemoFigure } from "../demo/data";
import { getAnonServerSupabase, getServiceSupabase } from "../supabase/server";
import type { FigureRecord, Rarity, Tier } from "../types";

/** Resolves a figure by id: Supabase `figures` row (public read), or a demo fixture id. */
export async function resolveFigure(id: string): Promise<FigureRecord | null> {
  if (id.startsWith("demo-")) {
    const demo = findDemoFigure(id);
    if (demo) return demo;
    const m = /^demo-(\d+)-([345])$/.exec(id);
    if (!m) return null;
    const f = generateFigure(BigInt.asUintN(64, BigInt(m[1]!)), Number(m[2]));
    return { id, seed: m[1]!, tier: Number(m[2]) as Tier, encoding: f.encode(), rarity: f.rarity, name: f.name, ownerId: null, mintAddress: null, createdAt: null };
  }
  const client = getAnonServerSupabase() ?? getServiceSupabase();
  if (!client) return null;
  const { data, error } = await client.from("figures").select("id,seed,tier,encoding,rarity,name,owner_id,mint_address,created_at").eq("id", id).maybeSingle();
  if (error || !data) return null;
  return {
    id: String(data.id),
    seed: String(data.seed),
    tier: Number(data.tier) as Tier,
    encoding: String(data.encoding),
    rarity: data.rarity as Rarity,
    name: String(data.name),
    ownerId: data.owner_id ? String(data.owner_id) : null,
    mintAddress: data.mint_address ? String(data.mint_address) : null,
    createdAt: data.created_at ? String(data.created_at) : null,
  };
}

/** Traits are not part of RF1; regenerate from (seed, tier) and only trust them if the encoding matches. */
export function traitsFor(fig: FigureRecord): FigureTraits | null {
  try {
    const regenerated = generateFigure(BigInt.asUintN(64, BigInt(fig.seed)), fig.tier);
    return regenerated.encode() === fig.encoding ? (regenerated.traits ?? null) : null;
  } catch {
    return null;
  }
}

export function renderPng(encoding: string, size = 256): Buffer {
  return renderFigurePng(Figure.decode(encoding), { size });
}

/** Finds a fresh random seed whose generated figure has the requested rarity. */
export function generateWithRarity(tier: Tier, rarity: Rarity, maxTries = 20_000): { seed: bigint; figure: Figure } {
  const buf = new BigUint64Array(1);
  for (let i = 0; i < maxTries; i++) {
    crypto.getRandomValues(buf);
    const seed = buf[0]! & ((1n << 63n) - 1n); // keep it positive so it fits a signed bigint column as-is
    const figure = generateFigure(seed, tier);
    if (figure.rarity === rarity) return { seed, figure };
  }
  throw new Error(`No ${rarity} figure found in ${maxTries} tries.`);
}

export const TRAIT_LABELS: Record<keyof FigureTraits, string> = {
  skin: "Skin",
  outfit: "Outfit",
  accent: "Accent",
  hair: "Hair",
  pants: "Pants",
  shoes: "Shoes",
  headwear: "Headwear",
  eyes: "Eyes",
  outfitStyle: "Outfit style",
  accessory: "Accessory",
  glow: "Glow",
};
