import { beforeEach, describe, expect, it } from "vitest";
import {
  KIND,
  REFUND_KIND,
  PurchaseError,
  prepareSolPurchase,
  processSolPurchase,
  quote,
  type ChainDeps,
  type Claim,
  type FigureRow,
  type MintSpec,
  type PurchaseBody,
  type PurchaseConfig,
  type PurchaseRequest,
  type PurchaseStore,
  type ShelfSlot,
} from "./purchase";
import type { PurchaseTxExpectation } from "../solana/purchaseTx";
import { MINT_FEE_LAMPORTS } from "../economy";

// ------------------------------------------------------------------ in-memory fakes
const RECIPIENT = "Recipient11111111111111111111111111111111";
const AUTHORITY = "Authority11111111111111111111111111111111";
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
  async insertFigure(r: { id: string; userId: string; seed: string; tier: number; name: string }) {
    const existing = this.figures.get(r.id);
    if (existing) return existing;
    const row = { id: r.id, name: r.name, ownerId: r.userId, mintAddress: null, seed: r.seed, tier: r.tier };
    this.figures.set(r.id, row);
    return row;
  }
  async figure(id: string) {
    return this.figures.get(id) ?? null;
  }
  async hasActiveListing(id: string) {
    return this.listings.has(id);
  }
  async cancelActiveListings(id: string) {
    this.listings.delete(id);
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

interface Built {
  buyer: string;
  recipient: string;
  lamports: number;
  memo: string;
  mint?: MintSpec;
}
interface Landed {
  feePayer: string;
  recipient: string;
  lamports: number;
  memo: string | null;
  asset: string | null;
  authoritySigned: boolean;
}

/**
 * Simulates the chain: `buildTx` returns the transaction as JSON; `land` is the buyer signing and
 * sending it. Landing is atomic like Solana: if the asset already exists, nothing happens.
 */
class FakeChain implements ChainDeps {
  authority = AUTHORITY;
  onChain = new Set<string>();
  txs = new Map<string, Landed>();
  builds: Built[] = [];
  verifyCalls: PurchaseTxExpectation[] = [];
  lagging = false;

  assetAddress(figureId: string) {
    return `asset-${figureId}`;
  }
  legendaryFigureId(u: string, day: number, slot: number) {
    return `leg-${u}-${day}-${slot}`;
  }
  memo(u: string, kind: string, ref: string) {
    return `ronriku:v2:${kind}:${ref}:${u}`;
  }
  async assetExists(a: string) {
    return !this.lagging && this.onChain.has(a);
  }
  async buildTx(p: Built) {
    this.builds.push(p);
    return { transaction: JSON.stringify(p), lastValidBlockHeight: 1000 };
  }
  /** Buyer signs + sends a prepared transaction. Returns false when it fails on-chain (atomic). */
  land(signature: string, transaction: string): boolean {
    const b = JSON.parse(transaction) as Built;
    const asset = b.mint ? this.assetAddress(b.mint.figureId) : null;
    if (asset && this.onChain.has(asset)) return false; // Core create: account already in use
    if (asset) this.onChain.add(asset);
    this.txs.set(signature, { feePayer: b.buyer, recipient: b.recipient, lamports: b.lamports, memo: b.memo, asset, authoritySigned: true });
    return true;
  }
  async verifyTx(signature: string, e: PurchaseTxExpectation) {
    this.verifyCalls.push(e);
    const t = this.txs.get(signature);
    if (!t) return { ok: false as const, reason: "not found" };
    if (!t.authoritySigned || e.authority !== AUTHORITY) return { ok: false as const, reason: "not prepared by this server" };
    if (t.memo !== e.memo) return { ok: false as const, reason: "memo mismatch" };
    if (e.asset && t.asset !== e.asset) return { ok: false as const, reason: "wrong asset" };
    if (t.recipient !== e.recipient) return { ok: false as const, reason: "wrong recipient" };
    if (t.lamports < e.minLamports) return { ok: false as const, reason: "too small" };
    return { ok: true as const, lamports: t.lamports, buyer: t.feePayer };
  }
}

const cfg: PurchaseConfig = { recipient: RECIPIENT, siteUrl: "http://x", today: TODAY, now: new Date("2026-09-24T12:00:00Z") };
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

const LEG: PurchaseRequest = { item: "figure-legendary", day: TODAY, slot: 5 };
const MINT = (figureId = FIG): PurchaseRequest => ({ item: "mint-fee", figureId });
const BOSSREQ: PurchaseRequest = { item: "boss-entry", bossId: BOSS };
const withSig = (r: PurchaseRequest, s: string): PurchaseBody => ({ ...r, signature: s });
const prepare = (u: string, r: PurchaseRequest) => prepareSolPurchase(store, chain, cfg, u, r);
const run = (u: string, b: PurchaseBody) => processSolPurchase(store, chain, cfg, u, b);

/** prepare → buyer signs/sends → returns the signature. */
async function pay(u: string, r: PurchaseRequest, s: string) {
  const p = await prepare(u, r);
  expect(chain.land(s, p.transaction)).toBe(true);
  return p;
}
const legId = (u: string) => chain.legendaryFigureId(u, TODAY, 5);

// ------------------------------------------------------------------ tests
describe("prepare: one transaction, buyer pays, server authority only signs", () => {
  it("legendary: payment to the recipient + Core mint to the linked wallet, server price", async () => {
    const p = await prepare(ALICE, LEG);
    expect(p).toMatchObject({ lamports: LEG_PRICE, wallet: ALICE_WALLET, recipient: RECIPIENT, figureId: legId(ALICE), assetAddress: `asset-${legId(ALICE)}` });
    expect(chain.builds[0]!).toMatchObject({
      buyer: ALICE_WALLET,
      recipient: RECIPIENT,
      lamports: LEG_PRICE,
      memo: `ronriku:v2:sol_legendary:legendary:${TODAY}:5:${ALICE}`,
      mint: { figureId: legId(ALICE), uri: `http://x/api/figures/${legId(ALICE)}/metadata` },
    });
    expect(chain.builds[0]!.mint!.name.length).toBeLessThanOrEqual(32);
    expect(store.figures.has(legId(ALICE))).toBe(false); // nothing recorded before payment
  });
  it("boss entry: payment + memo, no mint", async () => {
    const p = await prepare(ALICE, BOSSREQ);
    expect(p).toMatchObject({ lamports: 10_000_000, figureId: null, assetAddress: null });
    expect(chain.builds[0]!.mint).toBeUndefined();
  });
  it("mint-fee (shard figure claim): buyer-paid mint of the owned figure", async () => {
    const p = await prepare(ALICE, MINT());
    expect(p).toMatchObject({ lamports: MINT_FEE_LAMPORTS, assetAddress: `asset-${FIG}` });
    expect(chain.builds[0]!.buyer).toBe(ALICE_WALLET);
  });
  it("requires a linked wallet before building anything", async () => {
    store.wallets.set(ALICE, null);
    await expectCode(prepare(ALICE, LEG), "wallet_not_linked");
    expect(chain.builds).toHaveLength(0);
  });
  it("runs all pre-checks before building", async () => {
    await expectCode(prepare(ALICE, { item: "figure-legendary", day: TODAY, slot: 0 }), "not_sol_item");
    await expectCode(prepare(ALICE, { item: "figure-legendary", day: TODAY - 2, slot: 5 }), "shelf_rotated");
    await expectCode(prepare(ALICE, MINT(FIG2)), "figure_not_owned");
    store.listings.add(FIG);
    await expectCode(prepare(ALICE, MINT()), "figure_listed");
    store.attempts.set("open", { id: "open", bossId: BOSS, userId: ALICE, open: true });
    await expectCode(prepare(ALICE, BOSSREQ), "attempt_open");
    expect(chain.builds).toHaveLength(0);
  });
});

describe("happy paths: verify the landed transaction and record ownership", () => {
  it("legendary", async () => {
    await pay(ALICE, LEG, sig(1));
    const out = await run(ALICE, withSig(LEG, sig(1)));
    expect(out).toMatchObject({ figureId: legId(ALICE), mintAddress: `asset-${legId(ALICE)}`, resumed: false });
    expect(store.figures.get(legId(ALICE))).toMatchObject({ ownerId: ALICE, mintAddress: `asset-${legId(ALICE)}` });
    expect(store.claims.get(sig(1))).toEqual({ userId: ALICE, kind: KIND["figure-legendary"], refId: `legendary:${TODAY}:5`, lamports: LEG_PRICE });
    expect(chain.verifyCalls[0]).toMatchObject({ recipient: RECIPIENT, authority: AUTHORITY, minLamports: LEG_PRICE, asset: `asset-${legId(ALICE)}` });
  });
  it("mint-fee", async () => {
    await pay(ALICE, MINT(), sig(2));
    expect(await run(ALICE, withSig(MINT(), sig(2)))).toMatchObject({ figureId: FIG, mintAddress: `asset-${FIG}` });
    expect(store.figures.get(FIG)!.mintAddress).toBe(`asset-${FIG}`);
  });
  it("boss-entry opens an attempt with the ledger row written atomically", async () => {
    await pay(ALICE, BOSSREQ, sig(3));
    const out = await run(ALICE, withSig(BOSSREQ, sig(3)));
    expect(out.attemptId).toBeTruthy();
    expect(store.claims.get(sig(3))).toMatchObject({ userId: ALICE, kind: "boss_entry", refId: out.attemptId });
  });
});

describe("C1: a paid signature is bound to user + kind + ref", () => {
  beforeEach(async () => {
    await pay(ALICE, MINT(), sig(10));
    await run(ALICE, withSig(MINT(), sig(10)));
  });
  it("cannot be reused for a more expensive item", async () => {
    await expectCode(run(ALICE, withSig(LEG, sig(10))), "claim_mismatch");
  });
  it("cannot be reused for another figure", async () => {
    store.figures.set(FIG2, { id: FIG2, name: "OTHER", ownerId: ALICE, mintAddress: null });
    await expectCode(run(ALICE, withSig(MINT(FIG2), sig(10))), "claim_mismatch");
    expect(store.figures.get(FIG2)!.mintAddress).toBeNull();
  });
  it("cannot be reused for a boss entry", async () => {
    await expectCode(run(ALICE, withSig(BOSSREQ, sig(10))), "claim_mismatch");
  });
  it("cannot be claimed by another user", async () => {
    await expectCode(run(BOB, withSig(MINT(FIG2), sig(10))), "signature_used");
  });
  it("a same-item retry only resumes", async () => {
    expect(await run(ALICE, withSig(MINT(), sig(10)))).toMatchObject({ figureId: FIG, mintAddress: `asset-${FIG}`, resumed: true });
  });
  it("an unclaimed transaction prepared for one item cannot be claimed as another (memo)", async () => {
    await pay(ALICE, BOSSREQ, sig(11));
    await expectCode(run(ALICE, withSig(LEG, sig(11))), "payment_invalid");
    expect(store.claims.has(sig(11))).toBe(false);
  });
  it("a boss signature cannot be replayed for another attempt", async () => {
    await pay(ALICE, BOSSREQ, sig(12));
    const first = await run(ALICE, withSig(BOSSREQ, sig(12)));
    expect(await run(ALICE, withSig(BOSSREQ, sig(12)))).toMatchObject({ attemptId: first.attemptId, resumed: true });
  });
});

describe("C2: only the user the transaction was prepared for can claim it", () => {
  it("rejects a victim's landed purchase claimed by someone else", async () => {
    await pay(BOB, LEG, sig(20));
    await expectCode(run(ALICE, withSig(LEG, sig(20))), "payment_invalid");
    expect(store.claims.has(sig(20))).toBe(false);
    expect((await run(BOB, withSig(LEG, sig(20)))).mintAddress).toBe(`asset-${legId(BOB)}`);
  });
  it("rejects a plain transfer the server did not prepare", async () => {
    chain.txs.set(sig(21), { feePayer: ALICE_WALLET, recipient: RECIPIENT, lamports: LEG_PRICE, memo: null, asset: null, authoritySigned: false });
    await expectCode(run(ALICE, withSig(BOSSREQ, sig(21))), "payment_invalid");
  });
  it("rejects underpayment of the server price (M4)", async () => {
    const p = await prepare(ALICE, LEG);
    chain.land(sig(22), JSON.stringify({ ...(JSON.parse(p.transaction) as Built), lamports: LEG_PRICE - 1 }));
    await expectCode(run(ALICE, withSig(LEG, sig(22))), "payment_invalid");
  });
  it("delivery does not depend on the wallet still being linked", async () => {
    await pay(ALICE, LEG, sig(23));
    store.wallets.set(ALICE, null);
    expect((await run(ALICE, withSig(LEG, sig(23)))).figureId).toBe(legId(ALICE));
  });
});

describe("atomic payment + mint (no 'paid but not minted')", () => {
  it("two prepared transactions for the same Legendary: only one can land, the other charges nothing", async () => {
    const a = await prepare(ALICE, LEG);
    const b = await prepare(ALICE, LEG);
    expect(chain.land(sig(30), a.transaction)).toBe(true);
    expect(chain.land(sig(31), b.transaction)).toBe(false);
    expect(chain.txs.has(sig(31))).toBe(false);
  });
  it("landed but never verified → prepare self-heals ownership instead of charging again", async () => {
    await pay(ALICE, LEG, sig(32));
    await expectCode(prepare(ALICE, LEG), "already_owned");
    expect(store.figures.get(legId(ALICE))).toMatchObject({ ownerId: ALICE, mintAddress: `asset-${legId(ALICE)}` });
    expect(chain.builds).toHaveLength(1);
    expect(await run(ALICE, withSig(LEG, sig(32)))).toMatchObject({ figureId: legId(ALICE), mintAddress: `asset-${legId(ALICE)}` });
    expect([...store.figures.values()].filter((f) => f.seed === "99")).toHaveLength(1);
  });
  it("mint-fee landed but never verified → prepare records the mint", async () => {
    await pay(ALICE, MINT(), sig(33));
    await expectCode(prepare(ALICE, MINT()), "already_minted");
    expect(store.figures.get(FIG)!.mintAddress).toBe(`asset-${FIG}`);
  });
  it("RPC lag: asset not visible yet → retryable 503, then the same signature completes", async () => {
    await pay(ALICE, MINT(), sig(34));
    chain.lagging = true;
    await expectCode(run(ALICE, withSig(MINT(), sig(34))), "asset_not_visible");
    chain.lagging = false;
    expect(await run(ALICE, withSig(MINT(), sig(34)))).toMatchObject({ mintAddress: `asset-${FIG}`, resumed: true });
  });
  it("a figure recorded with another address is not re-minted", async () => {
    store.figures.get(FIG)!.mintAddress = "someone-else";
    await expectCode(prepare(ALICE, MINT()), "already_minted");
    await expectCode(quote(store, cfg, ALICE, MINT()), "already_minted");
  });
});

describe("H1: boss entry never loses a payment", () => {
  it("pre-checks inactive raids and unpublished keys", async () => {
    await expectCode(quote(store, { ...cfg, now: new Date("2026-10-05T00:00:00Z") }, ALICE, BOSSREQ), "boss_not_active");
    store.keysPublished = false;
    await expectCode(quote(store, cfg, ALICE, BOSSREQ), "puzzle_not_published");
  });
  it("records refund_due when the attempt opened between prepare and payment", async () => {
    await pay(ALICE, BOSSREQ, sig(40));
    store.attempts.set("open", { id: "open", bossId: BOSS, userId: ALICE, open: true });
    await expectCode(run(ALICE, withSig(BOSSREQ, sig(40))), "refund_due");
    expect(store.claims.get(sig(40))).toMatchObject({ userId: ALICE, kind: REFUND_KIND, lamports: 10_000_000 });
    await expectCode(run(ALICE, withSig(BOSSREQ, sig(40))), "refund_due"); // not claimable afterwards
  });
  it("records refund_due when the RPC fails after payment", async () => {
    await pay(ALICE, BOSSREQ, sig(41));
    store.enterBossFailure = "boss_not_active";
    await expectCode(run(ALICE, withSig(BOSSREQ, sig(41))), "refund_due");
    expect(store.claims.get(sig(41))!.kind).toBe(REFUND_KIND);
  });
  it("does not record anything when there was no valid payment", async () => {
    await expectCode(run(ALICE, withSig(BOSSREQ, sig(42))), "payment_invalid");
    expect(store.claims.size).toBe(0);
  });
});

describe("H3: buyers are never left with nothing", () => {
  it("verification has no time-sensitive checks (resume after the shelf rotated)", async () => {
    await pay(ALICE, LEG, sig(50));
    const later = { ...cfg, today: TODAY + 3 };
    expect((await processSolPurchase(store, chain, later, ALICE, withSig(LEG, sig(50)))).mintAddress).toBe(`asset-${legId(ALICE)}`);
  });
  it("'already own this Legendary' is caught before payment", async () => {
    await pay(ALICE, LEG, sig(51));
    await run(ALICE, withSig(LEG, sig(51)));
    await expectCode(quote(store, cfg, ALICE, LEG), "already_owned");
    await expectCode(prepare(ALICE, LEG), "already_owned");
  });
  it("a Legendary traded away cannot be bought into the same deterministic asset again", async () => {
    await pay(ALICE, LEG, sig(52));
    await run(ALICE, withSig(LEG, sig(52)));
    store.figures.get(legId(ALICE))!.ownerId = BOB;
    await expectCode(prepare(ALICE, LEG), "already_owned");
  });
});

describe("M5: listings vs minting", () => {
  it("listed between prepare and verify → the listing is cancelled and the mint recorded", async () => {
    await pay(ALICE, MINT(), sig(60));
    store.listings.add(FIG);
    expect((await run(ALICE, withSig(MINT(), sig(60)))).mintAddress).toBe(`asset-${FIG}`);
    expect(store.listings.has(FIG)).toBe(false);
  });
  it("quotes the mint fee", async () => {
    expect((await quote(store, cfg, ALICE, MINT())).lamports).toBe(MINT_FEE_LAMPORTS);
  });
});

describe("concurrency", () => {
  it("a duplicate claim insert (race) falls back to resume, not a second delivery", async () => {
    await pay(ALICE, LEG, sig(70));
    const [a, b] = await Promise.all([run(ALICE, withSig(LEG, sig(70))), run(ALICE, withSig(LEG, sig(70)))]);
    expect(a.mintAddress).toBe(b.mintAddress);
    expect(store.claims.size).toBe(1);
    expect([...store.figures.values()].filter((f) => f.seed === "99")).toHaveLength(1);
  });
});
