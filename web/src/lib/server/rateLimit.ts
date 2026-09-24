import "server-only";

/**
 * In-memory token bucket. Good enough for a single local Next.js process; swap for Redis/Upstash
 * or a Supabase-backed limiter before running multiple instances.
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
    if (!b) {
      if (this.buckets.size >= this.maxKeys) this.buckets.clear();
      b = { tokens: this.capacity, updated: now };
      this.buckets.set(key, b);
    }
    b.tokens = Math.min(this.capacity, b.tokens + ((now - b.updated) / 1000) * this.refillPerSecond);
    b.updated = now;
    if (b.tokens >= 1) {
      b.tokens -= 1;
      return { ok: true, retryAfterSeconds: 0 };
    }
    return { ok: false, retryAfterSeconds: Math.ceil((1 - b.tokens) / this.refillPerSecond) };
  }
}

export function clientIp(headers: Headers): string {
  return headers.get("x-forwarded-for")?.split(",")[0]?.trim() || headers.get("x-real-ip") || "local";
}
