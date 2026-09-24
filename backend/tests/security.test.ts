import { applyMove } from "@ronriku/core";
import { beforeAll, describe, expect, it } from "vitest";
import { admin, anon, newPlayer, rpc, rpcError, shards, type Player } from "./helpers.ts";

let a: Player, b: Player;
beforeAll(async () => {
  a = await newPlayer();
  b = await newPlayer();
});

describe("anonymous sign-in", () => {
  it("creates a profile with starter shards and a starter avatar figure", async () => {
    const p = await rpc(a.db, "ensure_profile");
    expect(p.id).toBe(a.id);
    expect(p.shards).toBe(150);
    expect(p.handle).toMatch(/^p_[0-9a-f]{12}$/);
    expect(p.avatar_figure_id).toBeTruthy();
    const { data: fig } = await a.db.from("figures").select("owner_id, tier, rarity, encoding, source").eq("id", p.avatar_figure_id).single();
    expect(fig).toMatchObject({ owner_id: a.id, tier: 4, rarity: "Common", source: "starter" });
    expect(fig!.encoding).toMatch(/^RF1\.4\./);
  });

  it("ensure_profile is idempotent", async () => {
    await rpc(a.db, "ensure_profile");
    expect(await shards(a.id)).toBe(150);
    const { data } = await admin.from("transactions").select("kind").eq("user_id", a.id).eq("kind", "signup_bonus");
    expect(data).toHaveLength(1);
  });

  it("sign-up never fails on an empty starter pool; ensure_profile grants the starter lazily", async () => {
    const { data: pool } = await admin.from("figure_pool").select("*").eq("tier", 4).eq("rarity", "Common");
    await admin.from("figure_pool").delete().in("id", pool!.map((r) => r.id));
    try {
      const p = await newPlayer();
      expect((await rpc(p.db, "ensure_profile")).avatar_figure_id).toBeNull();
      expect(await shards(p.id)).toBe(150);
      await admin.from("figure_pool").insert(pool!.slice(0, 5));
      const after = await rpc(p.db, "ensure_profile");
      expect(after.avatar_figure_id).toBeTruthy();
      const { data: figs } = await admin.from("figures").select("source").eq("owner_id", p.id);
      expect(figs).toEqual([{ source: "starter" }]);
      expect((await rpc(p.db, "ensure_profile")).avatar_figure_id).toBe(after.avatar_figure_id);
    } finally {
      await admin.from("figure_pool").upsert(pool!.slice(5), { onConflict: "seed,tier" });
    }
  });

  it("requires authentication for player RPCs", async () => {
    expect(await rpcError(anon(), "ensure_profile")).toMatch(/permission denied|not_authenticated/);
  });
});

describe("RLS", () => {
  it("public profile columns are readable, economy columns are not", async () => {
    const { data, error } = await anon().from("profiles").select("id, handle, rating").eq("id", a.id).single();
    expect(error).toBeNull();
    expect(data!.handle).toBe(a.handle);
    const { error: shardsError } = await b.db.from("profiles").select("shards").eq("id", a.id);
    expect(shardsError?.message).toMatch(/permission denied/);
  });

  it("denies direct writes to economy/progress tables, including cross-user", async () => {
    const attempts = [
      b.db.from("profiles").update({ shards: 999999 }).eq("id", a.id),
      a.db.from("profiles").update({ shards: 999999 }).eq("id", a.id),
      b.db.from("figures").update({ owner_id: b.id }).eq("owner_id", a.id),
      b.db.from("transactions").insert({ user_id: a.id, kind: "hack", shards_delta: 1 }),
      b.db.from("level_progress").insert({ user_id: a.id, world: 0, level: 0, stars: 3, best_ms: 1000 }),
      b.db.from("listings").insert({ figure_id: "00000000-0000-0000-0000-000000000000", seller_id: a.id, price_shards: 1 }),
      b.db.from("friendships").insert({ requester_id: a.id, addressee_id: b.id, status: "accepted" }),
    ];
    for (const q of attempts) {
      const { error } = await q;
      expect(error?.message).toMatch(/permission denied/);
    }
    expect(await shards(a.id)).toBe(150);
    const { data: figs } = await admin.from("figures").select("owner_id").eq("owner_id", a.id);
    expect(figs!.length).toBeGreaterThan(0);
  });

  it("owner-only tables hide other players' rows", async () => {
    const { data: mine } = await a.db.from("transactions").select("id, user_id");
    expect(mine!.length).toBeGreaterThan(0);
    expect(mine!.every((t) => t.user_id === a.id)).toBe(true);
    const { data: theirs } = await b.db.from("transactions").select("id").eq("user_id", a.id);
    expect(theirs).toEqual([]);
  });

  it("server-only tables are unreadable by clients", async () => {
    for (const table of ["puzzle_keys", "figure_pool", "wallet_nonces"]) {
      const { error } = await a.db.from(table).select("*").limit(1);
      expect(error?.message).toMatch(/permission denied/);
    }
  });

  it("internal helpers and service-only RPCs are not executable by clients", async () => {
    expect(await rpcError(a.db, "_add_shards", { p_user: a.id, p_delta: 1000, p_kind: "hack" })).toMatch(/permission denied|Could not find/);
    expect(await rpcError(a.db, "enter_boss_sol", {
      p_user: a.id, p_boss_id: "00000000-0000-4000-8000-00000000b055", p_signature: "x", p_lamports: 1,
    })).toMatch(/permission denied|Could not find/);
  });
});

describe("contracts shared with @ronriku/core", () => {
  it("SQL spatial transition table equals the TS/C# orientation graph", async () => {
    for (let o = 0; o < 24; o++)
      for (let m = 0; m < 4; m++) expect(await rpc(admin, "_spatial_apply", { p_orientation: o, p_move: m })).toBe(applyMove(o, m));
  });

  it("daily_day matches dayNumber", async () => {
    expect(await rpc(anon(), "daily_day", { p_at: "2026-09-01T00:00:00Z" })).toBe(1);
    expect(await rpc(anon(), "daily_day", { p_at: "2026-09-24T23:59:59Z" })).toBe(24);
  });
});
