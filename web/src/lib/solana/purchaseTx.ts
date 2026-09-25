/**
 * Single-transaction SOL purchases (browser-safe: web3.js only, no secrets).
 *
 * The server builds ONE transaction per purchase and partially signs it:
 *   1. System transfer  buyer → payment recipient  (the price)
 *   2. SPL Memo         "ronriku:v2:<kind>:<ref>:<userTag>", signed by the server mint authority
 *   3. (figures only)   Metaplex Core CreateV2: asset (signer), authority = server, payer = buyer,
 *                       owner = buyer, update authority = server
 * Fee payer = buyer. The buyer's wallet adds the last signature and broadcasts. Because the whole
 * message carries the server authority's signature, the server can later prove it prepared this
 * exact transaction for this user and item (memo), and payment + mint are atomic: either both land
 * or neither does. The mint authority never pays anything, so it needs no SOL.
 */
import { SystemInstruction, type ParsedTransactionWithMeta, type Transaction } from "@solana/web3.js";

export const SYSTEM_PROGRAM_ID = "11111111111111111111111111111111";
export const MEMO_PROGRAM_ID = "MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr";
export const MPL_CORE_PROGRAM_ID = "CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4PZNhX7d";
export const COMPUTE_BUDGET_PROGRAM_ID = "ComputeBudget111111111111111111111111111111";
/** mpl-core CreateV2 instruction discriminator (first data byte). */
export const CORE_CREATE_V2_DISCRIMINATOR = 20;

export function purchaseMemo(kind: string, ref: string, userTag: string): string {
  return `ronriku:v2:${kind}:${ref}:${userTag}`;
}

export type PurchaseTxCheck = { ok: true; lamports: number; buyer: string } | { ok: false; reason: string };

export interface PurchaseTxExpectation {
  /** Exact memo the server derives for (user, kind, ref). */
  memo: string;
  recipient: string;
  /** Server mint authority public key: must have signed the transaction. */
  authority: string;
  minLamports: number;
  /** Core asset that must have been created by this transaction (figure items), or null. */
  asset: string | null;
}

/**
 * Server-side verification of a landed purchase transaction (jsonParsed). No payment-age window:
 * the authority signature + memo bind the transaction to one (user, kind, ref), and the ledger
 * consumes each signature once, so an old transaction cannot be replayed for anything else.
 */
export function checkPurchaseTx(tx: ParsedTransactionWithMeta | null, e: PurchaseTxExpectation): PurchaseTxCheck {
  if (!tx) return { ok: false, reason: "Transaction not found (yet). Wait for confirmation and retry." };
  if (!tx.meta || tx.meta.err) return { ok: false, reason: "Transaction failed on-chain." };
  if (tx.blockTime == null) return { ok: false, reason: "Transaction has no block time yet — retry shortly." };
  const keys = tx.transaction.message.accountKeys;
  const payer = keys[0];
  if (!payer?.signer) return { ok: false, reason: "Transaction has no fee payer signature." };
  const buyer = payer.pubkey.toBase58();
  if (!keys.some((k) => k.signer && k.pubkey.toBase58() === e.authority)) return { ok: false, reason: "This transaction was not prepared by this server." };

  const top = tx.transaction.message.instructions;
  const memoOk = top.some((ix) => ix.programId.toBase58() === MEMO_PROGRAM_ID && "parsed" in ix && ix.parsed === e.memo);
  if (!memoOk) return { ok: false, reason: "This payment was prepared for a different user or item." };

  if (e.asset) {
    const assetKey = keys.find((k) => k.pubkey.toBase58() === e.asset);
    if (!assetKey?.signer || !assetKey.writable) return { ok: false, reason: "This transaction did not create the expected asset." };
    if (!top.some((ix) => ix.programId.toBase58() === MPL_CORE_PROGRAM_ID)) return { ok: false, reason: "This transaction did not mint." };
  }

  let paid = 0;
  const all = [...top, ...(tx.meta.innerInstructions ?? []).flatMap((i) => i.instructions)];
  for (const ix of all) {
    if (!("parsed" in ix) || ix.programId.toBase58() !== SYSTEM_PROGRAM_ID) continue;
    const p = ix.parsed as { type?: string; info?: { source?: string; destination?: string; lamports?: number } };
    if (p.type === "transfer" && p.info?.source === buyer && p.info.destination === e.recipient) paid += Number(p.info.lamports ?? 0);
  }
  if (paid <= 0) return { ok: false, reason: "No payment to the recipient in this transaction." };
  if (paid < e.minLamports) return { ok: false, reason: `Payment too small (${paid} < ${e.minLamports} lamports).` };
  return { ok: true, lamports: paid, buyer };
}

/**
 * Client-side check before the wallet signs a server-prepared transaction: fee payer is the buyer,
 * the only SOL leaving the wallet by System instruction is exactly `lamports` to `recipient`, and
 * every other instruction is a memo, compute-budget or Core CreateV2 (no transfers of existing
 * assets, no approvals). Returns an error string, or null when it is safe to sign.
 */
export function inspectPreparedTx(tx: Transaction, e: { buyer: string; recipient: string; lamports: number; mint: boolean }): string | null {
  if (tx.feePayer?.toBase58() !== e.buyer) return "Prepared transaction has the wrong fee payer.";
  let paid = 0;
  let creates = 0;
  for (const ix of tx.instructions) {
    const pid = ix.programId.toBase58();
    if (pid === SYSTEM_PROGRAM_ID) {
      let type: string;
      try {
        type = SystemInstruction.decodeInstructionType(ix);
      } catch {
        return "Prepared transaction has an unknown System instruction.";
      }
      if (type !== "Transfer") return `Prepared transaction has an unexpected System ${type}.`;
      const t = SystemInstruction.decodeTransfer(ix);
      if (t.fromPubkey.toBase58() !== e.buyer || t.toPubkey.toBase58() !== e.recipient) return "Prepared transaction pays the wrong account.";
      paid += Number(t.lamports);
    } else if (pid === MPL_CORE_PROGRAM_ID) {
      if (ix.data[0] !== CORE_CREATE_V2_DISCRIMINATOR) return "Prepared transaction has an unexpected Core instruction.";
      creates++;
    } else if (pid !== MEMO_PROGRAM_ID && pid !== COMPUTE_BUDGET_PROGRAM_ID) {
      return `Prepared transaction calls an unexpected program (${pid}).`;
    }
  }
  if (paid !== e.lamports) return `Prepared transaction charges ${paid} lamports, expected ${e.lamports}.`;
  if (e.mint ? creates !== 1 : creates !== 0) return "Prepared transaction has an unexpected number of mints.";
  return null;
}
