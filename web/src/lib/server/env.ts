import "server-only";

import { Keypair, PublicKey } from "@solana/web3.js";
import { publicEnv } from "../env";

/** Server-only secrets. Importing this module from a client component fails the build (server-only). */
export function serviceRoleKey(): string | null {
  return process.env.SUPABASE_SERVICE_ROLE_KEY || null;
}

/** Supabase URL for server-side calls (inside Docker this is Kong on the internal network). */
export function serverSupabaseUrl(): string {
  return process.env.SUPABASE_INTERNAL_URL || publicEnv.supabaseUrl;
}

export function parseSecretKey(raw: string | undefined): Uint8Array | null {
  if (!raw) return null;
  try {
    const arr: unknown = JSON.parse(raw);
    if (!Array.isArray(arr) || arr.length !== 64 || !arr.every((n) => Number.isInteger(n) && n >= 0 && n <= 255)) return null;
    return Uint8Array.from(arr as number[]);
  } catch {
    return null;
  }
}

export type SolConfig = { ok: true; authoritySecret: Uint8Array; authority: string; recipient: string } | { ok: false; reason: string };

let cached: SolConfig | null = null;

/**
 * SOL purchase config:
 *  - MINT_AUTHORITY_SECRET_KEY (server secret): co-signs prepared transactions and is the Core
 *    update authority. It never pays fees or rent, so it needs no SOL.
 *  - NEXT_PUBLIC_PAYMENT_RECIPIENT (public): where payments go. No secret key needed on this server.
 */
export function solConfig(): SolConfig {
  if (cached) return cached;
  const secret = parseSecretKey(process.env.MINT_AUTHORITY_SECRET_KEY);
  if (!secret) return (cached = { ok: false, reason: "MINT_AUTHORITY_SECRET_KEY missing or malformed" });
  let authority: string;
  try {
    authority = Keypair.fromSecretKey(secret).publicKey.toBase58();
  } catch {
    return (cached = { ok: false, reason: "MINT_AUTHORITY_SECRET_KEY is not a valid ed25519 keypair" });
  }
  const recipient = publicEnv.paymentRecipient;
  if (!recipient) return (cached = { ok: false, reason: "NEXT_PUBLIC_PAYMENT_RECIPIENT missing" });
  try {
    new PublicKey(recipient);
  } catch {
    return (cached = { ok: false, reason: "NEXT_PUBLIC_PAYMENT_RECIPIENT is not a valid public key" });
  }
  if (recipient === authority) return (cached = { ok: false, reason: "NEXT_PUBLIC_PAYMENT_RECIPIENT must not be the mint authority" });
  return (cached = { ok: true, authoritySecret: secret, authority, recipient });
}
