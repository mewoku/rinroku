import { type DecodedFigure, voxelAt } from "./rf1";

/**
 * Pure (three.js-free) voxel → quad mesh builder with hidden-face culling.
 * Output is in three.js space: X = figure x, Y = figure z (up), Z = figure front (y = 0 faces +Z).
 * The model is centred on X/Z with its feet at Y = 0.
 */
export interface VoxelMeshData {
  positions: Float32Array; // 3 per vertex, 4 vertices per face
  normals: Float32Array;
  colors: Float32Array; // 0..1 sRGB, flat per face
  indices: Uint32Array; // 6 per face
  faceCount: number;
}

type V3 = [number, number, number];
interface Dir {
  d: V3;
  corners: [V3, V3, V3, V3];
}

// Figure-space directions (x, y, z-up) with counter-clockwise quad corners on the unit cube.
const DIRS: Dir[] = [
  { d: [1, 0, 0], corners: [[1, 0, 0], [1, 1, 0], [1, 1, 1], [1, 0, 1]] },
  { d: [-1, 0, 0], corners: [[0, 1, 0], [0, 0, 0], [0, 0, 1], [0, 1, 1]] },
  { d: [0, 1, 0], corners: [[1, 1, 0], [0, 1, 0], [0, 1, 1], [1, 1, 1]] },
  { d: [0, -1, 0], corners: [[0, 0, 0], [1, 0, 0], [1, 0, 1], [0, 0, 1]] },
  { d: [0, 0, 1], corners: [[0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]] },
  { d: [0, 0, -1], corners: [[0, 1, 0], [1, 1, 0], [1, 0, 0], [0, 0, 0]] },
];

function exposed(f: DecodedFigure, x: number, y: number, z: number, d: V3): boolean {
  return voxelAt(f, x + d[0], y + d[1], z + d[2]) === 0;
}

/** Number of exposed faces (neighbour empty or outside the volume). */
export function countVisibleFaces(f: DecodedFigure): number {
  let n = 0;
  for (let z = 0; z < f.height; z++)
    for (let y = 0; y < f.size; y++)
      for (let x = 0; x < f.size; x++) {
        if (voxelAt(f, x, y, z) === 0) continue;
        for (const dir of DIRS) if (exposed(f, x, y, z, dir.d)) n++;
      }
  return n;
}

export function buildVoxelMesh(f: DecodedFigure): VoxelMeshData {
  const faceCount = countVisibleFaces(f);
  const positions = new Float32Array(faceCount * 12);
  const normals = new Float32Array(faceCount * 12);
  const colors = new Float32Array(faceCount * 12);
  const indices = new Uint32Array(faceCount * 6);
  const half = f.size / 2;
  let face = 0;
  for (let z = 0; z < f.height; z++)
    for (let y = 0; y < f.size; y++)
      for (let x = 0; x < f.size; x++) {
        const v = voxelAt(f, x, y, z);
        if (v === 0) continue;
        const rgb = f.palette[v - 1] ?? 0xff00ff;
        const col: V3 = [((rgb >> 16) & 255) / 255, ((rgb >> 8) & 255) / 255, (rgb & 255) / 255];
        for (const dir of DIRS) {
          if (!exposed(f, x, y, z, dir.d)) continue;
          const n: V3 = [dir.d[0], dir.d[2], -dir.d[1]];
          for (let c = 0; c < 4; c++) {
            const k = dir.corners[c]!;
            const o = face * 12 + c * 3;
            positions.set([x + k[0] - half, z + k[2], half - (y + k[1])], o);
            normals.set(n, o);
            colors.set(col, o);
          }
          const b = face * 4;
          indices.set([b, b + 1, b + 2, b, b + 2, b + 3], face * 6);
          face++;
        }
      }
  return { positions, normals, colors, indices, faceCount };
}
