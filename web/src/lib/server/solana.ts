import "server-only";

import { Connection } from "@solana/web3.js";
import { publicEnv } from "../env";

/**
 * Commitment used to accept a payment. `confirmed` (supermajority vote, ~1 s) is fine on devnet
 * where nothing of value moves. On mainnet this MUST be `finalized`: a confirmed block can still be
 * dropped in a fork, and the item would already have been delivered.
 */
export const PAYMENT_COMMITMENT = "confirmed" as const;

export function devnetConnection(): Connection {
  return new Connection(publicEnv.solanaRpcUrl, PAYMENT_COMMITMENT);
}
