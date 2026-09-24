import { NextResponse, type NextRequest } from "next/server";
import { dayNumber } from "@ronriku/core";
import { publicEnv } from "@/lib/env";
import { clientKey, TokenBucket } from "@/lib/server/rateLimit";
import { treasuryConfig } from "@/lib/server/env";
import { PurchaseError, type PurchaseConfig } from "@/lib/server/purchase";
import { getServiceSupabase } from "@/lib/supabase/server";
import { purchaseQuoteSchema, purchaseSolBodySchema, zodMessage } from "@/lib/validation";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const MAX_BODY_BYTES = 2048;
// Without a trusted proxy all callers share the "direct" key, so size that bucket for a whole
// (local) deployment; the per-user bucket below is the real per-player limit.
const ipLimiter = new TokenBucket(process.env.TRUSTED_PROXY === "1" ? 10 : 60, process.env.TRUSTED_PROXY === "1" ? 1 / 6 : 1);
const userLimiter = new TokenBucket(5, 1 / 12);
const inflight = new Set<string>();

function json(body: unknown, status = 200, headers: Record<string, string> = {}) {
  return NextResponse.json(body, { status, headers: { "cache-control": "no-store", ...headers } });
}

function limited(r: { retryAfterSeconds: number }) {
  return json({ error: "Too many requests.", code: "rate_limited" }, 429, { "retry-after": String(r.retryAfterSeconds) });
}

function config(): PurchaseConfig & { enabled: true; secret: Uint8Array } | { enabled: false; reason: string } {
  const t = treasuryConfig();
  if (!t.ok) return { enabled: false, reason: t.reason };
  if (!getServiceSupabase()) return { enabled: false, reason: "SUPABASE_SERVICE_ROLE_KEY missing" };
  return { enabled: true, secret: t.secret, treasury: t.pubkey, siteUrl: publicEnv.siteUrl, today: dayNumber(), now: new Date() };
}

async function authUser(req: NextRequest): Promise<string | null> {
  const token = req.headers.get("authorization")?.match(/^Bearer\s+(\S{20,4096})$/)?.[1];
  const db = getServiceSupabase();
  if (!token || !db) return null;
  const { data, error } = await db.auth.getUser(token);
  return error || !data.user ? null : data.user.id;
}

async function readBody(req: NextRequest): Promise<{ ok: true; text: string } | { ok: false; status: number; error: string }> {
  const declared = Number(req.headers.get("content-length") ?? "NaN");
  if (Number.isFinite(declared) && declared > MAX_BODY_BYTES) return { ok: false, status: 413, error: "Body too large." };
  if (!req.body) return { ok: false, status: 400, error: "Empty body." };
  const reader = req.body.getReader();
  const chunks: Uint8Array[] = [];
  let size = 0;
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    size += value.byteLength;
    if (size > MAX_BODY_BYTES) {
      await reader.cancel();
      return { ok: false, status: 413, error: "Body too large." };
    }
    chunks.push(value);
  }
  return { ok: true, text: new TextDecoder().decode(Buffer.concat(chunks)) };
}

/**
 * GET                     → { enabled, treasury } (no auth).
 * GET ?item=…&refs (auth) → pre-payment quote: { ok, lamports, wallet } or { ok:false, code, error }.
 * The client must get ok:true — and pay from `wallet` exactly `lamports` — before sending any SOL.
 */
export async function GET(req: NextRequest) {
  const cfg = config();
  const params = Object.fromEntries(req.nextUrl.searchParams);
  if (!Object.keys(params).length) return json({ enabled: cfg.enabled, treasury: cfg.enabled ? cfg.treasury : null, cluster: "devnet" });
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
    return json({ ok: true, lamports: q.lamports, wallet: q.buyer, treasury: cfg.treasury });
  } catch (e) {
    if (e instanceof PurchaseError) return json({ ok: false, code: e.code, error: e.message }, e.status);
    console.error("purchase/sol quote failed", e);
    return json({ ok: false, code: "error", error: "Could not check this purchase." }, 500);
  }
}

export async function POST(req: NextRequest) {
  const rl = ipLimiter.take(clientKey(req.headers));
  if (!rl.ok) return limited(rl);

  if (!(req.headers.get("content-type") ?? "").includes("application/json")) return json({ error: "Expected application/json." }, 415);
  const raw = await readBody(req);
  if (!raw.ok) return json({ error: raw.error }, raw.status);
  let payload: unknown;
  try {
    payload = JSON.parse(raw.text);
  } catch {
    return json({ error: "Invalid JSON." }, 400);
  }
  const parsed = purchaseSolBodySchema.safeParse(payload);
  if (!parsed.success) return json({ error: zodMessage(parsed.error) }, 400);
  const body = parsed.data;

  if (!req.headers.get("authorization")) return json({ error: "Sign in first (missing bearer token)." }, 401);
  const cfg = config();
  if (!cfg.enabled) return json({ error: "SOL purchases are not configured on this server." }, 503);
  const userId = await authUser(req);
  if (!userId) return json({ error: "Invalid session." }, 401);
  const url = userLimiter.take(`user:${userId}`);
  if (!url.ok) return limited(url);

  if (inflight.has(body.signature)) return json({ error: "This payment is already being processed.", code: "in_progress" }, 409);
  inflight.add(body.signature);
  try {
    // Lazy imports keep Umi / web3 out of the cold path for rejected requests.
    const { processSolPurchase } = await import("@/lib/server/purchase");
    const { supabasePurchaseStore } = await import("@/lib/server/purchaseStore");
    const { solanaChain } = await import("@/lib/server/mint");
    try {
      const out = await processSolPurchase(supabasePurchaseStore(getServiceSupabase()!), solanaChain(cfg.secret), cfg, userId, body);
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
