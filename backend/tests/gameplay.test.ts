import { dailyPlan, levelDef } from "@ronriku/core";
import { describe, expect, it } from "vitest";
import { admin, answerFor, dailyOutcomes, newPlayer, rpc, rpcError, setShards, shards, today, type Player } from "./helpers.ts";

const proof = (world: number, level: number) => ({ answers: levelDef(world, level).stages.map(answerFor) });
const SEED_BOSS = "00000000-0000-4000-8000-00000000b055";

async function bossAnswers(db: Player["db"], bossId = SEED_BOSS) {
  const { data: boss } = await db.from("boss_events").select("stages").eq("id", bossId).single();
  return (boss!.stages as { kind: "Pattern" | "Spatial" | "Logic"; seed: string }[])
    .map((s) => answerFor({ kind: s.kind, seed: BigInt(s.seed), difficulty: "Standard" }));
}

describe("client payload contract (exact Unity shapes)", () => {
  it("submit_daily accepts {kind, elapsed_ms, resets, moves, answer} with C# enum-name kinds", async () => {
    const p = await newPlayer();
    const [pat, spa, log] = dailyPlan(today()).trials.map(answerFor) as [number, number[], number[]];
    const p_outcomes = [
      { kind: "Pattern", elapsed_ms: 12345, resets: 0, moves: 1, answer: pat },
      { kind: "Spatial", elapsed_ms: 23456, resets: 1, moves: spa.length, answer: spa },
      { kind: "Logic", elapsed_ms: 34567, resets: 0, moves: log.length - 1, answer: log },
    ];
    const r = await rpc(p.db, "submit_daily", { p_day: today(), p_outcomes });
    expect(r).toMatchObject({ counted: true, solved: 3 });
    expect(r.outcomes[2]).toMatchObject({ kind: "Logic", moves: 24, par: 24, points: 1300 + Math.trunc((60000 - 34567) / 100) });
    expect(r.outcomes[1]).toMatchObject({ resets: 1 });
  });

  it("complete_level accepts {answers, moves, resets} and finish_boss {answers, moves, resets, elapsed_ms}", async () => {
    const p = await newPlayer();
    const a = levelDef(0, 0).stages.map(answerFor);
    expect(await rpc(p.db, "complete_level", {
      p_world: 0, p_level: 0, p_stars: 3, p_elapsed_ms: 5000, p_proof: { answers: a, moves: [1], resets: [0] },
    })).toMatchObject({ stars: 3 });
    await setShards(p.id, 1000);
    const attempt = await rpc(p.db, "enter_boss", { p_boss_id: SEED_BOSS, p_pay_with: "shards" });
    const answers = await bossAnswers(p.db);
    const moves = answers.map((x) => (Array.isArray(x) ? x.length : 1));
    expect(await rpc(p.db, "finish_boss", {
      p_attempt_id: attempt.id, p_result: { answers, moves, resets: [0, 0, 0], elapsed_ms: 123456 },
    })).toMatchObject({ result: "won" });
  });

  it("submit_daily compares kind case-insensitively", async () => {
    const p = await newPlayer();
    const outcomes = dailyOutcomes(today()).map((o) => ({ ...o, kind: o.kind.toLowerCase() }));
    expect(await rpc(p.db, "submit_daily", { p_day: today(), p_outcomes: outcomes })).toMatchObject({ counted: true, solved: 3 });
  });
});

describe("complete_level", () => {
  it("awards shards once, recomputes stars, and is idempotent", async () => {
    const p = await newPlayer();
    const first = await rpc(p.db, "complete_level", { p_world: 0, p_level: 0, p_stars: 3, p_elapsed_ms: 5000, p_proof: proof(0, 0) });
    expect(first).toMatchObject({ stars: 3, earned: 40, first_clear: true });
    expect(await shards(p.id)).toBe(190);
    const again = await rpc(p.db, "complete_level", { p_world: 0, p_level: 0, p_stars: 3, p_elapsed_ms: 4000, p_proof: proof(0, 0) });
    expect(again).toMatchObject({ earned: 0, first_clear: false, best_ms: 4000 });
    expect(await shards(p.id)).toBe(190);
  });

  it("concurrent first clears pay once (profile lock serialises the race)", async () => {
    const p = await newPlayer();
    const args = { p_world: 0, p_level: 0, p_stars: 3, p_elapsed_ms: 5000, p_proof: proof(0, 0) };
    const results = await Promise.all([p.db.rpc("complete_level", args), p.db.rpc("complete_level", args), p.db.rpc("complete_level", args)]);
    expect(results.every((r) => !r.error)).toBe(true);
    expect(results.filter((r) => r.data.first_clear)).toHaveLength(1);
    expect(await shards(p.id)).toBe(190);
  });

  it("caps stars by the client claim, reported Spatial moves over par cost the clean star", async () => {
    const p = await newPlayer();
    expect(await rpc(p.db, "complete_level", { p_world: 0, p_level: 0, p_stars: 1, p_elapsed_ms: 5000, p_proof: proof(0, 0) }))
      .toMatchObject({ stars: 1, earned: 20 });
    expect(await rpc(p.db, "complete_level", { p_world: 0, p_level: 0, p_stars: 3, p_elapsed_ms: 5000, p_proof: proof(0, 0) }))
      .toMatchObject({ stars: 3, earned: 20 });
    // Level 0-1 is Spatial: over the target time → at most 2 stars; 99 reported moves → 1 star.
    expect(levelDef(0, 1).kind).toBe("Spatial");
    expect(await rpc(p.db, "complete_level", { p_world: 0, p_level: 1, p_stars: 3, p_elapsed_ms: 600_000, p_proof: proof(0, 1) }))
      .toMatchObject({ run_stars: 2, earned: 30 });
    expect(await rpc(p.db, "complete_level", {
      p_world: 0, p_level: 1, p_stars: 3, p_elapsed_ms: 5000, p_proof: { ...proof(0, 1), moves: [99] },
    })).toMatchObject({ run_stars: 1, earned: 0 });
  });

  it("rejects wrong answers, locked levels and bad input", async () => {
    const p = await newPlayer();
    const wrong = { answers: [(Number(answerFor(levelDef(0, 0).stages[0])) + 1) % 4] };
    expect(await rpcError(p.db, "complete_level", { p_world: 0, p_level: 0, p_stars: 3, p_elapsed_ms: 5000, p_proof: wrong })).toMatch(/not_solved/);
    expect(await rpcError(p.db, "complete_level", { p_world: 0, p_level: 5, p_stars: 3, p_elapsed_ms: 5000, p_proof: proof(0, 5) })).toMatch(/level_locked/);
    expect(await rpcError(p.db, "complete_level", { p_world: 9, p_level: 0, p_stars: 3, p_elapsed_ms: 5000, p_proof: proof(0, 0) })).toMatch(/invalid_level/);
    expect(await rpcError(p.db, "complete_level", { p_world: 0, p_level: 0, p_stars: 3, p_elapsed_ms: 10, p_proof: proof(0, 0) })).toMatch(/invalid_elapsed/);
    expect(await shards(p.id)).toBe(150);
  });

  it("world boss (index 11) takes three stage answers and pays 300+", async () => {
    const p = await newPlayer();
    const rows = Array.from({ length: 11 }, (_, level) => ({ user_id: p.id, world: 0, level, stars: 1, best_ms: 9000 }));
    const { error } = await admin.from("level_progress").insert(rows);
    expect(error).toBeNull();
    const r = await rpc(p.db, "complete_level", { p_world: 0, p_level: 11, p_stars: 1, p_elapsed_ms: 60_000, p_proof: proof(0, 11) });
    expect(r).toMatchObject({ earned: 300, first_clear: true });
    expect(await rpc(p.db, "complete_level", { p_world: 1, p_level: 0, p_stars: 1, p_elapsed_ms: 9000, p_proof: proof(1, 0) }))
      .toMatchObject({ earned: 20 });
  });
});

describe("submit_daily", () => {
  it("scores server-side, pays 100 + 10·streak once, and replays are not counted", async () => {
    const p = await newPlayer();
    const r = await rpc(p.db, "submit_daily", { p_day: today(), p_outcomes: dailyOutcomes(today()) });
    expect(r).toMatchObject({ counted: true, solved: 3, streak: 1, shards_earned: 110, rating_before: 1200 });
    expect(r.rating_after).toBeGreaterThan(1200);
    expect(await shards(p.id)).toBe(260);
    const again = await rpc(p.db, "submit_daily", { p_day: today(), p_outcomes: dailyOutcomes(today(), 1000) });
    expect(again).toMatchObject({ counted: false, shards_earned: 0, points: r.points });
    expect(await shards(p.id)).toBe(260);
  });

  it("floors client-reported moves and clamps resets (misreporting only lowers the score)", async () => {
    const p = await newPlayer();
    const o = dailyOutcomes(today());
    o[1].moves = 0; // under-reporting Spatial moves is floored to the answer length
    o[2].moves = 27; // Logic: 3 steps over par 24 → −45
    o[0].resets = 999; // clamped to 50
    const r = await rpc(p.db, "submit_daily", { p_day: today(), p_outcomes: o });
    expect(r.outcomes[1].moves).toBe((o[1].answer as number[]).length);
    expect(r.outcomes[2]).toMatchObject({ moves: 27, par: 24, points: 1300 + (60000 - 20000) / 100 - 45 });
    expect(r.outcomes[0]).toMatchObject({ resets: 50, points: 100 });
  });

  it("pays no shards when nothing is solved (anti-faucet) and treats wrong answers as unsolved", async () => {
    const p = await newPlayer();
    const r = await rpc(p.db, "submit_daily", { p_day: today(), p_outcomes: dailyOutcomes(today(), 20_000, [false, false, false]) });
    expect(r).toMatchObject({ counted: true, solved: 0, shards_earned: 0 });
    expect(await shards(p.id)).toBe(150);

    const q = await newPlayer();
    const outcomes = dailyOutcomes(today(), 20_000, [true, false, true]);
    outcomes[1].answer = [0, 0, 0, 1];
    expect((await rpc(q.db, "submit_daily", { p_day: today(), p_outcomes: outcomes })).solved).toBe(2);
    expect(await rpcError(q.db, "submit_daily", { p_day: today() + 1, p_outcomes: dailyOutcomes(today() + 1) })).toMatch(/wrong_day/);
  });
});

describe("shop", () => {
  it("buy_figure_shards debits the price, grants the figure once, and fails on insufficient balance", async () => {
    const p = await newPlayer();
    const { data: shelf } = await p.db.from("shop_shelf").select("*").eq("day", today()).not("price_shards", "is", null).order("slot");
    expect(shelf!.length).toBeGreaterThan(0);
    const item = shelf![0];

    await setShards(p.id, item.price_shards - 1);
    expect(await rpcError(p.db, "buy_figure_shards", { p_item: item.item_id })).toMatch(/insufficient_shards/);
    expect(await shards(p.id)).toBe(item.price_shards - 1);

    await setShards(p.id, 5000);
    const fig = await rpc(p.db, "buy_figure_shards", { p_item: item.item_id });
    expect(fig).toMatchObject({ owner_id: p.id, encoding: item.encoding, tier: item.tier, rarity: item.rarity, source: "shop" });
    expect(await shards(p.id)).toBe(5000 - item.price_shards);
    expect(await rpcError(p.db, "buy_figure_shards", { p_item: item.item_id })).toMatch(/already_owned/);
    expect(await shards(p.id)).toBe(5000 - item.price_shards);
    expect(await rpcError(p.db, "buy_figure_shards", { p_item: "fig-1-3" })).toMatch(/unknown_item/);
  });
});

describe("bosses", () => {
  it("charges once per open attempt; pays reward_shards only on the FIRST win (no farming)", async () => {
    const p = await newPlayer();
    await setShards(p.id, 1000);
    const attempt = await rpc(p.db, "enter_boss", { p_boss_id: SEED_BOSS, p_pay_with: "shards" });
    expect((await rpc(p.db, "enter_boss", { p_boss_id: SEED_BOSS })).id).toBe(attempt.id);
    expect(await shards(p.id)).toBe(850);
    expect(await rpcError(p.db, "enter_boss", { p_boss_id: SEED_BOSS, p_pay_with: "sol" })).toMatch(/sol_entry_requires_server/);

    const answers = await bossAnswers(p.db);
    expect(await rpc(p.db, "finish_boss", { p_attempt_id: attempt.id, p_result: { answers, elapsed_ms: 90_000 } }))
      .toMatchObject({ result: "won", earned: 450, first_win: true });
    expect(await shards(p.id)).toBe(1300);
    expect(await rpc(p.db, "finish_boss", { p_attempt_id: attempt.id, p_result: { answers, elapsed_ms: 1000 } }))
      .toMatchObject({ already_finished: true, earned: 0 });

    // Second win on the same boss: recorded, but pays nothing (entry is a pure cost).
    const second = await rpc(p.db, "enter_boss", { p_boss_id: SEED_BOSS });
    expect(await rpc(p.db, "finish_boss", { p_attempt_id: second.id, p_result: { answers, elapsed_ms: 80_000 } }))
      .toMatchObject({ result: "won", earned: 0, first_win: false });
    expect(await shards(p.id)).toBe(1150);

    const lost = await rpc(p.db, "enter_boss", { p_boss_id: SEED_BOSS });
    expect(await rpc(p.db, "finish_boss", { p_attempt_id: lost.id, p_result: { answers: [null, null, null], elapsed_ms: 5000 } }))
      .toMatchObject({ result: "lost", earned: 0 });
  });

  it("ranks on server time: a forged elapsed_ms cannot beat a real run; stale attempts cannot win", async () => {
    const honest = await newPlayer();
    const forger = await newPlayer();
    const stale = await newPlayer();
    for (const p of [honest, forger, stale]) await setShards(p.id, 1000);
    const answers = await bossAnswers(honest.db);

    const fa = await rpc(forger.db, "enter_boss", { p_boss_id: SEED_BOSS });
    await admin.from("boss_attempts").update({ created_at: new Date(Date.now() - 60_000).toISOString() }).eq("id", fa.id);
    const forged = await rpc(forger.db, "finish_boss", { p_attempt_id: fa.id, p_result: { answers, elapsed_ms: 1000 } });
    expect(forged.result).toBe("won");
    expect(forged.elapsed_ms).toBeGreaterThanOrEqual(54_000);

    const ha = await rpc(honest.db, "enter_boss", { p_boss_id: SEED_BOSS });
    await admin.from("boss_attempts").update({ created_at: new Date(Date.now() - 20_000).toISOString() }).eq("id", ha.id);
    const real = await rpc(honest.db, "finish_boss", { p_attempt_id: ha.id, p_result: { answers, elapsed_ms: 20_000 } });
    expect(real.elapsed_ms).toBeLessThan(forged.elapsed_ms);

    const board = await rpc(honest.db, "leaderboard", { p_scope: "boss", p_limit: 200, p_ref: SEED_BOSS });
    const pos = (id: string) => board.findIndex((r: { user_id: string }) => r.user_id === id);
    expect(pos(honest.id)).toBeGreaterThanOrEqual(0);
    expect(pos(honest.id)).toBeLessThan(pos(forger.id));

    const sa = await rpc(stale.db, "enter_boss", { p_boss_id: SEED_BOSS });
    await admin.from("boss_attempts").update({ created_at: new Date(Date.now() - 400_000).toISOString() }).eq("id", sa.id);
    expect(await rpc(stale.db, "finish_boss", { p_attempt_id: sa.id, p_result: { answers, elapsed_ms: 60_000 } }))
      .toMatchObject({ result: "lost", earned: 0 });
  });

  it("enter_boss checks puzzle keys before charging", async () => {
    const p = await newPlayer();
    const { data: ev, error } = await admin.from("boss_events").insert({
      name: "UNPUBLISHED", seed: 1, stages: [{ kind: "Pattern", seed: "1" }],
      starts_at: new Date(Date.now() - 1000).toISOString(), ends_at: new Date(Date.now() + 3_600_000).toISOString(),
    }).select("id").single();
    expect(error).toBeNull();
    try {
      expect(await rpcError(p.db, "enter_boss", { p_boss_id: ev!.id })).toMatch(/puzzle_not_published/);
      expect(await shards(p.id)).toBe(150);
    } finally {
      await admin.from("boss_events").delete().eq("id", ev!.id);
    }
  });

  it("enter_boss_sol (service role) validates payment and is idempotent", async () => {
    const p = await newPlayer();
    const sig = "5".repeat(64) + Date.now();
    expect(await rpcError(admin, "enter_boss_sol", { p_user: p.id, p_boss_id: SEED_BOSS, p_signature: sig, p_lamports: null })).toMatch(/underpaid/);
    expect(await rpcError(admin, "enter_boss_sol", { p_user: p.id, p_boss_id: SEED_BOSS, p_signature: sig, p_lamports: 1 })).toMatch(/underpaid/);
    expect(await rpcError(admin, "enter_boss_sol", {
      p_user: "00000000-0000-4000-8000-000000000000", p_boss_id: SEED_BOSS, p_signature: sig, p_lamports: 10_000_000,
    })).toMatch(/profile_missing/);
    const a = await rpc(admin, "enter_boss_sol", { p_user: p.id, p_boss_id: SEED_BOSS, p_signature: sig, p_lamports: 10_000_000 });
    expect(a).toMatchObject({ paid_with: "sol", result: "open" });
    const b = await rpc(admin, "enter_boss_sol", { p_user: p.id, p_boss_id: SEED_BOSS, p_signature: sig + "x", p_lamports: 10_000_000 });
    expect(b.id).toBe(a.id);
    expect(await shards(p.id)).toBe(150);
  });

  it("publisher upserted this week's roster from C# BossEvent.ForWeek", async () => {
    const { data } = await admin.from("boss_events").select("code, tier, reward_shards, entry_shards").not("code", "is", null);
    expect(data!.length).toBeGreaterThanOrEqual(3);
    for (const b of data!) expect(b).toMatchObject({ entry_shards: 150 + 50 * b.tier, reward_shards: 450 + 250 * b.tier });
  });
});
