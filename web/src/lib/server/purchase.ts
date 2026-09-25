/**
 * Devnet SOL purchase flow, independent of Supabase / Solana so it can be unit tested with an
 * in-memory store and a fake chain (see purchase.test.ts). The Supabase + Solana bindings live in
 * purchaseStore.ts / mint.ts; the transaction layout is documented in lib/solana/purchaseTx.ts.
 *
 * Flow: prepare (server checks everything, builds ONE transaction: payment → recipient + memo +
 * Core create, partially signs it) → the buyer's wallet signs, pays all fees/rent and broadcasts →
 * process (server verifies the landed transaction and records ownership).
 *
 * Invariants:
 *  - The buyer is the caller's *linked* wallet (profiles.wallet_address), never a request field; it
 *    is baked into the prepared transaction as fee payer and cannot be changed without voiding the
 *    server's signature.
 *  - Price comes from the server. The memo, signed by the server mint authority, binds a transaction
 *    to exactly one (user, kind, ref); a payment signature is consumed exactly once in the ledger, and
 *    a retry with the same signature may only resume delivery of that same thing.
 *  - Payment and mint are one atomic transaction: "paid but not minted" cannot happen. Each figure
 *    has a deterministic asset address, so it can be created on-chain at most once.
 *  - A verified boss payment that cannot be fulfilled is recorded as `sol_refund_due`.
 *  - The mint authority never pays: the buyer is fee payer and Core rent payer.
 */
import { MINT_FEE_LAMPORTS } from "../economy";
import type { PurchaseTxCheck, PurchaseTxExpectation } from "../solana/purchaseTx";

export type PurchaseItem = "figure-legendary" | "mint-fee" | "boss-entry";

export interface PurchaseRequest {
  item: PurchaseItem;
  figureId?: string;
  bossId?: string;
  day?: number;
  slot?: number;
}

export interface PurchaseBody extends PurchaseRequest {
  signature: string;
}

export const KIND = {
  "figure-legendary": "sol_legendary",
  "mint-fee": "sol_mint_fee",
  "boss-entry": "boss_entry", // written by enter_boss_sol (ref_id = attempt id)
} as const;
export const REFUND_KIND = "sol_refund_due";

export interface Claim {
  userId: string;
  kind: string;
  refId: string | null;
  lamports: number | null;
}

export interface ShelfSlot {
  day: number;
  slot: number;
  itemId: string;
  /** signed 64-bit decimal as stored in the DB */
  seed: string;
  tier: number;
  rarity: string;
  name: string;
  encoding: string;
  priceLamports: number | null;
}

export interface FigureRow {
  id: string;
  name: string;
  ownerId: string | null;
  mintAddress: string | null;
}

export interface BossRow {
  id: string;
  entryLamports: number;
  startsAt: string;
  endsAt: string;
  stages: number;
}

export interface PurchaseStore {
  linkedWallet(userId: string): Promise<string | null>;
  claimBySignature(signature: string): Promise<Claim | null>;
  /** Insert a ledger row carrying the signature; "duplicate" when the signature is already used. */
  insertClaim(signature: string, claim: Claim): Promise<"ok" | "duplicate">;
  shelfSlot(day: number, slot: number): Promise<ShelfSlot | null>;
  findOwnedFigure(userId: string, seed: string, tier: number): Promise<FigureRow | null>;
  /** Insert with a caller-chosen id; returns the existing row if that id is already present. */
  insertFigure(row: { id: string; userId: string; seed: string; tier: number; encoding: string; rarity: string; name: string }): Promise<FigureRow>;
  figure(id: string): Promise<FigureRow | null>;
  hasActiveListing(figureId: string): Promise<boolean>;
  /** Minted figures cannot carry shard listings (list_figure invariant): cancel any that slipped in. */
  cancelActiveListings(figureId: string): Promise<void>;
  boss(id: string): Promise<BossRow | null>;
  bossKeysPublished(bossId: string, stages: number): Promise<boolean>;
  hasOpenAttempt(userId: string, bossId: string): Promise<boolean>;
  attempt(id: string): Promise<{ id: string; bossId: string; userId: string } | null>;
  /** enter_boss_sol: attempt + ledger row in one transaction. */
  enterBossSol(userId: string, bossId: string, signature: string, lamports: number): Promise<{ ok: true; attemptId: string } | { ok: false; error: string }>;
  /** Atomically sets mint_address when it is null (or already equal). False if another address is set. */
  reserveMint(figureId: string, address: string): Promise<boolean>;
}

export interface MintSpec {
  figureId: string;
  name: string;
  uri: string;
}

export interface ChainDeps {
  /** Server mint authority public key (co-signs every prepared transaction; holds no SOL). */
  authority: string;
  assetAddress(figureId: string): string;
  legendaryFigureId(userId: string, day: number, slot: number): string;
  memo(userId: string, kind: string, ref: string): string;
  assetExists(address: string): Promise<boolean>;
  /** Build + partially sign the purchase transaction. `transaction` is base64 wire format. */
  buildTx(p: { buyer: string; recipient: string; lamports: number; memo: string; mint?: MintSpec }): Promise<{ transaction: string; lastValidBlockHeight: number }>;
  verifyTx(signature: string, expected: PurchaseTxExpectation): Promise<PurchaseTxCheck>;
}

export interface PurchaseConfig {
  /** Public payment recipient (NEXT_PUBLIC_PAYMENT_RECIPIENT). */
  recipient: string;
  siteUrl: string;
  today: number;
  now: Date;
}

export class PurchaseError extends Error {
  constructor(
    readonly code: string,
    message: string,
    readonly status: number,
  ) {
    super(message);
  }
}

export interface Quote {
  item: PurchaseItem;
  kind: string;
  ref: string;
  lamports: number;
  buyer: string;
}

export interface Prepared {
  transaction: string;
  lastValidBlockHeight: number;
  lamports: number;
  wallet: string;
  recipient: string;
  figureId: string | null;
  assetAddress: string | null;
}

export interface PurchaseOutcome {
  figureId: string | null;
  mintAddress: string | null;
  attemptId: string | null;
  resumed: boolean;
}

export function refFor(req: PurchaseRequest): string {
  switch (req.item) {
    case "figure-legendary":
      return `legendary:${req.day}:${req.slot}`;
    case "mint-fee":
      return `mint:${req.figureId}`;
    case "boss-entry":
      return `boss:${req.bossId}`;
  }
}

const nftName = (name: string, figureId: string) => `${name} #${figureId.slice(0, 8)}`.slice(0, 32);
const metadataUri = (cfg: PurchaseConfig, figureId: string) => `${cfg.siteUrl}/api/figures/${figureId}/metadata`;

/** Everything that can be checked before any SOL moves (time-sensitive checks included). */
export async function quote(store: PurchaseStore, cfg: PurchaseConfig, userId: string, req: PurchaseRequest): Promise<Quote> {
  const buyer = await store.linkedWallet(userId);
  if (!buyer) throw new PurchaseError("wallet_not_linked", "Link your wallet to your profile before paying with SOL.", 409);
  const base = { item: req.item, kind: KIND[req.item], ref: refFor(req), buyer };

  if (req.item === "figure-legendary") {
    const day = req.day!;
    if (day !== cfg.today && day !== cfg.today - 1) throw new PurchaseError("shelf_rotated", "That shelf has rotated.", 410);
    const slot = await store.shelfSlot(day, req.slot!);
    if (!slot) throw new PurchaseError("shelf_not_published", "The shop shelf is not published.", 404);
    if (slot.rarity !== "Legendary" || !slot.priceLamports) throw new PurchaseError("not_sol_item", "That shelf item is not sold for SOL.", 400);
    if (await store.findOwnedFigure(userId, slot.seed, slot.tier)) throw new PurchaseError("already_owned", "You already own this Legendary.", 409);
    return { ...base, lamports: slot.priceLamports };
  }

  if (req.item === "mint-fee") {
    const fig = await store.figure(req.figureId!);
    if (!fig || fig.ownerId !== userId) throw new PurchaseError("figure_not_owned", "You don't own that figure.", 403);
    if (fig.mintAddress) throw new PurchaseError("already_minted", "This figure is already minted.", 409);
    if (await store.hasActiveListing(fig.id)) throw new PurchaseError("figure_listed", "Cancel the figure's market listing before minting.", 409);
    return { ...base, lamports: MINT_FEE_LAMPORTS };
  }

  const boss = await store.boss(req.bossId!);
  if (!boss) throw new PurchaseError("unknown_boss", "Unknown boss event.", 404);
  const t = cfg.now.getTime();
  if (t < new Date(boss.startsAt).getTime() || t > new Date(boss.endsAt).getTime()) throw new PurchaseError("boss_not_active", "This raid is not active.", 410);
  if (!(await store.bossKeysPublished(boss.id, boss.stages))) throw new PurchaseError("puzzle_not_published", "This raid is not ready yet.", 503);
  if (await store.hasOpenAttempt(userId, boss.id)) throw new PurchaseError("attempt_open", "You already have an open attempt for this raid — finish it in the game first.", 409);
  return { ...base, lamports: boss.entryLamports };
}

/**
 * Pre-checks, then builds the single partially-signed transaction for the buyer to sign and send.
 * Self-heals when the figure's deterministic asset already exists on-chain (a landed purchase whose
 * verification request never arrived): ownership is recorded and the caller is told it is done.
 */
export async function prepareSolPurchase(store: PurchaseStore, chain: ChainDeps, cfg: PurchaseConfig, userId: string, req: PurchaseRequest): Promise<Prepared> {
  const q = await quote(store, cfg, userId, req);
  const memo = chain.memo(userId, q.kind, q.ref);
  let mint: MintSpec | undefined;

  if (req.item === "figure-legendary") {
    const slot = (await store.shelfSlot(req.day!, req.slot!))!;
    const figureId = chain.legendaryFigureId(userId, req.day!, req.slot!);
    if (await store.figure(figureId)) throw new PurchaseError("already_owned", "You already bought this Legendary.", 409);
    const address = chain.assetAddress(figureId);
    if (await chain.assetExists(address)) {
      const fig = await store.insertFigure({ id: figureId, userId, seed: slot.seed, tier: slot.tier, encoding: slot.encoding, rarity: slot.rarity, name: slot.name });
      await store.reserveMint(fig.id, address);
      throw new PurchaseError("already_owned", "You already bought this Legendary — it is in your inventory.", 409);
    }
    mint = { figureId, name: nftName(slot.name, figureId), uri: metadataUri(cfg, figureId) };
  } else if (req.item === "mint-fee") {
    const fig = (await store.figure(req.figureId!))!;
    const address = chain.assetAddress(fig.id);
    if (await chain.assetExists(address)) {
      await store.reserveMint(fig.id, address);
      throw new PurchaseError("already_minted", "This figure is already minted.", 409);
    }
    mint = { figureId: fig.id, name: nftName(fig.name, fig.id), uri: metadataUri(cfg, fig.id) };
  }

  const built = await chain.buildTx({ buyer: q.buyer, recipient: cfg.recipient, lamports: q.lamports, memo, mint });
  return {
    ...built,
    lamports: q.lamports,
    wallet: q.buyer,
    recipient: cfg.recipient,
    figureId: mint?.figureId ?? null,
    assetAddress: mint ? chain.assetAddress(mint.figureId) : null,
  };
}

async function recordRefundDue(store: PurchaseStore, signature: string, userId: string, kind: string, ref: string, lamports: number, reason: string): Promise<never> {
  const r = await store.insertClaim(signature, { userId, kind: REFUND_KIND, refId: `${kind}|${ref}`, lamports });
  if (r === "duplicate") throw new PurchaseError("signature_used", "This payment was already used.", 409);
  throw new PurchaseError("refund_due", `${reason} Your payment was recorded for a refund (signature ${signature.slice(0, 8)}…).`, 409);
}

/** Price and asset the prepared transaction must show. No time-sensitive checks: those ran at prepare. */
async function expectationFor(store: PurchaseStore, chain: ChainDeps, userId: string, body: PurchaseBody): Promise<{ lamports: number; asset: string | null }> {
  if (body.item === "figure-legendary") {
    const slot = await store.shelfSlot(body.day!, body.slot!);
    if (!slot) throw new PurchaseError("shelf_not_published", "Shelf data missing — retry later with the same payment.", 503);
    if (!slot.priceLamports) throw new PurchaseError("not_sol_item", "That shelf item is not sold for SOL.", 400);
    return { lamports: slot.priceLamports, asset: chain.assetAddress(chain.legendaryFigureId(userId, body.day!, body.slot!)) };
  }
  if (body.item === "mint-fee") return { lamports: MINT_FEE_LAMPORTS, asset: chain.assetAddress(body.figureId!) };
  const boss = await store.boss(body.bossId!);
  if (!boss) throw new PurchaseError("unknown_boss", "Unknown boss event.", 404);
  return { lamports: boss.entryLamports, asset: null };
}

export async function processSolPurchase(store: PurchaseStore, chain: ChainDeps, cfg: PurchaseConfig, userId: string, body: PurchaseBody): Promise<PurchaseOutcome> {
  const kind = KIND[body.item];
  const ref = refFor(body);

  // 1) Earlier claim for this signature? It may only resume exactly what it paid for.
  const prior = await store.claimBySignature(body.signature);
  if (prior) return resume(store, chain, userId, body, prior);

  // 2) Verify the landed transaction is the one this server prepared for (user, kind, ref).
  const exp = await expectationFor(store, chain, userId, body);
  const check = await chain.verifyTx(body.signature, {
    memo: chain.memo(userId, kind, ref),
    recipient: cfg.recipient,
    authority: chain.authority,
    minLamports: exp.lamports,
    asset: exp.asset,
  });
  if (!check.ok) throw new PurchaseError("payment_invalid", check.reason, 402);

  // 3) Claim.
  if (body.item === "boss-entry") {
    const r = await store.enterBossSol(userId, body.bossId!, body.signature, check.lamports);
    if (r.ok) return { figureId: null, mintAddress: null, attemptId: r.attemptId, resumed: false };
    if (r.error.includes("signature_used")) throw new PurchaseError("signature_used", "This payment was already used.", 409);
    return recordRefundDue(store, body.signature, userId, kind, ref, check.lamports, `Raid entry failed (${r.error}).`);
  }
  const ins = await store.insertClaim(body.signature, { userId, kind, refId: ref, lamports: check.lamports });
  if (ins === "duplicate") {
    const raced = await store.claimBySignature(body.signature);
    if (!raced) throw new PurchaseError("claim_failed", "Could not record payment — retry.", 503);
    return resume(store, chain, userId, body, raced);
  }

  // 4) Record ownership of what the transaction already minted.
  return deliver(store, chain, userId, body, false);
}

async function resume(store: PurchaseStore, chain: ChainDeps, userId: string, body: PurchaseBody, prior: Claim): Promise<PurchaseOutcome> {
  if (prior.userId !== userId) throw new PurchaseError("signature_used", "This payment was already claimed.", 409);
  if (prior.kind === REFUND_KIND) throw new PurchaseError("refund_due", "This payment is recorded for a refund.", 409);
  if (body.item === "boss-entry") {
    if (prior.kind !== KIND["boss-entry"] || !prior.refId) throw new PurchaseError("claim_mismatch", "This payment was for a different item.", 409);
    const att = await store.attempt(prior.refId);
    if (!att || att.bossId !== body.bossId || att.userId !== userId) throw new PurchaseError("claim_mismatch", "This payment was for a different raid.", 409);
    return { figureId: null, mintAddress: null, attemptId: att.id, resumed: true };
  }
  if (prior.kind !== KIND[body.item] || prior.refId !== refFor(body)) throw new PurchaseError("claim_mismatch", "This payment was for a different item.", 409);
  return deliver(store, chain, userId, body, true);
}

async function deliver(store: PurchaseStore, chain: ChainDeps, userId: string, body: PurchaseBody, resumed: boolean): Promise<PurchaseOutcome> {
  let fig: FigureRow | null;
  if (body.item === "figure-legendary") {
    const slot = await store.shelfSlot(body.day!, body.slot!);
    if (!slot) throw new PurchaseError("shelf_not_published", "Shelf data missing — retry later with the same payment.", 503);
    const id = chain.legendaryFigureId(userId, body.day!, body.slot!);
    fig = (await store.figure(id)) ?? (await store.insertFigure({ id, userId, seed: slot.seed, tier: slot.tier, encoding: slot.encoding, rarity: slot.rarity, name: slot.name }));
  } else {
    fig = await store.figure(body.figureId!);
    if (!fig) throw new PurchaseError("figure_not_found", "That figure no longer exists — contact support with your payment signature.", 409);
  }
  const address = chain.assetAddress(fig.id);
  if (fig.mintAddress !== address) {
    // The verified transaction created it; double-check the account is visible before recording.
    if (!(await chain.assetExists(address))) throw new PurchaseError("asset_not_visible", "The mint is not visible on devnet yet — retry in a few seconds.", 503);
    await store.cancelActiveListings(fig.id);
    if (!(await store.reserveMint(fig.id, address))) throw new PurchaseError("mint_conflict", "This figure is recorded with a different asset — contact support.", 409);
  }
  if (fig.ownerId !== userId && body.item === "mint-fee") {
    throw new PurchaseError("figure_not_owned", "The figure changed owner while minting — contact support with your payment signature.", 409);
  }
  return { figureId: fig.id, mintAddress: address, attemptId: null, resumed };
}
