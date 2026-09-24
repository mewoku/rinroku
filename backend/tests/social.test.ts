import { describe, expect, it } from "vitest";
import { admin, anon, dailyOutcomes, giveProgress, newPlayer, rpc, rpcError, setShards, shards, today } from "./helpers.ts";

async function starterFigure(db: import("@supabase/supabase-js").SupabaseClient, owner: string): Promise<string> {
  const { data } = await db.from("figures").select("id").eq("owner_id", owner).limit(1).single();
  return data!.id;
}

describe("marketplace", () => {
  it("buy_listing transfers ownership and shards atomically", async () => {
    const seller = await newPlayer();
    const buyer = await newPlayer();
    const figure = await starterFigure(seller.db, seller.id);

    // Anti-Sybil: fresh accounts (no cleared level / solved Daily) cannot trade.
    expect(await rpcError(seller.db, "list_figure", { p_figure_id: figure, p_price_shards: 100 })).toMatch(/progress_required/);
    await giveProgress(seller.id);
    expect(await rpcError(seller.db, "list_figure", { p_figure_id: figure, p_price_shards: 100 })).toMatch(/figure_is_avatar/);
    // Unequip by moving the avatar pointer away (admin), then list.
    await admin.from("profiles").update({ avatar_figure_id: null }).eq("id", seller.id);
    expect(await rpcError(buyer.db, "list_figure", { p_figure_id: figure, p_price_shards: 100 })).toMatch(/progress_required/); // gate runs first
    const listing = await rpc(seller.db, "list_figure", { p_figure_id: figure, p_price_shards: 100 });
    expect(await rpcError(seller.db, "list_figure", { p_figure_id: figure, p_price_shards: 90 })).toMatch(/already_listed/);
    expect(await rpcError(seller.db, "buy_listing", { p_listing_id: listing.id })).toMatch(/own_listing/);
    const { data: pub } = await anon().from("listings").select("id, status").eq("id", listing.id).single();
    expect(pub!.status).toBe("active");

    expect(await rpcError(buyer.db, "buy_listing", { p_listing_id: listing.id })).toMatch(/progress_required/);
    await giveProgress(buyer.id);
    // Insufficient balance → nothing changes.
    await setShards(buyer.id, 99);
    expect(await rpcError(buyer.db, "buy_listing", { p_listing_id: listing.id })).toMatch(/insufficient_shards/);
    const { data: still } = await admin.from("figures").select("owner_id").eq("id", figure).single();
    expect(still!.owner_id).toBe(seller.id);
    expect(await shards(seller.id)).toBe(150);

    await setShards(buyer.id, 500);
    const r = await rpc(buyer.db, "buy_listing", { p_listing_id: listing.id });
    expect(r).toMatchObject({ figure_id: figure, price_shards: 100 });
    expect(await shards(buyer.id)).toBe(400);
    expect(await shards(seller.id)).toBe(250);
    const { data: moved } = await admin.from("figures").select("owner_id").eq("id", figure).single();
    expect(moved!.owner_id).toBe(buyer.id);
    const { data: sold } = await admin.from("listings").select("status, buyer_id").eq("id", listing.id).single();
    expect(sold).toMatchObject({ status: "sold", buyer_id: buyer.id });
    expect(await rpcError(buyer.db, "buy_listing", { p_listing_id: listing.id })).toMatch(/listing_not_active/);
  });

  it("cancel_listing is seller-only", async () => {
    const seller = await newPlayer();
    const other = await newPlayer();
    const figure = await starterFigure(seller.db, seller.id);
    await admin.from("profiles").update({ avatar_figure_id: null }).eq("id", seller.id);
    await giveProgress(seller.id);
    const listing = await rpc(seller.db, "list_figure", { p_figure_id: figure, p_price_shards: 10 });
    expect(await rpcError(other.db, "cancel_listing", { p_listing_id: listing.id })).toMatch(/listing_not_active/);
    expect(await rpc(seller.db, "cancel_listing", { p_listing_id: listing.id })).toMatchObject({ status: "cancelled" });
  });
});

describe("marketplace inflow cap", () => {
  it("a seller receives at most 2000 shards from sales per UTC day", async () => {
    const seller = await newPlayer();
    const buyer = await newPlayer();
    await giveProgress(seller.id);
    await giveProgress(buyer.id);
    await setShards(buyer.id, 10_000);
    const figure = await starterFigure(seller.db, seller.id);
    await admin.from("profiles").update({ avatar_figure_id: null }).eq("id", seller.id);
    const { data: copy } = await admin.from("figures").select("seed, tier, encoding, rarity, name").eq("id", figure).single();
    const { data: second } = await admin.from("figures").insert({ ...copy, owner_id: seller.id, source: "admin" }).select("id").single();

    const l1 = await rpc(seller.db, "list_figure", { p_figure_id: figure, p_price_shards: 1500 });
    const l2 = await rpc(seller.db, "list_figure", { p_figure_id: second!.id, p_price_shards: 600 });
    await rpc(buyer.db, "buy_listing", { p_listing_id: l1.id });
    expect(await rpcError(buyer.db, "buy_listing", { p_listing_id: l2.id })).toMatch(/seller_daily_cap/);
    expect(await shards(seller.id)).toBe(150 + 1500);
    expect(await shards(buyer.id)).toBe(10_000 - 1500);
  });
});

describe("friends", () => {
  it("request → accept flow, visibility and friends leaderboard", async () => {
    const a = await newPlayer();
    const b = await newPlayer();
    const c = await newPlayer();
    expect(await rpcError(a.db, "send_friend_request", { p_handle: a.handle })).toMatch(/cannot_friend_self/);
    expect(await rpcError(a.db, "send_friend_request", { p_handle: "nobody_here_zz" })).toMatch(/not_found/);

    expect(await rpc(a.db, "send_friend_request", { p_handle: b.handle.toUpperCase() })).toMatchObject({ status: "pending", user_id: b.id });
    expect(await rpc(a.db, "send_friend_request", { p_handle: b.handle })).toMatchObject({ status: "pending" });
    const incoming = await rpc(b.db, "list_friends");
    expect(incoming).toEqual([expect.objectContaining({ user_id: a.id, status: "pending", direction: "incoming" })]);
    const { data: hidden } = await c.db.from("friendships").select("*").eq("requester_id", a.id);
    expect(hidden).toEqual([]);

    expect(await rpcError(a.db, "respond_friend_request", { p_requester_id: a.id, p_action: "accept" })).toMatch(/no_pending_request/);
    expect(await rpc(b.db, "respond_friend_request", { p_requester_id: a.id, p_action: "accept" })).toMatchObject({ status: "accepted" });

    await admin.from("profiles").update({ rating: 1500 }).eq("id", b.id);
    const board = await rpc(a.db, "leaderboard", { p_scope: "friends", p_limit: 10 });
    expect(board.map((r: { user_id: string }) => r.user_id)).toEqual([b.id, a.id]);
    expect(board.map((r: { rank: number }) => r.rank)).toEqual([1, 2]);

    // Mutual request auto-accepts; decline removes; block hides the blocker.
    expect(await rpc(c.db, "send_friend_request", { p_handle: a.handle })).toMatchObject({ status: "pending" });
    expect(await rpc(a.db, "respond_friend_request", { p_requester_id: c.id, p_action: "block" })).toMatchObject({ status: "blocked" });
    expect(await rpcError(c.db, "send_friend_request", { p_handle: a.handle })).toMatch(/not_found/);
  });

  it("crossing requests sent at the same time end up accepted", async () => {
    const a = await newPlayer();
    const b = await newPlayer();
    const [ra, rb] = await Promise.all([
      a.db.rpc("send_friend_request", { p_handle: b.handle }),
      b.db.rpc("send_friend_request", { p_handle: a.handle }),
    ]);
    expect(ra.error).toBeNull();
    expect(rb.error).toBeNull();
    expect([ra.data.status, rb.data.status]).toContain("accepted");
    expect(await rpc(a.db, "list_friends")).toEqual([expect.objectContaining({ user_id: b.id, status: "accepted" })]);
  });
});

describe("leaderboards", () => {
  it("global is ordered by rating desc and ranked", async () => {
    const p = await newPlayer();
    // Earlier runs may have left top-rated players, so assert relative order rather than rank 1.
    await admin.from("profiles").update({ rating: 3000 }).eq("id", p.id);
    const q = await newPlayer();
    await admin.from("profiles").update({ rating: 3000 }).eq("id", q.id);
    const board = await rpc(anon(), "leaderboard", { p_scope: "global", p_limit: 200 });
    const row = (id: string) => board.find((r: { user_id: string }) => r.user_id === id);
    // Ties share a rank (rank()), and the top score is ranked 1.
    expect(row(p.id)).toMatchObject({ rank: 1, score: 3000 });
    expect(row(q.id)).toMatchObject({ rank: 1, score: 3000 });
    for (let i = 1; i < board.length; i++) {
      expect(board[i - 1].score).toBeGreaterThanOrEqual(board[i].score);
      expect(board[i].rank).toBe(board[i].score === board[i - 1].score ? board[i - 1].rank : i + 1);
    }
  });

  it("daily is ordered by points desc", async () => {
    const fast = await newPlayer();
    const slow = await newPlayer();
    await rpc(slow.db, "submit_daily", { p_day: today(), p_outcomes: dailyOutcomes(today(), 50_000) });
    await rpc(fast.db, "submit_daily", { p_day: today(), p_outcomes: dailyOutcomes(today(), 2_000) });
    const board = await rpc(anon(), "leaderboard", { p_scope: "daily", p_limit: 200 });
    const ids = board.map((r: { user_id: string }) => r.user_id);
    expect(ids.indexOf(fast.id)).toBeLessThan(ids.indexOf(slow.id));
    for (let i = 1; i < board.length; i++) expect(board[i - 1].score).toBeGreaterThanOrEqual(board[i].score);
    expect(await rpcError(anon(), "leaderboard", { p_scope: "weekly" })).toMatch(/invalid_scope/);
    expect(await rpcError(anon(), "leaderboard", { p_scope: "daily", p_ref: "abc" })).toMatch(/invalid_ref/);
    expect(await rpcError(anon(), "leaderboard", { p_scope: "boss", p_ref: "not-a-uuid" })).toMatch(/invalid_ref/);
  });
});
