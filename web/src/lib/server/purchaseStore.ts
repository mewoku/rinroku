import "server-only";

import type { SupabaseClient } from "@supabase/supabase-js";
import type { BossRow, Claim, FigureRow, PurchaseStore, ShelfSlot } from "./purchase";

type Row = Record<string, unknown>;

function fail(what: string, error: { message: string } | null): never {
  throw new Error(`${what}: ${error?.message ?? "unknown error"}`);
}

const fig = (r: Row): FigureRow => ({
  id: String(r.id),
  name: String(r.name ?? ""),
  ownerId: r.owner_id ? String(r.owner_id) : null,
  mintAddress: r.mint_address ? String(r.mint_address) : null,
});

/** PurchaseStore over the service-role Supabase client (bypasses RLS; server only). */
export function supabasePurchaseStore(db: SupabaseClient): PurchaseStore {
  return {
    async linkedWallet(userId) {
      const { data, error } = await db.from("profiles").select("wallet_address").eq("id", userId).maybeSingle();
      if (error) fail("profile", error);
      return data?.wallet_address ? String(data.wallet_address) : null;
    },
    async claimBySignature(signature) {
      const { data, error } = await db.from("transactions").select("user_id,kind,ref_id,lamports").eq("signature", signature).maybeSingle();
      if (error) fail("ledger", error);
      if (!data) return null;
      return { userId: String(data.user_id), kind: String(data.kind), refId: data.ref_id ? String(data.ref_id) : null, lamports: data.lamports == null ? null : Number(data.lamports) } satisfies Claim;
    },
    async insertClaim(signature, c) {
      const { error } = await db.from("transactions").insert({ user_id: c.userId, kind: c.kind, shards_delta: 0, lamports: c.lamports, signature, ref_id: c.refId });
      if (!error) return "ok";
      if (error.code === "23505") return "duplicate";
      fail("ledger insert", error);
    },
    async shelfSlot(day, slot) {
      const { data, error } = await db
        .from("shop_shelf")
        .select("day,slot,item_id,seed,tier,rarity,name,encoding,price_lamports")
        .eq("day", day)
        .eq("slot", slot)
        .maybeSingle();
      if (error) fail("shelf", error);
      if (!data) return null;
      return {
        day: Number(data.day),
        slot: Number(data.slot),
        itemId: String(data.item_id),
        seed: String(data.seed),
        tier: Number(data.tier),
        rarity: String(data.rarity),
        name: String(data.name),
        encoding: String(data.encoding),
        priceLamports: data.price_lamports == null ? null : Number(data.price_lamports),
      } satisfies ShelfSlot;
    },
    async findOwnedFigure(userId, seed, tier) {
      const { data, error } = await db.from("figures").select("id,name,owner_id,mint_address").eq("owner_id", userId).eq("seed", seed).eq("tier", tier).limit(1).maybeSingle();
      if (error) fail("figure", error);
      return data ? fig(data) : null;
    },
    async insertFigure(r) {
      // Deterministic ids (SOL Legendaries): a concurrent resume may have inserted the row already.
      const { error } = await db
        .from("figures")
        .upsert({ id: r.id, seed: r.seed, tier: r.tier, encoding: r.encoding, rarity: r.rarity, name: r.name, owner_id: r.userId, source: "sol" }, { onConflict: "id", ignoreDuplicates: true });
      if (error) fail("figure insert", error);
      const { data, error: readErr } = await db.from("figures").select("id,name,owner_id,mint_address").eq("id", r.id).single();
      if (readErr || !data) fail("figure insert", readErr);
      return fig(data);
    },
    async figure(id) {
      const { data, error } = await db.from("figures").select("id,name,owner_id,mint_address").eq("id", id).maybeSingle();
      if (error) fail("figure", error);
      return data ? fig(data) : null;
    },
    async hasActiveListing(figureId) {
      const { count, error } = await db.from("listings").select("id", { count: "exact", head: true }).eq("figure_id", figureId).eq("status", "active");
      if (error) fail("listings", error);
      return (count ?? 0) > 0;
    },
    async cancelActiveListings(figureId) {
      const { error } = await db.from("listings").update({ status: "cancelled", closed_at: new Date().toISOString() }).eq("figure_id", figureId).eq("status", "active");
      if (error) fail("cancel listings", error);
    },
    async boss(id) {
      const { data, error } = await db.from("boss_events").select("id,entry_lamports,starts_at,ends_at,stages").eq("id", id).maybeSingle();
      if (error) fail("boss", error);
      if (!data) return null;
      return {
        id: String(data.id),
        entryLamports: Number(data.entry_lamports),
        startsAt: String(data.starts_at),
        endsAt: String(data.ends_at),
        stages: Array.isArray(data.stages) ? data.stages.length : 0,
      } satisfies BossRow;
    },
    async bossKeysPublished(bossId, stages) {
      const { count, error } = await db.from("puzzle_keys").select("stage", { count: "exact", head: true }).eq("source", `boss:${bossId}`).lt("stage", stages);
      if (error) fail("puzzle keys", error);
      return (count ?? 0) >= stages;
    },
    async hasOpenAttempt(userId, bossId) {
      const { count, error } = await db.from("boss_attempts").select("id", { count: "exact", head: true }).eq("user_id", userId).eq("boss_id", bossId).eq("result", "open");
      if (error) fail("attempts", error);
      return (count ?? 0) > 0;
    },
    async attempt(id) {
      const { data, error } = await db.from("boss_attempts").select("id,boss_id,user_id").eq("id", id).maybeSingle();
      if (error) fail("attempt", error);
      return data ? { id: String(data.id), bossId: String(data.boss_id), userId: String(data.user_id) } : null;
    },
    async enterBossSol(userId, bossId, signature, lamports) {
      const { data, error } = await db.rpc("enter_boss_sol", { p_user: userId, p_boss_id: bossId, p_signature: signature, p_lamports: lamports });
      if (error) return { ok: false, error: error.message };
      const row = (Array.isArray(data) ? data[0] : data) as Row | null;
      return row?.id ? { ok: true, attemptId: String(row.id) } : { ok: false, error: "no_attempt_returned" };
    },
    async reserveMint(figureId, address) {
      const { data, error } = await db
        .from("figures")
        .update({ mint_address: address })
        .eq("id", figureId)
        .or(`mint_address.is.null,mint_address.eq.${address}`)
        .select("id")
        .maybeSingle();
      if (error) {
        if (error.code === "23505") return false; // address already used by another row
        fail("reserve mint", error);
      }
      return !!data;
    },
  };
}
