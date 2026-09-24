import { NextResponse, type NextRequest } from "next/server";
import { publicEnv } from "@/lib/env";
import { clientIp, TokenBucket } from "@/lib/server/rateLimit";
import { treasurySecretKey } from "@/lib/server/env";
import { getServiceSupabase } from "@/lib/supabase/server";
import { purchaseSolBodySchema, zodMessage } from "@/lib/validation";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const MAX_BODY_BYTES = 2048;
// 5 requests burst, refilling 1 every 12 s, per IP.
const limiter = new TokenBucket(5, 1 / 12);
const inflight = new Set<string>();

function json(body: unknown, status = 200, headers: Record<string, string> = {}) {
  return NextResponse.json(body, { status, headers: { "cache-control": "no-store", ...headers } });
}

/** Lets the client check the route can fulfil a purchase *before* it sends any SOL. */
export async function GET() {
  const enabled = !!getServiceSupabase() && !!treasurySecretKey() && !!publicEnv.treasuryPubkey;
  return json({ enabled, treasury: enabled ? publicEnv.treasuryPubkey : null, cluster: "devnet" });
}

export async function POST(req: NextRequest) {
  const rl = limiter.take(clientIp(req.headers));
  if (!rl.ok) return json({ error: "Too many requests." }, 429, { "retry-after": String(rl.retryAfterSeconds) });

  if (!(req.headers.get("content-type") ?? "").includes("application/json")) return json({ error: "Expected application/json." }, 415);
  const raw = await req.text();
  if (raw.length > MAX_BODY_BYTES) return json({ error: "Body too large." }, 413);
  let payload: unknown;
  try {
    payload = JSON.parse(raw);
  } catch {
    return json({ error: "Invalid JSON." }, 400);
  }
  const parsed = purchaseSolBodySchema.safeParse(payload);
  if (!parsed.success) return json({ error: zodMessage(parsed.error) }, 400);
  const body = parsed.data;

  const token = req.headers.get("authorization")?.match(/^Bearer\s+(\S{20,4096})$/)?.[1];
  if (!token) return json({ error: "Sign in first (missing bearer token)." }, 401);

  const db = getServiceSupabase();
  const secret = treasurySecretKey();
  if (!db || !secret || !publicEnv.treasuryPubkey) return json({ error: "SOL purchases are not configured on this server." }, 503);

  const { data: auth, error: authErr } = await db.auth.getUser(token);
  if (authErr || !auth.user) return json({ error: "Invalid session." }, 401);

  if (inflight.has(body.signature)) return json({ error: "This payment is already being processed." }, 409);
  inflight.add(body.signature);
  try {
    // Lazy import keeps Umi / web3 out of the cold path for rejected requests.
    const { processSolPurchase, PurchaseError } = await import("@/lib/server/purchase");
    try {
      const out = await processSolPurchase(db, secret, auth.user.id, body);
      return json(out);
    } catch (e) {
      if (e instanceof PurchaseError) return json({ error: e.message }, e.status);
      console.error("purchase/sol failed", e);
      return json({ error: "Purchase failed." }, 500);
    }
  } finally {
    inflight.delete(body.signature);
  }
}
