/**
 * wallet-link: proves ownership of a Solana wallet with a signed nonce and stores it as
 * profiles.wallet_address.
 *
 *   POST { "action": "nonce" }                                  (Authorization: Bearer <user JWT>)
 *     → { nonce, expires_at, message }   message = walletLinkMessage(user, nonce, expires_at)
 *   POST { "action": "verify", "address": "<base58 pubkey>", "signature": "<base58 64-byte sig>" }
 *     → { wallet_address }   ed25519 signature of the UTF-8 message bytes, verified with tweetnacl.
 *
 * Nonces live 5 minutes and are single-use (consumed by an atomic DELETE … RETURNING).
 * The message format (domain line, SIWS style) is documented in ../_shared/wallet-message.ts. Errors: 400 bad_request, 401 unauthorized,
 * 404 no_nonce, 410 nonce_expired, 422 bad_signature, 409 wallet_in_use.
 */
import { createClient } from "npm:@supabase/supabase-js@2";
import nacl from "npm:tweetnacl@1.0.3";
import bs58 from "npm:bs58@6.0.0";
import { DEFAULT_WALLET_LINK_DOMAIN, walletLinkMessage } from "../_shared/wallet-message.ts";

const NONCE_TTL_MS = 5 * 60 * 1000;
const DOMAIN = Deno.env.get("WALLET_LINK_DOMAIN") ?? DEFAULT_WALLET_LINK_DOMAIN;
const cors = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { ...cors, "Content-Type": "application/json" } });

function decode58(value: unknown, length: number): Uint8Array | null {
  if (typeof value !== "string" || value.length > 128) return null;
  try {
    const bytes = bs58.decode(value);
    return bytes.length === length ? bytes : null;
  } catch {
    return null;
  }
}

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: cors });
  if (req.method !== "POST") return json(405, { error: "method_not_allowed" });

  const url = Deno.env.get("SUPABASE_URL")!;
  const admin = createClient(url, Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!, { auth: { persistSession: false } });
  const jwt = (req.headers.get("Authorization") ?? "").replace(/^Bearer\s+/i, "");
  const { data: auth, error: authError } = await admin.auth.getUser(jwt);
  if (authError || !auth.user) return json(401, { error: "unauthorized" });
  const userId = auth.user.id;

  let body: { action?: string; address?: unknown; signature?: unknown };
  try {
    body = await req.json();
  } catch {
    return json(400, { error: "bad_request" });
  }

  if (body.action === "nonce") {
    const nonce = Array.from(crypto.getRandomValues(new Uint8Array(16)), (b) => b.toString(16).padStart(2, "0")).join("");
    const expiresAt = new Date(Date.now() + NONCE_TTL_MS).toISOString();
    const { error } = await admin.from("wallet_nonces").upsert({ user_id: userId, nonce, expires_at: expiresAt });
    if (error) return json(500, { error: "server_error" });
    return json(200, { nonce, expires_at: expiresAt, message: walletLinkMessage(DOMAIN, userId, nonce, expiresAt) });
  }

  if (body.action === "verify") {
    const publicKey = decode58(body.address, 32);
    const signature = decode58(body.signature, 64);
    if (!publicKey || !signature) return json(400, { error: "bad_request" });

    const { data: row } = await admin.from("wallet_nonces").select("nonce, expires_at").eq("user_id", userId).maybeSingle();
    if (!row) return json(404, { error: "no_nonce" });
    // Re-render the timestamp the same way it was issued (ISO from Date).
    const expiresAt = new Date(row.expires_at).toISOString();
    if (Date.parse(expiresAt) < Date.now()) return json(410, { error: "nonce_expired" });

    const message = new TextEncoder().encode(walletLinkMessage(DOMAIN, userId, row.nonce, expiresAt));
    if (!nacl.sign.detached.verify(message, signature, publicKey)) return json(422, { error: "bad_signature" });

    // Consume the nonce atomically: only the request that actually deletes it may link (true single use).
    const { data: consumed, error: consumeError } = await admin.from("wallet_nonces").delete()
      .eq("user_id", userId).eq("nonce", row.nonce).select("nonce");
    if (consumeError) return json(500, { error: "server_error" });
    if (!consumed || consumed.length === 0) return json(404, { error: "no_nonce" });
    const address = bs58.encode(publicKey);
    const { error } = await admin.from("profiles").update({ wallet_address: address }).eq("id", userId);
    if (error) return json(error.code === "23505" ? 409 : 500, { error: error.code === "23505" ? "wallet_in_use" : "server_error" });
    return json(200, { wallet_address: address });
  }

  return json(400, { error: "bad_request" });
});
