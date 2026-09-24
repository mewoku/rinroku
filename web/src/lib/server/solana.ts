import "server-only";

import { Connection, type ParsedTransactionWithMeta } from "@solana/web3.js";
import { publicEnv } from "../env";
import type { TransferCheck } from "./purchase";

const SYSTEM_PROGRAM = "11111111111111111111111111111111";
/** Payments must land within this window before the verification request. */
export const MAX_PAYMENT_AGE_SECONDS = 15 * 60;

/**
 * Commitment used to accept a payment. `confirmed` (supermajority vote, ~1 s) is fine on devnet
 * where nothing of value moves. On mainnet this MUST be `finalized`: a confirmed block can still be
 * dropped in a fork, and the item would already have been delivered.
 */
export const PAYMENT_COMMITMENT = "confirmed" as const;

export function devnetConnection(): Connection {
  return new Connection(publicEnv.solanaRpcUrl, PAYMENT_COMMITMENT);
}

/**
 * Pure check over a parsed transaction: succeeded, has a block time, recent, signed by `buyer`, and
 * contains System transfers totalling at least `minLamports` from `buyer` to `treasury`
 * (top-level or inner instructions).
 */
export function checkTransfer(
  tx: ParsedTransactionWithMeta | null,
  expected: { buyer: string; treasury: string; minLamports: number },
  nowSeconds = Math.floor(Date.now() / 1000),
): TransferCheck {
  if (!tx) return { ok: false, reason: "Transaction not found (yet). Wait for confirmation and retry." };
  if (!tx.meta || tx.meta.err) return { ok: false, reason: "Transaction failed on-chain." };
  if (tx.blockTime == null) return { ok: false, reason: "Transaction has no block time yet — retry shortly." };
  if (nowSeconds - tx.blockTime > MAX_PAYMENT_AGE_SECONDS) return { ok: false, reason: "Payment is too old." };
  const signerOk = tx.transaction.message.accountKeys.some((k) => k.signer && k.pubkey.toBase58() === expected.buyer);
  if (!signerOk) return { ok: false, reason: "Your linked wallet did not sign this transaction." };
  const all = [...tx.transaction.message.instructions, ...(tx.meta.innerInstructions ?? []).flatMap((i) => i.instructions)];
  let paid = 0;
  for (const ix of all) {
    if (!("parsed" in ix) || ix.programId.toBase58() !== SYSTEM_PROGRAM) continue;
    const parsed = ix.parsed as { type?: string; info?: { source?: string; destination?: string; lamports?: number } };
    if (parsed.type !== "transfer" || !parsed.info) continue;
    if (parsed.info.source === expected.buyer && parsed.info.destination === expected.treasury) paid += Number(parsed.info.lamports ?? 0);
  }
  if (paid <= 0) return { ok: false, reason: "No transfer from your linked wallet to the treasury in this transaction." };
  if (paid < expected.minLamports) return { ok: false, reason: `Transfer to treasury too small (${paid} < ${expected.minLamports} lamports).` };
  return { ok: true, lamports: paid };
}

export async function verifyTransfer(signature: string, expected: { buyer: string; treasury: string; minLamports: number }): Promise<TransferCheck> {
  const tx = await devnetConnection().getParsedTransaction(signature, { commitment: PAYMENT_COMMITMENT, maxSupportedTransactionVersion: 0 });
  return checkTransfer(tx, expected);
}
