import { beforeEach, describe, expect, it } from "vitest";
import {
  KIND,
  REFUND_KIND,
  PurchaseError,
  processSolPurchase,
  quote,
  type ChainDeps,
  type Claim,
  type FigureRow,
  type PurchaseBody,
  type PurchaseConfig,
  type PurchaseStore,
  type ShelfSlot,
} from "./purchase";
import { MINT_FEE_LAMPORTS } from "../economy";

// ------------------------------------------------------------------ in-memory fakes
const TREASURY = "Treasury1111111111111111111111111111111111";
const ALICE = "11111111-1111-4111-8111-111111111111";
const BOB = "22222222-2222-4222-8222-222222222222";
const ALICE_WALLET = "AliceWallet111111111111111111111111111111";
const BOB_WALLET = "BobWallet1111111111111111111111111111111111";
const BOSS = "33333333-3333-4333-8333-333333333333";
const FIG = "44444444-4444-4444-8444-444444444444";
const FIG2 = "55555555-5555-4555-8555-555555555555";
const TODAY = 24;
const LEG_PRICE = 100_000_000;
const sig = (n: number) => `${n}`.padStart(8, "9") + "S".repeat(80);

interface Payment {
  from: string;
  lamports: number;
}

class FakeStore implements PurchaseStore {
  wallets = new Map<string, string | null>([
    [ALICE, ALICE_WALLET],
    [BOB, BOB_WALLET],
  ]);
  claims = new Map<string, Claim>();
  shelf: ShelfSlot[] = [
    { day: TODAY, slot: 5, itemId: "fig-99-5", seed: "99", tier: 5, rarity: "Legendary", name: "LEGGO", encoding: "RF1.x", priceLamports: LEG_PRICE },
    { day: TODAY, slot: 0, itemId: "fig-1-3", seed: "1", tier: 3, rarity: "Rare", name: "RARO", encoding: "RF1.y", priceLamports: null },
  ];
  figures = new Map<string, FigureRow & { seed?: string; tier?: number }>([
    [FIG, { id: FIG, name: "MINTME", ownerId: ALICE, mintAddress: null }],
    [FIG2, { id: FIG2, name: "BOBS", ownerId: BOB, mintAddress: null }],
  ]);
  listings = new Set<string>();
  bosses = new Map([[BOSS, { id: BOSS, entryLamports: 10_000_000, startsAt: "2026-09-20T00:00:00Z", endsAt: "2026-09-30T00:00:00Z", stages: 3 }]]);
  keysPublished = true;
  attempts = new Map<string, { id: string; bossId: string; userId: string; open: boolean }>();
  enterBossFailure: string | null = null;
  private n = 0;

  async linkedWallet(u: string) {
    return this.wallets.get(u) ?? null;
  }
  async claimBySignature(s: string) {
    return this.claims.get(s) ?? null;
  }
  async insertClaim(s: string, c: Claim) {
    if (this.claims.has(s)) return "duplicate" as const;
    this.claims.set(s, c);
    return "ok" as const;
  }
  async shelfSlot(day: number, slot: number) {
    return this.shelf.find((x) => x.day === day && x.slot === slot) ?? null;
  }
  async findOwnedFigure(u: string, seed: string, tier: number) {
    return [...this.figures.values()].find((f) => f.ownerId === u && f.seed === seed && f.tier === tier) ?? null;
  }
  async insertFigure(r: { userId: string; seed: string; tier: number; name: string }) {
    const id = `00000000-0000-4000-8000-${String(++this.n).padStart(12, "0")}`;
    const row = { id, name: r.name, ownerId: r.userId, mintAddress: null, seed: r.seed, tier: r.tier };
    this.figures.set(id, row);
    return row;
  }
  async figure(id: string) {
    return this.figures.get(id) ?? null;
  }
  async hasActiveListing(id: string) {
    return this.listings.has(id);
  }
  async boss(id: string) {
    return this.bosses.get(id) ?? null;
  }
  async bossKeysPublished() {
    return this.keysPublished;
  }
  async hasOpenAttempt(u: string, b: string) {
    return [...this.attempts.values()].some((a) => a.userId === u && a.bossId === b && a.open);
  }
  async attempt(id: string) {
    return this.attempts.get(id) ?? null;
  }
  // Mirrors enter_boss_sol: attempt + ledger atomically; refuses an open attempt or a used signature.
  async enterBossSol(u: string, b: string, s: string, lamports: number) {
    if (this.enterBossFailure) return { ok: false as const, error: this.enterBossFailure };
    if (this.claims.has(s)) return { ok: false as const, error: "signature_used" };
    if (await this.hasOpenAttempt(u, b)) return { ok: false as const, error: "attempt_open" };
    const id = `att-${++this.n}`;
    this.attempts.set(id, { id, bossId: b, userId: u, open: true });
    this.claims.set(s, { userId: u, kind: "boss_entry", refId: id, lamports });
    return { ok: true as const, attemptId: id };
  }
  async reserveMint(id: string, address: string) {
    const f = this.figures.get(id);
    if (!f || (f.mintAddress && f.mintAddress !== address)) return false;
    f.mintAddress = address;
    return true;
  }
}

class FakeChain implements ChainDeps {
  payments = new Map<string, Payment>();
  onChain = new Set<string>();
  mintCalls = 0;
  failAfterLanding = false;
  failBeforeLanding = false;
  verifyCalls: { signature: string; buyer: string; minLamports: number }[] = [];

  async verifyTransfer(signature: string, e: { buyer: string; treasury: string; minLamports: number }) {
    this.verifyCalls.push({ signature, buyer: e.buyer, minLamports: e.minLamports });
    const p = this.payments.get(signature);
    if (!p || e.treasury !== TREASURY) return { ok: false as const, reason: "not found" };
    if (p.from !== e.buyer) return { ok: false as const, reason: "wrong payer" };
    if (p.lamports < e.minLamports) return { ok: false as const, reason: "too small" };
    return { ok: true as const, lamports: p.lamports };
  }
  assetAddress(figureId: string) {
    return `asset-${figureId}`;
  }
  async assetExists(a: string) {
    return this.onChain.has(a);
  }
  async mint({ figureId }: { figureId: string }) {
    this.mintCalls++;
    const a = this.assetAddress(figureId);
    if (this.failBeforeLanding) throw new Error("rpc down");
    if (this.onChain.has(a)) throw new Error("account already in use"); // Core create on an existing asset
    this.onChain.add(a);
    if (this.failAfterLanding) throw new Error("confirmation timeout");
    return a;
  }
}

const cfg: PurchaseConfig = { treasury: TREASURY, siteUrl: "http://x", today: TODAY, now: new Date("2026-09-24T12:00:00Z") };
let store: FakeStore;
let chain: FakeChain;

beforeEach(() => {
  store = new FakeStore();
  chain = new FakeChain();
});

async function expectCode(p: Promise<unknown>, code: string) {
  const e = await p.then(
    () => null,
    (x: unknown) => x,
  );
  expect(e).toBeInstanceOf(PurchaseError);
  expect((e as PurchaseError).code).toBe(code);
  return e as PurchaseError;
}

const legendary = (s: string): PurchaseBody => ({ item: "figure-legendary", signature: s, day: TODAY, slot: 5 });
const mintFee = (s: string, figureId = FIG): PurchaseBody => ({ item: "mint-fee", signature: s, figureId });
const boss = (s: string): PurchaseBody => ({ item: "boss-entry", signature: s, bossId: BOSS });
const run = (u: string, b: PurchaseBody) => processSolPurchase(store, chain, cfg, u, b);

// ------------------------------------------------------------------ tests
describe("happy paths", () => {
  it("legendary: verifies the linked wallet paid the shelf price, claims, creates and mints once", async () => {
    chain.payments.set(sig(1), { from: ALICE_WALLET, lamports: LEG_PRICE });
    const out = await run(ALICE, legendary(sig(1)));
    expect(out.mintAddress).toBe(`asset-${out.figureId}`);
    expect(chain.mintCalls).toBe(1);
    expect(chain.verifyCalls[0]).toMatchObject({ buyer: ALICE_WALLET, minLamports: LEG_PRICE });
    expect(store.claims.get(sig(1))).toEqual({ userId: ALICE, kind: KIND["figure-legendary"], refId: `legendary:${TODAY}:5`, lamports: LEG_PRICE });
  });

  it("mint-fee: mints the owned figure", async () => {
    chain.payments.set(sig(2), { from: ALICE_WALLET, lamports: MINT_FEE_LAMPORTS });
    const out = await run(ALICE, mintFee(sig(2)));
    expect(out).toMatchObject({ figureId: FIG, mintAddress: `asset-${FIG}` });
    expect(store.figures.get(FIG)!.mintAddress).toBe(`asset-${FIG}`);
  });

  it("boss-entry: opens an attempt with the ledger row written atomically", async () => {
    chain.payments.set(sig(3), { from: ALICE_WALLET, lamports: 10_000_000 });
    const out = await run(ALICE, boss(sig(3)));
    expect(out.attemptId).toBeTruthy();
    expect(store.claims.get(sig(3))).toMatchObject({ userId: ALICE, kind: "boss_entry", refId: out.attemptId });
  });
});

describe("C1: a paid signature is bound to user + kind + ref", () => {
  beforeEach(async () => {
    chain.payments.set(sig(10), { from: ALICE_WALLET, lamports: MINT_FEE_LAMPORTS });
    await run(ALICE, mintFee(sig(10)));
  });

  it("cannot be reused for a more expensive item", async () => {
    await expectCode(run(ALICE, legendary(sig(10))), "claim_mismatch");
  });
  it("cannot be reused for another figure", async () => {
    store.figures.set(FIG2, { id: FIG2, name: "OTHER", ownerId: ALICE, mintAddress: null });
    await expectCode(run(ALICE, mintFee(sig(10), FIG2)), "claim_mismatch");
    expect(store.figures.get(FIG2)!.mintAddress).toBeNull();
  });
  it("cannot be reused for a boss entry", async () => {
    await expectCode(run(ALICE, boss(sig(10))), "claim_mismatch");
  });
  it("cannot be claimed by another user", async () => {
    await expectCode(run(BOB, mintFee(sig(10), FIG2)), "signature_used");
  });
  it("a same-item retry only resumes (no second mint)", async () => {
    const again = await run(ALICE, mintFee(sig(10)));
    expect(again).toMatchObject({ figureId: FIG, mintAddress: `asset-${FIG}`, resumed: true });
    expect(chain.mintCalls).toBe(1);
  });
  it("a boss signature cannot be replayed for another raid attempt or item", async () => {
    chain.payments.set(sig(11), { from: ALICE_WALLET, lamports: 10_000_000 });
    const first = await run(ALICE, boss(sig(11)));
    const again = await run(ALICE, boss(sig(11)));
    expect(again).toMatchObject({ attemptId: first.attemptId, resumed: true });
    await expectCode(run(ALICE, legendary(sig(11))), "claim_mismatch");
  });
});

describe("C2: the buyer is the caller's linked wallet", () => {
  it("rejects a victim's transfer claimed by someone else", async () => {
    chain.payments.set(sig(20), { from: BOB_WALLET, lamports: LEG_PRICE }); // Bob paid
    await expectCode(run(ALICE, legendary(sig(20))), "payment_invalid"); // Alice tries to claim it
    expect(store.claims.has(sig(20))).toBe(false);
    const out = await run(BOB, legendary(sig(20))); // Bob can still claim his own payment
    expect(out.mintAddress).toBeTruthy();
  });
  it("requires a linked wallet (before any chain call)", async () => {
    store.wallets.set(ALICE, null);
    await expectCode(run(ALICE, legendary(sig(21))), "wallet_not_linked");
    await expectCode(quote(store, cfg, ALICE, legendary(sig(21))), "wallet_not_linked");
    expect(chain.verifyCalls).toHaveLength(0);
  });
  it("rejects underpayment of the shelf price (M4)", async () => {
    chain.payments.set(sig(22), { from: ALICE_WALLET, lamports: LEG_PRICE - 1 });
    await expectCode(run(ALICE, legendary(sig(22))), "payment_invalid");
  });
});

describe("H1: boss entry never loses a payment", () => {
  it("pre-checks an open attempt before payment", async () => {
    store.attempts.set("open", { id: "open", bossId: BOSS, userId: ALICE, open: true });
    await expectCode(quote(store, cfg, ALICE, { item: "boss-entry", bossId: BOSS }), "attempt_open");
  });
  it("pre-checks inactive raids and unpublished keys", async () => {
    await expectCode(quote(store, { ...cfg, now: new Date("2026-10-05T00:00:00Z") }, ALICE, { item: "boss-entry", bossId: BOSS }), "boss_not_active");
    store.keysPublished = false;
    await expectCode(quote(store, cfg, ALICE, { item: "boss-entry", bossId: BOSS }), "puzzle_not_published");
  });
  it("records refund_due when the attempt opened between quote and payment", async () => {
    chain.payments.set(sig(30), { from: ALICE_WALLET, lamports: 10_000_000 });
    store.attempts.set("open", { id: "open", bossId: BOSS, userId: ALICE, open: true });
    await expectCode(run(ALICE, boss(sig(30))), "refund_due");
    expect(store.claims.get(sig(30))).toMatchObject({ userId: ALICE, kind: REFUND_KIND, lamports: 10_000_000 });
    await expectCode(run(ALICE, boss(sig(30))), "refund_due"); // not claimable afterwards
  });
  it("records refund_due when the RPC fails after payment", async () => {
    chain.payments.set(sig(31), { from: ALICE_WALLET, lamports: 10_000_000 });
    store.enterBossFailure = "boss_not_active";
    await expectCode(run(ALICE, boss(sig(31))), "refund_due");
    expect(store.claims.get(sig(31))!.kind).toBe(REFUND_KIND);
  });
  it("does not record anything when there was no valid payment", async () => {
    store.attempts.set("open", { id: "open", bossId: BOSS, userId: ALICE, open: true });
    await expectCode(run(ALICE, boss(sig(32))), "attempt_open");
    expect(store.claims.size).toBe(0);
  });
});

describe("H2: no double mint", () => {
  it("mint landed but confirmation failed → retry detects the asset and does not mint again", async () => {
    chain.payments.set(sig(40), { from: ALICE_WALLET, lamports: MINT_FEE_LAMPORTS });
    chain.failAfterLanding = true;
    const out = await run(ALICE, mintFee(sig(40))); // catch path sees the asset on-chain
    expect(out.mintAddress).toBe(`asset-${FIG}`);
    chain.failAfterLanding = false;
    await run(ALICE, mintFee(sig(40)));
    expect(chain.mintCalls).toBe(1);
  });
  it("mint failed before landing → error, then the retry mints exactly once at the reserved address", async () => {
    chain.payments.set(sig(41), { from: ALICE_WALLET, lamports: LEG_PRICE });
    chain.failBeforeLanding = true;
    await expectCode(run(ALICE, legendary(sig(41))), "mint_failed");
    const reserved = [...store.figures.values()].find((f) => f.seed === "99")!;
    expect(reserved.mintAddress).toBe(`asset-${reserved.id}`); // persisted before send
    chain.failBeforeLanding = false;
    const out = await run(ALICE, legendary(sig(41)));
    expect(out).toMatchObject({ figureId: reserved.id, mintAddress: `asset-${reserved.id}`, resumed: true });
    expect([...store.figures.values()].filter((f) => f.seed === "99")).toHaveLength(1); // one figure row
    expect(chain.onChain.size).toBe(1);
  });
  it("a figure reserved for another address is not re-minted", async () => {
    store.figures.get(FIG)!.mintAddress = "someone-else";
    chain.payments.set(sig(42), { from: ALICE_WALLET, lamports: MINT_FEE_LAMPORTS });
    await expectCode(quote(store, cfg, ALICE, { item: "mint-fee", figureId: FIG }), "already_minted");
    await expectCode(run(ALICE, mintFee(sig(42))), "refund_due"); // paid anyway → refund, no mint
    expect(chain.mintCalls).toBe(0);
  });
});

describe("H3: buyers are never left with nothing", () => {
  it("the earlier-claim lookup runs before shelf checks (resume after the shelf rotated)", async () => {
    chain.payments.set(sig(50), { from: ALICE_WALLET, lamports: LEG_PRICE });
    chain.failBeforeLanding = true;
    await expectCode(run(ALICE, legendary(sig(50))), "mint_failed");
    chain.failBeforeLanding = false;
    const later = { ...cfg, today: TODAY + 3 }; // shelf rotated; quote would say shelf_rotated
    const out = await processSolPurchase(store, chain, later, ALICE, legendary(sig(50)));
    expect(out.mintAddress).toBeTruthy();
  });
  it("'already own this Legendary' is caught by the pre-payment quote", async () => {
    chain.payments.set(sig(51), { from: ALICE_WALLET, lamports: LEG_PRICE });
    await run(ALICE, legendary(sig(51)));
    await expectCode(quote(store, cfg, ALICE, { item: "figure-legendary", day: TODAY, slot: 5 }), "already_owned");
  });
  it("a verified payment whose pre-check fails after paying is recorded as refund_due", async () => {
    chain.payments.set(sig(52), { from: ALICE_WALLET, lamports: LEG_PRICE });
    chain.payments.set(sig(53), { from: ALICE_WALLET, lamports: LEG_PRICE });
    await run(ALICE, legendary(sig(52)));
    await expectCode(run(ALICE, legendary(sig(53))), "refund_due");
    expect(store.claims.get(sig(53))!.kind).toBe(REFUND_KIND);
  });
  it("rejects non-SOL shelf items and rotated shelves before payment", async () => {
    await expectCode(quote(store, cfg, ALICE, { item: "figure-legendary", day: TODAY, slot: 0 }), "not_sol_item");
    await expectCode(quote(store, cfg, ALICE, { item: "figure-legendary", day: TODAY - 2, slot: 5 }), "shelf_rotated");
    await expectCode(quote(store, cfg, ALICE, { item: "figure-legendary", day: TODAY, slot: 3 }), "shelf_not_published");
  });
});

describe("M5: mint-fee pre-checks", () => {
  it("rejects a figure with an active listing", async () => {
    store.listings.add(FIG);
    await expectCode(quote(store, cfg, ALICE, { item: "mint-fee", figureId: FIG }), "figure_listed");
  });
  it("listed between quote and resume → no mint until the listing is cancelled, then resumes", async () => {
    chain.payments.set(sig(70), { from: ALICE_WALLET, lamports: MINT_FEE_LAMPORTS });
    chain.failBeforeLanding = true;
    await expectCode(run(ALICE, mintFee(sig(70))), "mint_failed");
    chain.failBeforeLanding = false;
    store.figures.get(FIG)!.mintAddress = null; // pretend the reservation was not persisted
    store.listings.add(FIG);
    await expectCode(run(ALICE, mintFee(sig(70))), "figure_listed");
    store.listings.delete(FIG);
    expect((await run(ALICE, mintFee(sig(70)))).mintAddress).toBe(`asset-${FIG}`);
  });
  it("rejects someone else's figure", async () => {
    await expectCode(quote(store, cfg, ALICE, { item: "mint-fee", figureId: FIG2 }), "figure_not_owned");
  });
  it("quotes the mint fee", async () => {
    expect((await quote(store, cfg, ALICE, { item: "mint-fee", figureId: FIG })).lamports).toBe(MINT_FEE_LAMPORTS);
  });
});

describe("concurrency", () => {
  it("a duplicate claim insert (race) falls back to resume, not a second delivery", async () => {
    chain.payments.set(sig(60), { from: ALICE_WALLET, lamports: MINT_FEE_LAMPORTS });
    const [a, b] = await Promise.all([run(ALICE, mintFee(sig(60))), run(ALICE, mintFee(sig(60)))]);
    expect(a.mintAddress).toBe(b.mintAddress);
    expect(chain.onChain.size).toBe(1); // a second Core create at the same address fails on-chain
    expect(store.claims.size).toBe(1);
  });
});
