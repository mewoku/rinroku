import "server-only";

import { NextResponse, type NextRequest } from "next/server";
import { dayNumber } from "@ronriku/core";
import { publicEnv } from "../env";
import { solConfig } from "./env";
import type { PurchaseConfig } from "./purchase";
import { TokenBucket } from "./rateLimit";
import { getServiceSupabase } from "../supabase/server";

/** Shared plumbing for /api/purchase/sol (verify) and /api/purchase/sol/prepare. */
export const MAX_BODY_BYTES = 2048;
// Without a trusted proxy all callers share the "direct" key, so size that bucket for a whole
// (local) deployment; the per-user buckets are the real per-player limits.
export const ipLimiter = new TokenBucket(process.env.TRUSTED_PROXY === "1" ? 10 : 60, process.env.TRUSTED_PROXY === "1" ? 1 / 6 : 1);
export const userLimiter = new TokenBucket(5, 1 / 12);
export const prepareLimiter = new TokenBucket(10, 1 / 6);

export function json(body: unknown, status = 200, headers: Record<string, string> = {}) {
  return NextResponse.json(body, { status, headers: { "cache-control": "no-store", ...headers } });
}

export function limited(r: { retryAfterSeconds: number }) {
  return json({ error: "Too many requests.", code: "rate_limited" }, 429, { "retry-after": String(r.retryAfterSeconds) });
}

export type EnabledConfig = PurchaseConfig & { enabled: true; authoritySecret: Uint8Array; authority: string };

export function purchaseConfig(): EnabledConfig | { enabled: false; reason: string } {
  const c = solConfig();
  if (!c.ok) return { enabled: false, reason: c.reason };
  if (!getServiceSupabase()) return { enabled: false, reason: "SUPABASE_SERVICE_ROLE_KEY missing" };
  return { enabled: true, authoritySecret: c.authoritySecret, authority: c.authority, recipient: c.recipient, siteUrl: publicEnv.siteUrl, today: dayNumber(), now: new Date() };
}

export async function authUser(req: NextRequest): Promise<string | null> {
  const token = req.headers.get("authorization")?.match(/^Bearer\s+(\S{20,4096})$/)?.[1];
  const db = getServiceSupabase();
  if (!token || !db) return null;
  const { data, error } = await db.auth.getUser(token);
  return error || !data.user ? null : data.user.id;
}

/** Content-type check + size-capped body read + JSON parse. */
export async function readJson(req: NextRequest): Promise<{ ok: true; value: unknown } | { ok: false; status: number; error: string }> {
  if (!(req.headers.get("content-type") ?? "").includes("application/json")) return { ok: false, status: 415, error: "Expected application/json." };
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
  try {
    return { ok: true, value: JSON.parse(new TextDecoder().decode(Buffer.concat(chunks))) };
  } catch {
    return { ok: false, status: 400, error: "Invalid JSON." };
  }
}
