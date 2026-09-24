import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { Figure } from "@ronriku/core";
import { decodeRf1, filledCount, Rf1FormatError } from "./rf1";
import { buildVoxelMesh, countVisibleFaces } from "./voxelMesh";

const fixtures = JSON.parse(readFileSync(new URL("../../../packages/core/fixtures/figures.json", import.meta.url), "utf8")) as {
  figures: { seed: string; size: number; filled: number; encoding: string }[];
};

/** Reference face count: brute force over every voxel's 6 neighbours. */
function bruteForceFaces(size: number, voxels: Uint8Array): number {
  const h = size * 2;
  const at = (x: number, y: number, z: number) => (x < 0 || y < 0 || z < 0 || x >= size || y >= size || z >= h ? 0 : voxels[x + size * (y + size * z)]!);
  let n = 0;
  for (let z = 0; z < h; z++)
    for (let y = 0; y < size; y++)
      for (let x = 0; x < size; x++) {
        if (!at(x, y, z)) continue;
        for (const [dx, dy, dz] of [[1, 0, 0], [-1, 0, 0], [0, 1, 0], [0, -1, 0], [0, 0, 1], [0, 0, -1]] as const) if (!at(x + dx, y + dy, z + dz)) n++;
      }
  return n;
}

describe("RF1 decode", () => {
  it("decodes every C# fixture with the right size and filled count", () => {
    expect(fixtures.figures.length).toBeGreaterThan(100);
    for (const f of fixtures.figures) {
      const d = decodeRf1(f.encoding);
      expect(d.size).toBe(f.size);
      expect(d.height).toBe(f.size * 2);
      expect(d.voxels.length).toBe(f.size * f.size * f.size * 2);
      expect(filledCount(d)).toBe(f.filled);
    }
  });

  it("agrees voxel-for-voxel with @ronriku/core", () => {
    for (const f of fixtures.figures.slice(0, 40)) {
      const ours = decodeRf1(f.encoding);
      const core = Figure.decode(f.encoding);
      expect(Array.from(ours.voxels)).toEqual(Array.from(core.voxels));
      expect(ours.palette).toEqual(core.palette);
    }
  });

  it("rejects malformed encodings", () => {
    const good = fixtures.figures[0]!.encoding;
    expect(() => decodeRf1("")).toThrow(Rf1FormatError);
    expect(() => decodeRf1(good.replace("RF1.", "RF2."))).toThrow(Rf1FormatError);
    expect(() => decodeRf1(good.replace("RF1.3.", "RF1.9."))).toThrow(Rf1FormatError);
    expect(() => decodeRf1(`${good}AA`)).toThrow(Rf1FormatError);
    expect(() => decodeRf1(good.slice(0, -1) + "!")).toThrow(Rf1FormatError);
  });
});

describe("voxel mesh", () => {
  it("single voxel → 6 faces, 24 vertices, 36 indices", () => {
    const voxels = new Uint8Array(3 * 3 * 6);
    voxels[0] = 1;
    const mesh = buildVoxelMesh({ size: 3, height: 6, palette: new Array(15).fill(0xff0000), voxels });
    expect(mesh.faceCount).toBe(6);
    expect(mesh.positions.length).toBe(6 * 4 * 3);
    expect(mesh.indices.length).toBe(36);
    expect(Array.from(mesh.colors.slice(0, 3))).toEqual([1, 0, 0]);
  });

  it("two adjacent voxels share a hidden face pair → 10 faces", () => {
    const voxels = new Uint8Array(3 * 3 * 6);
    voxels[0] = 1;
    voxels[1] = 2;
    expect(countVisibleFaces({ size: 3, height: 6, palette: new Array(15).fill(0), voxels })).toBe(10);
  });

  it("full solid 3×3×6 block exposes only its surface", () => {
    const voxels = new Uint8Array(3 * 3 * 6).fill(1);
    // surface = 2·(3·3) + 4·(3·6) = 18 + 72
    expect(countVisibleFaces({ size: 3, height: 6, palette: new Array(15).fill(0), voxels })).toBe(90);
  });

  it("face count matches brute force for every fixture", () => {
    for (const f of fixtures.figures) {
      const d = decodeRf1(f.encoding);
      const mesh = buildVoxelMesh(d);
      expect(mesh.faceCount).toBe(bruteForceFaces(d.size, d.voxels));
      expect(mesh.indices.length).toBe(mesh.faceCount * 6);
    }
  });

  it("normals point away from the voxel centre (outward winding sanity)", () => {
    const voxels = new Uint8Array(3 * 3 * 6);
    voxels[0] = 1;
    const m = buildVoxelMesh({ size: 3, height: 6, palette: new Array(15).fill(0), voxels });
    // voxel centre in three space: x = 0.5 - 1.5, y = 0.5, z = 1.5 - 0.5
    const c = [-1, 0.5, 1];
    for (let f = 0; f < m.faceCount; f++) {
      const i = f * 12;
      const mid = [0, 1, 2].map((k) => (m.positions[i + k]! + m.positions[i + 6 + k]!) / 2);
      const n = [m.normals[i]!, m.normals[i + 1]!, m.normals[i + 2]!];
      const dot = n.reduce((s, v, k) => s + v * (mid[k]! - c[k]!), 0);
      expect(dot).toBeGreaterThan(0);
      // CCW: (b - a) × (c - a) should align with the normal
      const a = [0, 1, 2].map((k) => m.positions[i + k]!);
      const b = [0, 1, 2].map((k) => m.positions[i + 3 + k]!);
      const cc = [0, 1, 2].map((k) => m.positions[i + 6 + k]!);
      const u = b.map((v, k) => v - a[k]!);
      const w = cc.map((v, k) => v - a[k]!);
      const cross = [u[1]! * w[2]! - u[2]! * w[1]!, u[2]! * w[0]! - u[0]! * w[2]!, u[0]! * w[1]! - u[1]! * w[0]!];
      expect(cross.reduce((s, v, k) => s + v * n[k]!, 0)).toBeGreaterThan(0);
    }
  });
});
