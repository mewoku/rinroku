/**
 * Devnet SOL purchase flow, independent of Supabase / Solana so it can be unit tested with an
 * in-memory store and a fake chain (see purchase.test.ts). The Supabase + Umi bindings live in
 * purchaseStore.ts / mint.ts.
 *
 * Invariants:
 *  - The buyer is the caller's *linked* wallet (profiles.wallet_address), never a request field.
 *  - A payment signature is consumed exactly once, bound to (user, kind, ref, lamports). A retry with
 *    the same signature may only resume delivery of that same thing.
 *  - A verified payment that cannot be fulfilled is recorded as `sol_refund_due` (never left
 *    claimable, never silently dropped).
 *  - Minting uses a deterministic asset address per figure, reserved in the DB before sending, so a
 *    figure can never be minted twice (Core `create` fails if the asset account already exists).
 */
import { MINT_FEE_LAMPORTS } from "../economy";

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
  insertFigure(row: { userId: string; seed: string; tier: number; encoding: string; rarity: string; name: string }): Promise<FigureRow>;
  figure(id: string): Promise<FigureRow | null>;
  hasActiveListing(figureId: string): Promise<boolean>;
  boss(id: string): Promise<BossRow | null>;
  bossKeysPublished(bossId: string, stages: number): Promise<boolean>;
  hasOpenAttempt(userId: string, bossId: string): Promise<boolean>;
  attempt(id: string): Promise<{ id: string; bossId: string; userId: string } | null>;
  /** enter_boss_sol: attempt + ledger row in one transaction. */
  enterBossSol(userId: string, bossId: string, signature: string, lamports: number): Promise<{ ok: true; attemptId: string } | { ok: false; error: string }>;
  /** Atomically sets mint_address when it is null (or already equal). False if another address is set. */
  reserveMint(figureId: string, address: string): Promise<boolean>;
}

export type TransferCheck = { ok: true; lamports: number } | { ok: false; reason: string };

export interface ChainDeps {
  verifyTransfer(signature: string, expected: { buyer: string; treasury: string; minLamports: number }): Promise<TransferCheck>;
  assetAddress(figureId: string): string;
  assetExists(address: string): Promise<boolean>;
  mint(params: { figureId: string; owner: string; name: string; uri: string }): Promise<string>;
}

export interface PurchaseConfig {
  treasury: string;
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

/**
 * Everything that can be checked before any SOL moves. Used by the client (GET quote) before it
 * builds the transfer, and again by the server after verification.
 */
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

async function recordRefundDue(store: PurchaseStore, signature: string, userId: string, kind: string, ref: string, lamports: number, reason: string): Promise<never> {
  const r = await store.insertClaim(signature, { userId, kind: REFUND_KIND, refId: `${kind}|${ref}`, lamports });
  if (r === "duplicate") throw new PurchaseError("signature_used", "This payment was already used.", 409);
  throw new PurchaseError("refund_due", `${reason} Your payment was recorded for a refund (signature ${signature.slice(0, 8)}…).`, 409);
}

export async function processSolPurchase(
  store: PurchaseStore,
  chain: ChainDeps,
  cfg: PurchaseConfig,
  userId: string,
  body: PurchaseBody,
): Promise<PurchaseOutcome> {
  const kind = KIND[body.item];
  const ref = refFor(body);

  // 1) Earlier claim for this signature? It may only resume exactly what it paid for.
  const prior = await store.claimBySignature(body.signature);
  if (prior) return resume(store, chain, cfg, userId, body, prior);

  // 2) Pre-checks (the client ran them too, before paying).
  let q: Quote;
  try {
    q = await quote(store, cfg, userId, body);
  } catch (e) {
    if (!(e instanceof PurchaseError) || e.code === "wallet_not_linked") throw e;
    // Conditions changed after the client's pre-check. If a real payment from the linked wallet
    // exists, record it for refund; otherwise just report the error.
    const buyer = await store.linkedWallet(userId);
    const paid = buyer ? await chain.verifyTransfer(body.signature, { buyer, treasury: cfg.treasury, minLamports: 1 }) : null;
    if (paid?.ok) return recordRefundDue(store, body.signature, userId, kind, ref, paid.lamports, e.message);
    throw e;
  }

  // 3) Verify the on-chain payment from the linked wallet.
  const check = await chain.verifyTransfer(body.signature, { buyer: q.buyer, treasury: cfg.treasury, minLamports: q.lamports });
  if (!check.ok) throw new PurchaseError("payment_invalid", check.reason, 402);

  // 4) Claim.
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
    return resume(store, chain, cfg, userId, body, raced);
  }

  // 5) Deliver.
  return deliver(store, chain, cfg, userId, body, q.buyer, false);
}

async function resume(store: PurchaseStore, chain: ChainDeps, cfg: PurchaseConfig, userId: string, body: PurchaseBody, prior: Claim): Promise<PurchaseOutcome> {
  if (prior.userId !== userId) throw new PurchaseError("signature_used", "This payment was already claimed.", 409);
  if (prior.kind === REFUND_KIND) throw new PurchaseError("refund_due", "This payment is recorded for a refund.", 409);
  if (body.item === "boss-entry") {
    if (prior.kind !== KIND["boss-entry"] || !prior.refId) throw new PurchaseError("claim_mismatch", "This payment was for a different item.", 409);
    const att = await store.attempt(prior.refId);
    if (!att || att.bossId !== body.bossId || att.userId !== userId) throw new PurchaseError("claim_mismatch", "This payment was for a different raid.", 409);
    return { figureId: null, mintAddress: null, attemptId: att.id, resumed: true };
  }
  if (prior.kind !== KIND[body.item] || prior.refId !== refFor(body)) throw new PurchaseError("claim_mismatch", "This payment was for a different item.", 409);
  const buyer = await store.linkedWallet(userId);
  if (!buyer) throw new PurchaseError("wallet_not_linked", "Link your wallet to resume this purchase.", 409);
  return deliver(store, chain, cfg, userId, body, buyer, true);
}

async function deliver(store: PurchaseStore, chain: ChainDeps, cfg: PurchaseConfig, userId: string, body: PurchaseBody, buyer: string, resumed: boolean): Promise<PurchaseOutcome> {
  let fig: FigureRow | null;
  if (body.item === "figure-legendary") {
    const slot = await store.shelfSlot(body.day!, body.slot!);
    if (!slot) throw new PurchaseError("shelf_not_published", "Shelf data missing — retry later with the same payment.", 503);
    fig =
      (await store.findOwnedFigure(userId, slot.seed, slot.tier)) ??
      (await store.insertFigure({ userId, seed: slot.seed, tier: slot.tier, encoding: slot.encoding, rarity: slot.rarity, name: slot.name }));
  } else {
    fig = await store.figure(body.figureId!);
    if (!fig || fig.ownerId !== userId) throw new PurchaseError("figure_not_owned", "You no longer own that figure — contact support with your payment signature.", 409);
    // Listed after the pre-check: don't mint an asset out from under an open listing. The claim is
    // kept, so cancelling the listing and resuming with the same payment completes the mint.
    if (!fig.mintAddress && (await store.hasActiveListing(fig.id))) {
      throw new PurchaseError("figure_listed", "Cancel the figure's market listing, then resume this payment.", 409);
    }
  }
  const mintAddress = await mintOnce(store, chain, cfg, fig, buyer);
  return { figureId: fig.id, mintAddress, attemptId: null, resumed };
}

/** Idempotent mint: deterministic address, reserved before sending, existence-checked on retry. */
export async function mintOnce(store: PurchaseStore, chain: ChainDeps, cfg: PurchaseConfig, fig: FigureRow, owner: string): Promise<string> {
  const address = chain.assetAddress(fig.id);
  if (fig.mintAddress && fig.mintAddress !== address) return fig.mintAddress; // minted by another path
  if (!(await store.reserveMint(fig.id, address))) {
    const now = await store.figure(fig.id);
    if (now?.mintAddress) return now.mintAddress;
    throw new PurchaseError("mint_reserve_failed", "Could not reserve the mint — retry with the same payment.", 503);
  }
  if (await chain.assetExists(address)) return address;
  try {
    return await chain.mint({ figureId: fig.id, owner, name: `${fig.name} #${fig.id.slice(0, 8)}`, uri: `${cfg.siteUrl}/api/figures/${fig.id}/metadata` });
  } catch {
    // A concurrent retry may have landed the same (deterministic) asset.
    if (await chain.assetExists(address).catch(() => false)) return address;
    throw new PurchaseError("mint_failed", "Mint failed on devnet — your payment is recorded; retry with the same payment to resume.", 502);
  }
}
