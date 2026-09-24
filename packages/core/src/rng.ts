/**
 * Port of Ronriku.Domain.DeterministicRandom (C#). Bit-exact with the C# struct:
 *   state = seed == 0 ? 0x9E3779B97F4A7C15 : seed            (unsigned 64-bit)
 *   nextInt(n): v = state; v ^= v >> 12; v ^= v << 25; v ^= v >> 27; state = v;
 *               return (v * 2685821657736338717) mod 2^64 mod n
 */
export const MASK64 = (1n << 64n) - 1n;

/** Reinterpret any bigint as unsigned 64-bit (two's complement wrap), like a C# (ulong) cast. */
export const u64 = (value: bigint): bigint => BigInt.asUintN(64, value);

/** Reinterpret any bigint as signed 64-bit, like a C# (long) cast. */
export const i64 = (value: bigint): bigint => BigInt.asIntN(64, value);

export class DeterministicRandom {
  private state: bigint;

  constructor(seed: bigint) {
    const s = u64(seed);
    this.state = s === 0n ? 0x9e3779b97f4a7c15n : s;
  }

  nextInt(exclusiveMax: number): number {
    if (!Number.isInteger(exclusiveMax) || exclusiveMax <= 0) throw new RangeError("exclusiveMax must be > 0");
    let v = this.state;
    v ^= v >> 12n;
    v = (v ^ (v << 25n)) & MASK64;
    v ^= v >> 27n;
    this.state = v;
    return Number(((v * 2685821657736338717n) & MASK64) % BigInt(exclusiveMax));
  }
}

/** FNV-1a 64 over UTF-16 code units (C# iterates `char`), returned unsigned. */
export function fnv1a64(text: string): bigint {
  let hash = 14695981039346656037n;
  for (let i = 0; i < text.length; i++) {
    hash ^= BigInt(text.charCodeAt(i));
    hash = (hash * 1099511628211n) & MASK64;
  }
  return hash;
}

/**
 * Incremental FNV-1a 64 fed with the 8 little-endian bytes of signed 64-bit values; used by the
 * puzzle ContentHash functions. `hex()` matches C# `hash.ToString("x16")`.
 */
export class Fnv64Hasher {
  private hash = 14695981039346656037n;

  add(value: number | bigint): this {
    const raw = u64(BigInt(value));
    for (let i = 0n; i < 8n; i++) {
      this.hash ^= (raw >> (i * 8n)) & 0xffn;
      this.hash = (this.hash * 1099511628211n) & MASK64;
    }
    return this;
  }

  hex(): string {
    return this.hash.toString(16).padStart(16, "0");
  }
}
