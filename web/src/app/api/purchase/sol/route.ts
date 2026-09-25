import type { NextRequest } from "next/server";
import { clientKey } from "@/lib/server/rateLimit";
import { PurchaseError } from "@/lib/server/purchase";
import { authUser, ipLimiter, json, limited, purchaseConfig, readJson, userLimiter } from "@/lib/server/purchaseHttp";
import { getServiceSupabase } from "@/lib/supabase/server";
import { purchaseQuoteSchema, purchaseSolBodySchema, zodMessage } from "@/lib/validation";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const inflight = new Set<string>();

/**
 * GET                     → { enabled, recipient, authority, cluster } (no auth).
 * GET ?item=…&refs (auth) → pre-payment quote: { ok, lamports, wallet, recipient } or { ok:false, code, error }.
 * Paying happens only through a transaction from POST /api/purchase/sol/prepare.
 */
export async function GET(req: NextRequest) {
  const cfg = purchaseConfig();
  const params = Object.fromEntries(req.nextUrl.searchParams);
  if (!Object.keys(params).length) {
    return json({ enabled: cfg.enabled, recipient: cfg.enabled ? cfg.recipient : null, authority: cfg.enabled ? cfg.authority : null, cluster: "devnet" });
  }
  const rl = ipLimiter.take(clientKey(req.headers));
  if (!rl.ok) return limited(rl);
  if (!cfg.enabled) return json({ ok: false, code: "disabled", error: "SOL purchases are not configured on this server." }, 503);
  const parsed = purchaseQuoteSchema.safeParse(params);
  if (!parsed.success) return json({ ok: false, code: "invalid", error: zodMessage(parsed.error) }, 400);
  const userId = await authUser(req);
  if (!userId) return json({ ok: false, code: "unauthenticated", error: "Sign in first." }, 401);
  const { quote } = await import("@/lib/server/purchase");
  const { supabasePurchaseStore } = await import("@/lib/server/purchaseStore");
  try {
    const q = await quote(supabasePurchaseStore(getServiceSupabase()!), cfg, userId, parsed.data);
    return json({ ok: true, lamports: q.lamports, wallet: q.buyer, recipient: cfg.recipient });
  } catch (e) {
    if (e instanceof PurchaseError) return json({ ok: false, code: e.code, error: e.message }, e.status);
    console.error("purchase/sol quote failed", e);
    return json({ ok: false, code: "error", error: "Could not check this purchase." }, 500);
  }
}

/** Verify a landed, server-prepared purchase transaction by signature and record what it bought. */
export async function POST(req: NextRequest) {
  const rl = ipLimiter.take(clientKey(req.headers));
  if (!rl.ok) return limited(rl);

  const raw = await readJson(req);
  if (!raw.ok) return json({ error: raw.error }, raw.status);
  const parsed = purchaseSolBodySchema.safeParse(raw.value);
  if (!parsed.success) return json({ error: zodMessage(parsed.error) }, 400);
  const body = parsed.data;

  if (!req.headers.get("authorization")) return json({ error: "Sign in first (missing bearer token)." }, 401);
  const cfg = purchaseConfig();
  if (!cfg.enabled) return json({ error: "SOL purchases are not configured on this server." }, 503);
  const userId = await authUser(req);
  if (!userId) return json({ error: "Invalid session." }, 401);
  const url = userLimiter.take(`user:${userId}`);
  if (!url.ok) return limited(url);

  if (inflight.has(body.signature)) return json({ error: "This payment is already being processed.", code: "in_progress" }, 409);
  inflight.add(body.signature);
  try {
    // Lazy imports keep web3 / Umi out of the cold path for rejected requests.
    const { processSolPurchase } = await import("@/lib/server/purchase");
    const { supabasePurchaseStore } = await import("@/lib/server/purchaseStore");
    const { solanaChain } = await import("@/lib/server/mint");
    try {
      const out = await processSolPurchase(supabasePurchaseStore(getServiceSupabase()!), solanaChain(cfg.authoritySecret), cfg, userId, body);
      return json(out);
    } catch (e) {
      if (e instanceof PurchaseError) return json({ error: e.message, code: e.code }, e.status);
      console.error("purchase/sol failed", e);
      return json({ error: "Purchase failed — retry with the same payment.", code: "error" }, 500);
    }
  } finally {
    inflight.delete(body.signature);
  }
}
