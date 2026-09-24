/**
 * Port of Ronriku.Domain.Figures.Figure (C#). A voxel person: size (x) × size (y, front = 0) × 2·size
 * (z, up). Voxel 0 = empty, 1–15 = index into `palette` (15 RGB entries, 0xRRGGBB).
 */
export type FigureRarity = "Common" | "Rare" | "Epic" | "Legendary";
export const FIGURE_RARITIES: readonly FigureRarity[] = ["Common", "Rare", "Epic", "Legendary"];

export const PALETTE_SIZE = 15;

export interface FigureTraits {
  skin: number;
  outfit: number;
  accent: number;
  hair: number;
  pants: number;
  shoes: number;
  headwear: number;
  eyes: number;
  outfitStyle: number;
  accessory: number;
  glow: boolean;
}

export class Figure {
  readonly size: number;
  readonly palette: number[];
  readonly voxels: Uint8Array;
  readonly name: string;
  readonly rarity: FigureRarity;
  /** Undefined for decoded figures (traits are not part of the encoding). */
  readonly traits?: FigureTraits;

  constructor(size: number, palette: number[], voxels: Uint8Array, name = "", rarity: FigureRarity = "Common", traits?: FigureTraits) {
    if (!Number.isInteger(size) || size < 3 || size > 5) throw new RangeError("size must be 3..5");
    if (palette.length !== PALETTE_SIZE) throw new Error("Palette must have 15 colours.");
    if (voxels.length !== size * size * size * 2) throw new Error("Voxel count mismatch.");
    this.size = size;
    this.palette = palette;
    this.voxels = voxels;
    this.name = name;
    this.rarity = rarity;
    this.traits = traits;
  }

  get height(): number {
    return this.size * 2;
  }

  index(x: number, y: number, z: number): number {
    return x + this.size * (y + this.size * z);
  }

  get(x: number, y: number, z: number): number {
    return this.voxels[this.index(x, y, z)];
  }

  get filledCount(): number {
    let count = 0;
    for (const v of this.voxels) if (v !== 0) count++;
    return count;
  }

  /** `RF1.{S}.{palette 90 hex}.{base64url nibble-packed voxels}`. See docs/PLAN_V2.md §5. */
  encode(): string {
    const hex = this.palette.map((rgb) => (rgb & 0xffffff).toString(16).toUpperCase().padStart(6, "0")).join("");
    const packed = new Uint8Array((this.voxels.length + 1) >> 1);
    for (let i = 0; i < this.voxels.length; i++) {
      const nibble = this.voxels[i] & 0xf;
      packed[i >> 1] |= i % 2 === 0 ? nibble << 4 : nibble;
    }
    return `RF1.${this.size}.${hex}.${toBase64Url(packed)}`;
  }

  /** Decodes geometry and palette. Name, rarity and traits are not part of the encoding. */
  static decode(encoding: string): Figure {
    const parts = (encoding ?? "").split(".");
    if (parts.length !== 4 || parts[0] !== "RF1") throw new Error("Not an RF1 figure.");
    if (!/^[345]$/.test(parts[1]) || !/^[0-9A-Fa-f]{90}$/.test(parts[2])) throw new Error("Bad figure header.");
    if (!/^[A-Za-z0-9_-]*$/.test(parts[3])) throw new Error("Bad voxel payload.");
    const size = Number(parts[1]);
    const palette: number[] = [];
    for (let i = 0; i < PALETTE_SIZE; i++) palette.push(parseInt(parts[2].substring(i * 6, i * 6 + 6), 16));
    const packed = fromBase64Url(parts[3]);
    const voxels = new Uint8Array(size * size * size * 2);
    if (packed.length !== (voxels.length + 1) >> 1) throw new Error("Voxel payload length mismatch.");
    for (let i = 0; i < voxels.length; i++) voxels[i] = i % 2 === 0 ? packed[i >> 1] >> 4 : packed[i >> 1] & 0xf;
    return new Figure(size, palette, voxels);
  }

  /** MagicaVoxel .vox (version 150): SIZE, XYZI and RGBA chunks. Palette index n maps to vox colour n. */
  toVox(): Uint8Array {
    const entries: number[][] = [];
    for (let z = 0; z < this.height; z++)
      for (let y = 0; y < this.size; y++)
        for (let x = 0; x < this.size; x++) {
          const v = this.get(x, y, z);
          // MagicaVoxel's y axis points away from the viewer; our y = 0 is the front.
          if (v !== 0) entries.push([x, this.size - 1 - y, z, v]);
        }

    const sizeContent = int32s(this.size, this.size, this.height);
    const xyzi = new Uint8Array(4 + entries.length * 4);
    new DataView(xyzi.buffer).setInt32(0, entries.length, true);
    entries.forEach((e, i) => xyzi.set(e, 4 + i * 4));
    const rgba = new Uint8Array(256 * 4);
    for (let i = 0; i < 255; i++) {
      const rgb = i < PALETTE_SIZE ? this.palette[i] : 0x808080;
      rgba[i * 4] = (rgb >> 16) & 0xff;
      rgba[i * 4 + 1] = (rgb >> 8) & 0xff;
      rgba[i * 4 + 2] = rgb & 0xff;
      rgba[i * 4 + 3] = 255;
    }
    const children = concat(chunk("SIZE", sizeContent), chunk("XYZI", xyzi), chunk("RGBA", rgba));
    return concat(ascii("VOX "), int32s(150), ascii("MAIN"), int32s(0, children.length), children);
  }
}

const B64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

/** base64url without padding (no Buffer dependency, so it also runs in browsers and Deno). */
export function toBase64Url(bytes: Uint8Array): string {
  let out = "";
  for (let i = 0; i < bytes.length; i += 3) {
    const n = (bytes[i] << 16) | ((bytes[i + 1] ?? 0) << 8) | (bytes[i + 2] ?? 0);
    const chars = Math.min(4, Math.ceil(((bytes.length - i) * 8) / 6));
    for (let c = 0; c < chars; c++) out += B64[(n >> (18 - c * 6)) & 63];
  }
  return out;
}

export function fromBase64Url(text: string): Uint8Array {
  const out: number[] = [];
  let buffer = 0,
    bits = 0;
  for (const ch of text) {
    const v = B64.indexOf(ch);
    if (v < 0) throw new Error("Bad base64url character.");
    buffer = ((buffer << 6) | v) & 0xffffff;
    bits += 6;
    if (bits >= 8) {
      bits -= 8;
      out.push((buffer >> bits) & 0xff);
    }
  }
  return Uint8Array.from(out);
}

function ascii(text: string): Uint8Array {
  return Uint8Array.from(text, (c) => c.charCodeAt(0));
}

function int32s(...values: number[]): Uint8Array {
  const out = new Uint8Array(values.length * 4);
  const view = new DataView(out.buffer);
  values.forEach((v, i) => view.setInt32(i * 4, v, true));
  return out;
}

function chunk(id: string, content: Uint8Array): Uint8Array {
  return concat(ascii(id), int32s(content.length, 0), content);
}

function concat(...parts: Uint8Array[]): Uint8Array {
  const out = new Uint8Array(parts.reduce((n, p) => n + p.length, 0));
  let offset = 0;
  for (const p of parts) {
    out.set(p, offset);
    offset += p.length;
  }
  return out;
}
