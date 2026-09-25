import type { NextRequest } from "next/server";
import { clientKey } from "@/lib/server/rateLimit";
import { PurchaseError } from "@/lib/server/purchase";
import { authUser, ipLimiter, json, limited, prepareLimiter, purchaseConfig, readJson } from "@/lib/server/purchaseHttp";
import { getServiceSupabase } from "@/lib/supabase/server";
import { purchasePrepareSchema, zodMessage } from "@/lib/validation";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

/**
 * POST { item, refs } (auth) → { ok, transaction (base64, partially signed), lastValidBlockHeight,
 * lamports, wallet, recipient, figureId, assetAddress }. Runs every pre-check; the buyer's wallet
 * signs as fee payer and broadcasts, then POSTs the signature to /api/purchase/sol.
 */
export async function POST(req: NextRequest) {
  const rl = ipLimiter.take(clientKey(req.headers));
  if (!rl.ok) return limited(rl);
  const raw = await readJson(req);
  if (!raw.ok) return json({ ok: false, error: raw.error }, raw.status);
  const parsed = purchasePrepareSchema.safeParse(raw.value);
  if (!parsed.success) return json({ ok: false, code: "invalid", error: zodMessage(parsed.error) }, 400);

  const cfg = purchaseConfig();
  if (!cfg.enabled) return json({ ok: false, code: "disabled", error: "SOL purchases are not configured on this server." }, 503);
  const userId = await authUser(req);
  if (!userId) return json({ ok: false, code: "unauthenticated", error: "Sign in first." }, 401);
  const url = prepareLimiter.take(`user:${userId}`);
  if (!url.ok) return limited(url);

  const { prepareSolPurchase } = await import("@/lib/server/purchase");
  const { supabasePurchaseStore } = await import("@/lib/server/purchaseStore");
  const { solanaChain } = await import("@/lib/server/mint");
  try {
    const p = await prepareSolPurchase(supabasePurchaseStore(getServiceSupabase()!), solanaChain(cfg.authoritySecret), cfg, userId, parsed.data);
    return json({ ok: true, ...p });
  } catch (e) {
    if (e instanceof PurchaseError) return json({ ok: false, code: e.code, error: e.message }, e.status);
    console.error("purchase/sol/prepare failed", e);
    return json({ ok: false, code: "error", error: "Could not prepare this purchase — nothing was charged." }, 500);
  }
}
