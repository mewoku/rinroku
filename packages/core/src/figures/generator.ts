/**
 * Port of Ronriku.Domain.Figures.FigureGenerator (generator version 1). Same tables, same RNG call
 * order, same build order as the C# source of truth.
 *
 * RNG: new DeterministicRandom(seed * 31 + size + 1000 * kind)   (unsigned 64-bit wraparound)
 * Call order: skin r(8), outfit r(16), accent r(12), hair r(10), pants r(6), shoes r(3),
 *             headwear r(6), eyes r(4), outfitStyle r(4), accessory r(5), glowRoll r(100),
 *             glowColor r(4), name r(16) x3.
 */
import { DeterministicRandom, u64 } from "../rng.js";
import { Figure, PALETTE_SIZE, type FigureRarity, type FigureTraits } from "./figure.js";

export const GENERATOR_VERSION = 1;

/** Palette slots (voxel values). */
export const Slot = {
  Skin: 1, SkinShade: 2, Outfit: 3, OutfitShade: 4, Accent: 5, Eye: 6, Hair: 7, Hat: 8,
  Pants: 9, Shoes: 10, Mouth: 11, Glow: 12, Cheek: 13, Metal: 14, White: 15,
} as const;

export const Skins = [0xffd9b3, 0xf1c27d, 0xe0ac69, 0xc68642, 0x8d5524, 0x5c3a21, 0x9be35a, 0x8fe3ff];
export const MonsterSkins = [0x9be35a, 0x7b5cff, 0xff6b5a, 0x3a6bff, 0xff4fd8, 0x2f8f4e, 0xffb347, 0x5b2a86];
export const Outfits = [
  0x11c5b3, 0x135b73, 0xe8da37, 0xff4fd8, 0x7b5cff, 0xffb347, 0xff6b5a, 0x9be35a,
  0x2f8f4e, 0x8fe3ff, 0x3a6bff, 0xff3b5c, 0xffc83d, 0xe7e8e5, 0x2a2f3b, 0x5b2a86,
];
export const MonsterOutfits = [
  0x2a2f3b, 0x17181c, 0x3a0a14, 0x10301a, 0x0c2250, 0x2a1450, 0x4a1e12, 0x135b73,
  0x5b2a86, 0x2f8f4e, 0xff3b5c, 0x7b5cff, 0x1b1e27, 0x3b2a20, 0x0b3b4a, 0x8d5524,
];
export const Accents = [0xe8da37, 0x11c5b3, 0xff4fd8, 0xffb347, 0x8fe3ff, 0xff3b5c, 0x9be35a, 0xffffff, 0xffc83d, 0x7b5cff, 0x3a6bff, 0x17181c];
export const Hairs = [0x2b1b12, 0x5a3825, 0xa8672e, 0xe8c07d, 0xf4f1e8, 0x17181c, 0xff4fd8, 0x3a6bff, 0x9be35a, 0xff6b5a];
export const PantsColours = [0x2a2f3b, 0x1b1e27, 0x135b73, 0x3b2a20, 0x5b2a86, 0x17181c];
export const ShoeColours = [0x17181c, 0xe7e8e5, 0x8d5524];
export const GlowColours = [0x8fe3ff, 0xff4fd8, 0x9be35a, 0xffc83d];
export const Syllables = ["KA", "ZU", "MI", "RO", "NI", "TA", "SU", "KE", "YO", "RI", "HA", "MO", "NE", "TO", "LU", "VI"];

const HeadwearPoints = [0, 0, 1, 1, 3, 2];
const EyePoints = [0, 0, 2, 1];
const OutfitStylePoints = [0, 0, 0, 1];
const AccessoryPoints = [0, 1, 1, 1, 0];

/** Each channel x 3/4 (integer). */
export function shade(rgb: number): number {
  const r = Math.floor((((rgb >> 16) & 0xff) * 3) / 4);
  const g = Math.floor((((rgb >> 8) & 0xff) * 3) / 4);
  const b = Math.floor(((rgb & 0xff) * 3) / 4);
  return (r << 16) | (g << 8) | b;
}

export function rarityFromPoints(points: number): FigureRarity {
  return points >= 6 ? "Legendary" : points >= 4 ? "Epic" : points >= 2 ? "Rare" : "Common";
}

export function generateFigure(seed: bigint, size: number, monster = false): Figure {
  if (!Number.isInteger(size) || size < 3 || size > 5) throw new RangeError("size must be 3..5");
  const kind = monster ? 1n : 0n;
  const r = new DeterministicRandom(u64(u64(seed) * 31n + BigInt(size) + 1000n * kind));

  const skin = r.nextInt(8), outfit = r.nextInt(16), accent = r.nextInt(12), hair = r.nextInt(10);
  const pants = r.nextInt(6), shoes = r.nextInt(3), headwear = r.nextInt(6), eyes = r.nextInt(4);
  const outfitStyle = r.nextInt(4), accessory = r.nextInt(5);
  const glow = r.nextInt(100) < 6;
  const glowColour = r.nextInt(4);
  const name = Syllables[r.nextInt(16)] + Syllables[r.nextInt(16)] + Syllables[r.nextInt(16)];

  const skinRgb = (monster ? MonsterSkins : Skins)[skin];
  const outfitRgb = (monster ? MonsterOutfits : Outfits)[outfit];
  const palette = new Array<number>(PALETTE_SIZE).fill(0);
  palette[Slot.Skin - 1] = skinRgb;
  palette[Slot.SkinShade - 1] = shade(skinRgb);
  palette[Slot.Outfit - 1] = outfitRgb;
  palette[Slot.OutfitShade - 1] = shade(outfitRgb);
  palette[Slot.Accent - 1] = Accents[accent];
  palette[Slot.Eye - 1] = glow ? GlowColours[glowColour] : 0x17181c;
  palette[Slot.Hair - 1] = Hairs[hair];
  palette[Slot.Hat - 1] = Accents[(accent + 5) % Accents.length];
  palette[Slot.Pants - 1] = PantsColours[pants];
  palette[Slot.Shoes - 1] = ShoeColours[shoes];
  palette[Slot.Mouth - 1] = 0x8d3b3b;
  palette[Slot.Glow - 1] = GlowColours[glowColour];
  palette[Slot.Cheek - 1] = 0xff8a8a;
  palette[Slot.Metal - 1] = 0xffc83d;
  palette[Slot.White - 1] = 0xe7e8e5;

  const s = size, h = size * 2;
  const v = new Uint8Array(s * s * h);
  const idx = (x: number, y: number, z: number) => x + s * (y + s * z);
  const set = (x: number, y: number, z: number, value: number) => {
    v[idx(x, y, z)] = value;
  };
  const corner = (x: number, y: number) => (x === 0 || x === s - 1) && (y === 0 || y === s - 1);
  const rim = (x: number, y: number) => x === 0 || y === 0 || x === s - 1 || y === s - 1;

  const legsH = s - 2, headStart = 2 * s - 3, top = h - 1;

  // Legs.
  const legW = s >= 5 ? 2 : 1;
  const yFrom = 1, yTo = s === 3 ? 1 : s - 2;
  for (let z = 0; z < legsH; z++)
    for (let y = yFrom; y <= yTo; y++)
      for (let x = 0; x < s; x++) if (x < legW || x >= s - legW) set(x, y, z, z === 0 ? Slot.Shoes : Slot.Pants);

  // Torso with sleeves and hands.
  for (let z = legsH; z < headStart; z++)
    for (let y = 0; y < s; y++)
      for (let x = 0; x < s; x++) {
        if (s >= 4 && corner(x, y)) continue;
        const side = x === 0 || x === s - 1;
        set(x, y, z, side ? (z === legsH ? Slot.Skin : Slot.OutfitShade) : Slot.Outfit);
      }

  // Head.
  for (let z = headStart; z < h; z++)
    for (let y = 0; y < s; y++)
      for (let x = 0; x < s; x++) {
        if (s >= 4 && z === top && corner(x, y)) continue;
        set(x, y, z, Slot.Skin);
      }

  // Face.
  const eyeL = s === 3 ? 0 : 1, eyeR = s - 1 - eyeL, eyeZ = headStart + 1;
  switch (eyes) {
    case 0:
      set(eyeL, 0, eyeZ, Slot.Eye); set(eyeR, 0, eyeZ, Slot.Eye);
      break;
    case 1:
      set(eyeL, 0, eyeZ, Slot.Eye); set(eyeR, 0, eyeZ, Slot.Eye);
      set(eyeL, 0, eyeZ + 1, Slot.Eye); set(eyeR, 0, eyeZ + 1, Slot.Eye);
      break;
    case 2:
      for (let x = 0; x < s; x++) set(x, 0, eyeZ, Slot.Glow);
      break;
    default:
      set(eyeL, 0, eyeZ, Slot.Eye); set(eyeR, 0, eyeZ, Slot.Eye);
      set(eyeL, 0, headStart, Slot.Cheek); set(eyeR, 0, headStart, Slot.Cheek);
      break;
  }
  if (s >= 4 && eyes !== 2) set(s >> 1, 0, headStart, Slot.Mouth);

  // Headwear.
  switch (headwear) {
    case 1:
    case 2:
      for (let y = 0; y < s; y++) for (let x = 0; x < s; x++) if (v[idx(x, y, top)] !== 0) set(x, y, top, Slot.Hair);
      for (let z = headStart; z < h; z++) for (let x = 0; x < s; x++) if (v[idx(x, s - 1, z)] !== 0) set(x, s - 1, z, Slot.Hair);
      if (headwear === 2)
        for (let z = headStart + 1; z < h; z++)
          for (let y = 1; y < s; y++) {
            if (v[idx(0, y, z)] !== 0) set(0, y, z, Slot.Hair);
            if (v[idx(s - 1, y, z)] !== 0) set(s - 1, y, z, Slot.Hair);
          }
      break;
    case 3:
      for (let y = 0; y < s; y++)
        for (let x = 0; x < s; x++) if (v[idx(x, y, top)] !== 0) set(x, y, top, y === 0 ? Slot.Outfit : Slot.Hat);
      break;
    case 4:
      for (let y = 0; y < s; y++)
        for (let x = 0; x < s; x++) {
          if (v[idx(x, y, top)] === 0) continue;
          set(x, y, top, rim(x, y) ? Slot.Metal : Slot.Hair);
        }
      break;
    case 5:
      for (let y = 0; y < s; y++) for (let x = 0; x < s; x++) if (v[idx(x, y, top)] !== 0) set(x, y, top, Slot.Hair);
      set(s >> 1, s >> 1, top, Slot.Glow);
      break;
  }

  // Outfit detail.
  const torsoH = headStart - legsH;
  switch (outfitStyle) {
    case 1:
      for (let x = 1; x < s - 1; x++) set(x, 0, legsH + (torsoH >> 1), Slot.Accent);
      break;
    case 2:
      for (let z = legsH; z < headStart; z++) set(s >> 1, 0, z, Slot.Accent);
      break;
    case 3:
      set(s >> 1, 0, headStart - 2 >= legsH ? headStart - 2 : legsH, Slot.Metal);
      break;
  }

  // Accessory.
  switch (accessory) {
    case 1:
      for (let z = legsH; z < headStart; z++) for (let x = 1; x < s - 1; x++) set(x, s - 1, z, Slot.Accent);
      break;
    case 2:
      for (let z = legsH; z < headStart; z++) for (let x = 0; x < s; x++) if (v[idx(x, s - 1, z)] !== 0) set(x, s - 1, z, Slot.Hat);
      break;
    case 3:
      for (let y = 0; y < s; y++)
        for (let x = 0; x < s; x++) if (v[idx(x, y, legsH)] !== 0 && rim(x, y)) set(x, y, legsH, Slot.Metal);
      break;
    case 4:
      for (let y = 0; y < s; y++)
        for (let x = 0; x < s; x++) if (v[idx(x, y, headStart - 1)] !== 0 && rim(x, y)) set(x, y, headStart - 1, Slot.Accent);
      break;
  }

  const points = HeadwearPoints[headwear] + EyePoints[eyes] + OutfitStylePoints[outfitStyle] + AccessoryPoints[accessory] + (glow ? 3 : 0);
  const traits: FigureTraits = { skin, outfit, accent, hair, pants, shoes, headwear, eyes, outfitStyle, accessory, glow };
  return new Figure(size, palette, v, name, rarityFromPoints(points), traits);
}
