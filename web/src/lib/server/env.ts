import "server-only";

import { Keypair } from "@solana/web3.js";
import { publicEnv } from "../env";

/** Server-only secrets. Importing this module from a client component fails the build (server-only). */
export function serviceRoleKey(): string | null {
  return process.env.SUPABASE_SERVICE_ROLE_KEY || null;
}

export function treasurySecretKey(): Uint8Array | null {
  const raw = process.env.TREASURY_SECRET_KEY;
  if (!raw) return null;
  try {
    const arr: unknown = JSON.parse(raw);
    if (!Array.isArray(arr) || arr.length !== 64 || !arr.every((n) => Number.isInteger(n) && n >= 0 && n <= 255)) return null;
    return Uint8Array.from(arr as number[]);
  } catch {
    return null;
  }
}

export type TreasuryConfig = { ok: true; secret: Uint8Array; pubkey: string } | { ok: false; reason: string };

let cached: TreasuryConfig | null = null;

/** Treasury secret + public key, and a check that NEXT_PUBLIC_TREASURY_PUBKEY really is its public key. */
export function treasuryConfig(): TreasuryConfig {
  if (cached) return cached;
  const secret = treasurySecretKey();
  if (!secret) return (cached = { ok: false, reason: "TREASURY_SECRET_KEY missing or malformed" });
  if (!publicEnv.treasuryPubkey) return (cached = { ok: false, reason: "NEXT_PUBLIC_TREASURY_PUBKEY missing" });
  let derived: string;
  try {
    derived = Keypair.fromSecretKey(secret).publicKey.toBase58();
  } catch {
    return (cached = { ok: false, reason: "TREASURY_SECRET_KEY is not a valid ed25519 keypair" });
  }
  if (derived !== publicEnv.treasuryPubkey) return (cached = { ok: false, reason: "NEXT_PUBLIC_TREASURY_PUBKEY does not match TREASURY_SECRET_KEY" });
  return (cached = { ok: true, secret, pubkey: derived });
}
