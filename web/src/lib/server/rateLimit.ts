import "server-only";

/**
 * In-memory token bucket. Good enough for a single Next.js process; use Redis/Upstash or a
 * Supabase-backed limiter before running several instances.
 */
interface Bucket {
  tokens: number;
  updated: number;
}

export class TokenBucket {
  private buckets = new Map<string, Bucket>();

  constructor(
    private readonly capacity: number,
    private readonly refillPerSecond: number,
    private readonly maxKeys = 10_000,
  ) {}

  take(key: string, now = Date.now()): { ok: boolean; retryAfterSeconds: number } {
    let b = this.buckets.get(key);
    if (b) {
      this.buckets.delete(key); // re-insert → Map order is least-recently-used first
    } else {
      b = { tokens: this.capacity, updated: now };
      while (this.buckets.size >= this.maxKeys) {
        const oldest = this.buckets.keys().next().value;
        if (oldest === undefined) break;
        this.buckets.delete(oldest); // evict LRU instead of clearing everyone's state
      }
    }
    this.buckets.set(key, b);
    b.tokens = Math.min(this.capacity, b.tokens + ((now - b.updated) / 1000) * this.refillPerSecond);
    b.updated = now;
    if (b.tokens >= 1) {
      b.tokens -= 1;
      return { ok: true, retryAfterSeconds: 0 };
    }
    return { ok: false, retryAfterSeconds: Math.ceil((1 - b.tokens) / this.refillPerSecond) };
  }

  get size(): number {
    return this.buckets.size;
  }
}

/**
 * Client key for rate limiting. Forwarding headers are client-controlled unless a reverse proxy we
 * trust overwrites them, so they are only honoured with TRUSTED_PROXY=1. Otherwise every request
 * shares one "direct" bucket (sized accordingly by the caller) and per-user buckets do the rest.
 */
export function clientKey(headers: Headers, env: Record<string, string | undefined> = process.env): string {
  if (env.TRUSTED_PROXY !== "1") return "direct";
  const ip = headers.get("x-real-ip")?.trim() || headers.get("x-forwarded-for")?.split(",").at(-1)?.trim();
  return ip ? `ip:${ip}` : "direct";
}
