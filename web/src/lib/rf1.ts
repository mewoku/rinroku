/**
 * RF1 figure encoding (PLAN_V2 §5): `RF1.{S}.{palette 15×RRGGBB}.{base64url nibble-packed voxels}`.
 * Voxels: S (x) × S (y, 0 = front) × 2S (z, up); x fastest, then y, then z; high nibble first.
 *
 * Small dependency-free decoder mirroring client/.../Domain/Figures/Figure.cs `Decode`, so the
 * viewer works independently of @ronriku/core. Verified against packages/core/fixtures in tests.
 */
export const PALETTE_SIZE = 15;

export interface DecodedFigure {
  size: number;
  height: number;
  /** 15 RGB ints (0xRRGGBB); voxel value v (1..15) → palette[v - 1]. */
  palette: number[];
  voxels: Uint8Array;
}

export class Rf1FormatError extends Error {}

const B64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
const B64_LOOKUP = new Map<string, number>([...B64].map((c, i) => [c, i]));

export function base64UrlDecode(s: string): Uint8Array {
  const clean = s.replace(/=+$/, "");
  const out = new Uint8Array(Math.floor((clean.length * 3) / 4));
  let buffer = 0;
  let bits = 0;
  let o = 0;
  for (const ch of clean) {
    const v = B64_LOOKUP.get(ch);
    if (v === undefined) throw new Rf1FormatError("Invalid base64url character.");
    buffer = ((buffer << 6) | v) & 0xffffff;
    bits += 6;
    if (bits >= 8) {
      bits -= 8;
      out[o++] = (buffer >> bits) & 0xff;
    }
  }
  return out.subarray(0, o);
}

export function decodeRf1(encoding: string): DecodedFigure {
  const parts = (encoding ?? "").split(".");
  if (parts.length !== 4 || parts[0] !== "RF1") throw new Rf1FormatError("Not an RF1 figure.");
  const size = Number(parts[1]);
  const paletteHex = parts[2] ?? "";
  if (
    !Number.isInteger(size) ||
    size < 3 ||
    size > 5 ||
    paletteHex.length !== PALETTE_SIZE * 6 ||
    !/^[0-9A-Fa-f]+$/.test(paletteHex)
  ) {
    throw new Rf1FormatError("Bad figure header.");
  }
  const palette: number[] = [];
  for (let i = 0; i < PALETTE_SIZE; i++) palette.push(parseInt(paletteHex.slice(i * 6, i * 6 + 6), 16));
  const height = size * 2;
  const count = size * size * height;
  const packed = base64UrlDecode(parts[3] ?? "");
  if (packed.length !== Math.ceil(count / 2)) throw new Rf1FormatError("Voxel payload length mismatch.");
  const voxels = new Uint8Array(count);
  for (let i = 0; i < count; i++) {
    const b = packed[i >> 1]!;
    voxels[i] = i % 2 === 0 ? b >> 4 : b & 0xf;
  }
  return { size, height, palette, voxels };
}

export function isRf1(encoding: string): boolean {
  try {
    decodeRf1(encoding);
    return true;
  } catch {
    return false;
  }
}

export function voxelAt(f: DecodedFigure, x: number, y: number, z: number): number {
  if (x < 0 || y < 0 || z < 0 || x >= f.size || y >= f.size || z >= f.height) return 0;
  return f.voxels[x + f.size * (y + f.size * z)]!;
}

export function filledCount(f: DecodedFigure): number {
  let n = 0;
  for (const v of f.voxels) if (v !== 0) n++;
  return n;
}
