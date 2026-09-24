/**
 * Port of Ronriku.Domain.Puzzles.SpatialPuzzle (Shadow Match). Z is up; the floor is the XY plane.
 * Rotate a small cube structure with discrete 90° moves until its top-down shadow equals the target.
 */
import { DeterministicRandom, Fnv64Hasher, u64 } from "../rng.js";
import { DIFFICULTY_VALUE, seedHex, type PuzzleDifficulty, type PuzzleMetadata } from "./types.js";

export type GridPoint = readonly [x: number, y: number, z: number];

/** TurnLeft = 0, TurnRight = 1, TipBack = 2, TipForward = 3. */
export const SpatialMove = { TurnLeft: 0, TurnRight: 1, TipBack: 2, TipForward: 3 } as const;
export type SpatialMove = (typeof SpatialMove)[keyof typeof SpatialMove];

export const ORIENTATION_COUNT = 24;
export const MOVE_COUNT = 4;
export const BOX_SIZE = 3;

type Matrix = number[];

const GENERATORS: Matrix[] = [
  [0, -1, 0, 1, 0, 0, 0, 0, 1], // TurnLeft: +90° about Z
  [0, 1, 0, -1, 0, 0, 0, 0, 1], // TurnRight: -90° about Z
  [0, 0, -1, 0, 1, 0, 1, 0, 0], // TipBack: top moves toward -X
  [0, 0, 1, 0, 1, 0, -1, 0, 0], // TipForward: top moves toward +X
];

function multiply(a: Matrix, b: Matrix): Matrix {
  const r = new Array<number>(9);
  for (let row = 0; row < 3; row++)
    for (let col = 0; col < 3; col++)
      r[row * 3 + col] = a[row * 3] * b[col] + a[row * 3 + 1] * b[3 + col] + a[row * 3 + 2] * b[6 + col];
  return r;
}

/** The 24 cube rotations indexed by breadth-first expansion from identity (same order as C#). */
const { MATRICES, TRANSITIONS } = (() => {
  const found: Matrix[] = [[1, 0, 0, 0, 1, 0, 0, 0, 1]];
  const queue = [0];
  const transitions: number[][] = [];
  while (queue.length > 0) {
    const current = queue.shift()!;
    transitions[current] = [];
    for (let move = 0; move < MOVE_COUNT; move++) {
      const next = multiply(GENERATORS[move], found[current]);
      let index = found.findIndex((m) => m.every((v, i) => v === next[i]));
      if (index < 0) {
        found.push(next);
        index = found.length - 1;
        queue.push(index);
      }
      transitions[current][move] = index;
    }
  }
  if (found.length !== ORIENTATION_COUNT) throw new Error(`Expected 24 orientations, found ${found.length}`);
  return { MATRICES: found, TRANSITIONS: transitions };
})();

export function applyMove(orientation: number, move: number): number {
  return TRANSITIONS[orientation][move];
}

export function orientationMatrix(orientation: number): readonly number[] {
  return MATRICES[orientation];
}

/** Rotation about the box centre (1,1,1). */
export function rotate(orientation: number, [px, py, pz]: GridPoint): GridPoint {
  const m = MATRICES[orientation];
  const x = px - 1, y = py - 1, z = pz - 1;
  return [m[0] * x + m[1] * y + m[2] * z + 1, m[3] * x + m[4] * y + m[5] * z + 1, m[6] * x + m[7] * y + m[8] * z + 1];
}

/** Top-down shadow as a 9-bit mask, bit index x + 3y. */
export function shadow(cubes: readonly GridPoint[], orientation: number): number {
  let mask = 0;
  for (const cube of cubes) {
    const [x, y] = rotate(orientation, cube);
    mask |= 1 << (x + BOX_SIZE * y);
  }
  return mask;
}

export function inverseMove(move: number): number {
  return move === 0 ? 1 : move === 1 ? 0 : move === 2 ? 3 : 2;
}

export interface SpatialPuzzleData {
  metadata: PuzzleMetadata;
  cubes: GridPoint[];
  startOrientation: number;
  targetShadow: number;
  par: number;
}

export const SPATIAL_RULES_VERSION = 2;
export const MAX_MATCHING_ORIENTATIONS = 4;
export const MIN_DISTINCT_SHADOWS = 6;
const MAX_ATTEMPTS = 512;

export function spatialLimits(difficulty: PuzzleDifficulty): { cubes: number; minPar: number; maxPar: number } {
  return difficulty === "Easy" ? { cubes: 4, minPar: 1, maxPar: 2 }
    : difficulty === "Standard" ? { cubes: 5, minPar: 2, maxPar: 3 }
    : { cubes: 6, minPar: 3, maxPar: 5 };
}

/** BFS distances over the 24-state orientation graph. */
export function orientationDistances(start: number): number[] {
  const distance = new Array<number>(ORIENTATION_COUNT).fill(-1);
  distance[start] = 0;
  const queue = [start];
  while (queue.length > 0) {
    const current = queue.shift()!;
    for (let move = 0; move < MOVE_COUNT; move++) {
      const next = applyMove(current, move);
      if (distance[next] >= 0) continue;
      distance[next] = distance[current] + 1;
      queue.push(next);
    }
  }
  return distance;
}

/** Shortest move sequence to the target shadow, or null if unreachable (same BFS order as C#). */
export function solveSpatial(cubes: readonly GridPoint[], start: number, targetShadow: number): number[] | null {
  const parent = new Array<number>(ORIENTATION_COUNT).fill(-2);
  const via = new Array<number>(ORIENTATION_COUNT).fill(0);
  parent[start] = -1;
  const queue = [start];
  while (queue.length > 0) {
    const current = queue.shift()!;
    if (shadow(cubes, current) === targetShadow) {
      const path: number[] = [];
      for (let o = current; parent[o] >= 0; o = parent[o]) path.push(via[o]);
      return path.reverse();
    }
    for (let move = 0; move < MOVE_COUNT; move++) {
      const next = applyMove(current, move);
      if (parent[next] !== -2) continue;
      parent[next] = current;
      via[next] = move;
      queue.push(next);
    }
  }
  return null;
}

export const SPATIAL_MAX_MOVES = 64;

/** Replays submitted moves from the issued start orientation. Clients submit actions, never a flag. */
export function validateSpatial(data: Pick<SpatialPuzzleData, "cubes" | "startOrientation" | "targetShadow">, moves: readonly number[]): boolean {
  if (!data || !Array.isArray(moves) || moves.length > SPATIAL_MAX_MOVES) return false;
  let orientation = data.startOrientation;
  for (const move of moves) {
    if (!Number.isInteger(move) || move < 0 || move >= MOVE_COUNT) return false;
    orientation = applyMove(orientation, move);
  }
  return shadow(data.cubes, orientation) === data.targetShadow;
}

const DIRECTIONS: GridPoint[] = [[1, 0, 0], [-1, 0, 0], [0, 1, 0], [0, -1, 0], [0, 0, 1], [0, 0, -1]];

function growPolycube(random: DeterministicRandom, count: number): GridPoint[] {
  const cubes: GridPoint[] = [[1, 1, 1]];
  let guard = 0;
  while (cubes.length < count && guard++ < 256) {
    const from = cubes[random.nextInt(cubes.length)];
    const d = DIRECTIONS[random.nextInt(DIRECTIONS.length)];
    const next: GridPoint = [from[0] + d[0], from[1] + d[1], from[2] + d[2]];
    if (next.some((c) => c < 0 || c >= BOX_SIZE) || cubes.some((c) => c[0] === next[0] && c[1] === next[1] && c[2] === next[2])) continue;
    cubes.push(next);
  }
  return cubes;
}

function turnOnlyShadows(cubes: readonly GridPoint[], start: number): Set<number> {
  const result = new Set<number>();
  let o = start;
  for (let i = 0; i < 4; i++) {
    result.add(shadow(cubes, o));
    o = applyMove(o, SpatialMove.TurnLeft);
  }
  return result;
}

export function generateSpatial(seed: bigint, difficulty: PuzzleDifficulty, variantSeed = 0n): SpatialPuzzleData {
  const dv = DIFFICULTY_VALUE[difficulty];
  const random = new DeterministicRandom(u64(seed) ^ u64(BigInt(dv) * 0x9e3779b97f4a7c15n));
  const { cubes: cubeCount, minPar, maxPar } = spatialLimits(difficulty);
  const requireTip = difficulty !== "Easy";

  for (let attempt = 0; attempt < MAX_ATTEMPTS; attempt++) {
    const cubes = growPolycube(random, cubeCount);
    const shadows: number[] = [];
    const shadowCounts = new Map<number, number>();
    for (let o = 0; o < ORIENTATION_COUNT; o++) {
      shadows[o] = shadow(cubes, o);
      shadowCounts.set(shadows[o], (shadowCounts.get(shadows[o]) ?? 0) + 1);
    }
    if (shadowCounts.size < MIN_DISTINCT_SHADOWS) continue;

    const start = random.nextInt(ORIENTATION_COUNT);
    const distance = orientationDistances(start);
    const turnOnly = turnOnlyShadows(cubes, start);

    const candidates: number[] = [];
    for (const [mask, count] of shadowCounts) {
      if (mask === shadows[start] || count > MAX_MATCHING_ORIENTATIONS) continue;
      if (requireTip && turnOnly.has(mask)) continue;
      let par = Number.MAX_SAFE_INTEGER;
      for (let o = 0; o < ORIENTATION_COUNT; o++) if (shadows[o] === mask) par = Math.min(par, distance[o]);
      if (par >= minPar && par <= maxPar) candidates.push(mask);
    }
    if (candidates.length === 0) continue;

    candidates.sort((a, b) => a - b);
    const target = candidates[random.nextInt(candidates.length)];
    const finalPar = solveSpatial(cubes, start, target)!.length;
    const h = new Fnv64Hasher().add(SPATIAL_RULES_VERSION).add(seed).add(variantSeed).add(dv).add(start).add(target);
    for (const [x, y, z] of cubes) h.add(x).add(y).add(z);
    const metadata: PuzzleMetadata = {
      id: `spatial-${seedHex(seed)}-${dv}`, version: 1, type: "spatial", seed, variantSeed, difficulty,
      rulesVersion: SPATIAL_RULES_VERSION, goal: "MAKE THE SHADOW MATCH THE TARGET", timeLimitSeconds: 90,
      skillDimensions: ["Spatial", "Planning", "Speed"], contentHash: h.hex(),
    };
    return { metadata, cubes, startOrientation: start, targetShadow: target, par: finalPar };
  }
  throw new Error(`No valid Spatial puzzle for seed ${seed} at ${difficulty}.`);
}
