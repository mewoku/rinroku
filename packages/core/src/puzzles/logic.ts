/**
 * Port of Ronriku.Domain.Puzzles.LogicPuzzle ("Link"): draw one path through every cell of an N×N
 * grid, starting at 1, passing the numbered cells in order and ending on the highest number.
 * Cells are indexed x + size·y. Walls are edge keys min(a,b)·cells + max(a,b).
 */
import { DeterministicRandom, Fnv64Hasher, u64 } from "../rng.js";
import { DIFFICULTY_VALUE, seedHex, timeLimitSeconds, type PuzzleDifficulty, type PuzzleMetadata } from "./types.js";

export interface LogicPuzzleData {
  metadata: PuzzleMetadata;
  size: number;
  /** Checkpoint number (1-based) per cell, or 0 when plain. */
  checkpoints: number[];
  checkpointCount: number;
  walls: number[];
}

export const LOGIC_RULES_VERSION = 1;
export const NODE_BUDGET = 4_000_000;

export const edgeKey = (a: number, b: number, cellCount: number) => Math.min(a, b) * cellCount + Math.max(a, b);

export const logicGridSize = (d: PuzzleDifficulty) => (d === "Easy" ? 4 : d === "Standard" ? 5 : 6);
const extraSpacing = (d: PuzzleDifficulty) => (d === "Easy" ? 4 : d === "Standard" ? 7 : 0);
/** 0 = numbers only, 1 = alternate walls and numbers, 2 = walls first. */
const wallPreference = (d: PuzzleDifficulty) => (d === "Easy" ? 0 : d === "Standard" ? 1 : 2);

/** Minimal sorted-set of ints (C# SortedSet semantics used by the generator). */
class SortedIntSet {
  readonly items: number[] = [];
  constructor(values: number[] = []) {
    for (const v of values) this.add(v);
  }
  add(v: number): boolean {
    let lo = 0, hi = this.items.length;
    while (lo < hi) {
      const mid = (lo + hi) >> 1;
      if (this.items[mid] < v) lo = mid + 1;
      else hi = mid;
    }
    if (this.items[lo] === v) return false;
    this.items.splice(lo, 0, v);
    return true;
  }
  has(v: number): boolean {
    return this.items.includes(v);
  }
  get size(): number {
    return this.items.length;
  }
}

class Search {
  private readonly cells: number;
  private readonly visited: Uint8Array;
  private readonly seen: Uint8Array;
  private readonly stack: Int32Array;
  private readonly path: number[] = [];
  private nodes = 0;
  result: number[] | null = null;
  exhausted = false;

  constructor(
    private readonly size: number,
    private readonly checkpoints: readonly number[],
    private readonly last: number,
    private readonly walls: ReadonlySet<number>,
    private readonly exclude: readonly number[] | null,
  ) {
    this.cells = size * size;
    this.visited = new Uint8Array(this.cells);
    this.seen = new Uint8Array(this.cells);
    this.stack = new Int32Array(this.cells);
  }

  run(): boolean {
    const start = this.checkpoints.indexOf(1);
    if (start < 0) return false;
    this.visited[start] = 1;
    this.path.push(start);
    return this.step(start, 2);
  }

  private blocked(a: number, b: number): boolean {
    return this.walls.size > 0 && this.walls.has(edgeKey(a, b, this.cells));
  }

  private step(cell: number, next: number): boolean {
    if (++this.nodes > NODE_BUDGET) {
      this.exhausted = true;
      return false;
    }
    if (this.path.length === this.cells) {
      if (next !== this.last + 1) return false;
      if (this.exclude && this.path.every((c, i) => c === this.exclude![i])) return false;
      this.result = [...this.path];
      return true;
    }
    if (!this.remainderConnected(cell)) return false;

    const x = cell % this.size, y = Math.floor(cell / this.size);
    for (let d = 0; d < 4; d++) {
      const nx = x + (d === 0 ? 1 : d === 1 ? -1 : 0), ny = y + (d === 2 ? 1 : d === 3 ? -1 : 0);
      if (nx < 0 || ny < 0 || nx >= this.size || ny >= this.size) continue;
      const n = nx + this.size * ny;
      if (this.visited[n] || this.blocked(cell, n)) continue;
      const number = this.checkpoints[n];
      if (number !== 0 && number !== next) continue;
      if (number === this.last && this.path.length + 1 !== this.cells) continue;
      this.visited[n] = 1;
      this.path.push(n);
      if (this.step(n, number !== 0 ? next + 1 : next)) return true;
      this.path.pop();
      this.visited[n] = 0;
      if (this.exhausted) return false;
    }
    return false;
  }

  /** All unvisited cells must be reachable from the current cell, with at most one dead end. */
  private remainderConnected(from: number): boolean {
    this.seen.fill(0);
    let top = 0, reached = 0, deadEnds = 0;
    const remaining = this.cells - this.path.length;
    this.stack[top++] = from;
    this.seen[from] = 1;
    while (top > 0) {
      const c = this.stack[--top];
      const x = c % this.size, y = Math.floor(c / this.size);
      let free = 0;
      for (let d = 0; d < 4; d++) {
        const nx = x + (d === 0 ? 1 : d === 1 ? -1 : 0), ny = y + (d === 2 ? 1 : d === 3 ? -1 : 0);
        if (nx < 0 || ny < 0 || nx >= this.size || ny >= this.size) continue;
        const n = nx + this.size * ny;
        if ((this.visited[n] && n !== from) || this.blocked(c, n)) continue;
        free++;
        if (this.seen[n] || this.visited[n]) continue;
        this.seen[n] = 1;
        reached++;
        this.stack[top++] = n;
      }
      if (c !== from && free <= 1 && ++deadEnds > 1) return false;
    }
    return reached === remaining;
  }
}

/**
 * A valid solution different from `known`, or null if none exists. When the node budget runs out,
 * returns `known` with its last cell set to -1 (C# "not proven unique" signal).
 */
export function findOtherLogicSolution(size: number, checkpoints: readonly number[], checkpointCount: number,
  walls: ReadonlySet<number>, known: readonly number[]): number[] | null {
  const search = new Search(size, checkpoints, checkpointCount, walls, known);
  if (search.run()) return search.result;
  if (search.exhausted) {
    const fallback = [...known];
    fallback[fallback.length - 1] = -1;
    return fallback;
  }
  return null;
}

/** Any one valid solution, or null. */
export function solveLogic(data: Pick<LogicPuzzleData, "size" | "checkpoints" | "checkpointCount" | "walls">): number[] | null {
  const search = new Search(data.size, data.checkpoints, data.checkpointCount, new Set(data.walls), null);
  return search.run() ? search.result : null;
}

/** Validates a submitted cell path (clients submit the path, never a completed flag). */
export function validateLogic(data: Pick<LogicPuzzleData, "size" | "checkpoints" | "checkpointCount" | "walls">, path: readonly number[]): boolean {
  const cells = data?.size * data?.size;
  if (!data || !Array.isArray(path) || path.length !== cells) return false;
  const walls = new Set(data.walls);
  const seen = new Uint8Array(cells);
  let next = 1;
  for (let i = 0; i < path.length; i++) {
    const cell = path[i];
    if (!Number.isInteger(cell) || cell < 0 || cell >= cells || seen[cell]) return false;
    if (i > 0) {
      const prev = path[i - 1];
      const adjacent = Math.abs((prev % data.size) - (cell % data.size)) + Math.abs(Math.floor(prev / data.size) - Math.floor(cell / data.size)) === 1;
      if (!adjacent || walls.has(edgeKey(prev, cell, cells))) return false;
    }
    seen[cell] = 1;
    const number = data.checkpoints[cell];
    if (number === 0) continue;
    if (number !== next) return false;
    next++;
  }
  return next === data.checkpointCount + 1 && data.checkpoints[path[path.length - 1]] === data.checkpointCount;
}

/** Serpentine start, then random "backbite" moves, which preserve the Hamiltonian property. */
function randomHamiltonianPath(random: DeterministicRandom, size: number): number[] {
  let path: number[] = [];
  for (let y = 0; y < size; y++) for (let i = 0; i < size; i++) path.push((y % 2 === 0 ? i : size - 1 - i) + size * y);
  const dx = [1, -1, 0, 0], dy = [0, 0, 1, -1];
  const position = new Int32Array(size * size);
  const moves = size * size * 30;
  for (let m = 0; m < moves; m++) {
    const fromTail = random.nextInt(2) === 0;
    if (fromTail) path.reverse();
    const head = path[0];
    const d = random.nextInt(4);
    const nx = (head % size) + dx[d], ny = Math.floor(head / size) + dy[d];
    if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
    const neighbour = nx + size * ny;
    for (let i = 0; i < path.length; i++) position[path[i]] = i;
    const k = position[neighbour];
    if (k <= 1) continue;
    // Adding edge head–path[k] and removing path[k-1]–path[k] reverses path[0..k-1].
    path = [...path.slice(0, k).reverse(), ...path.slice(k)];
  }
  return path;
}

function largestGapMidpoint(numbered: SortedIntSet): number {
  let best = -1, bestGap = 0, previous = -1;
  for (const index of numbered.items) {
    if (previous >= 0 && index - previous > bestGap) {
      bestGap = index - previous;
      best = previous + Math.floor(bestGap / 2);
    }
    previous = index;
  }
  return best;
}

/** Blocks the first edge the alternative uses that the intended path does not. */
function tryAddWall(alternative: number[], pathEdges: Set<number>, walls: SortedIntSet, cells: number): boolean {
  for (let i = 1; i < alternative.length; i++) {
    if (alternative[i] < 0 || alternative[i - 1] < 0) return false;
    const key = edgeKey(alternative[i - 1], alternative[i], cells);
    if (!pathEdges.has(key) && walls.add(key)) return true;
  }
  return false;
}

export function generateLogic(seed: bigint, difficulty: PuzzleDifficulty, variantSeed = 0n): LogicPuzzleData & { solution: number[] } {
  const dv = DIFFICULTY_VALUE[difficulty];
  const random = new DeterministicRandom(u64(seed) ^ 0x4c4f474943n ^ (BigInt(dv) << 40n));
  const size = logicGridSize(difficulty);
  const cells = size * size;
  const path = randomHamiltonianPath(random, size);

  const numbered = new SortedIntSet([0, cells - 1]);
  const spacing = extraSpacing(difficulty);
  if (spacing > 0) for (let i = spacing; i < cells - 2; i += spacing) numbered.add(i + random.nextInt(2));

  const pathEdges = new Set<number>();
  for (let i = 1; i < path.length; i++) pathEdges.add(edgeKey(path[i - 1], path[i], cells));
  const walls = new SortedIntSet();
  const preference = wallPreference(difficulty);

  for (let guard = 0; guard < cells * 4; guard++) {
    const checkpoints = new Array<number>(cells).fill(0);
    numbered.items.forEach((index, n) => (checkpoints[path[index]] = n + 1));
    const alternative = findOtherLogicSolution(size, checkpoints, numbered.size, new Set(walls.items), path);
    if (alternative === null) {
      const wallList = [...walls.items];
      const h = new Fnv64Hasher().add(LOGIC_RULES_VERSION).add(seed).add(variantSeed).add(dv).add(size);
      for (const c of checkpoints) h.add(c);
      h.add(-1);
      for (const w of wallList) h.add(w);
      const metadata: PuzzleMetadata = {
        id: `logic-${seedHex(seed)}-${dv}`, version: 1, type: "logic", seed, variantSeed, difficulty,
        rulesVersion: LOGIC_RULES_VERSION, goal: "CONNECT THE NUMBERS. FILL EVERY CELL",
        timeLimitSeconds: timeLimitSeconds("logic", difficulty), skillDimensions: ["Logic", "Planning", "Speed"], contentHash: h.hex(),
      };
      return { metadata, size, checkpoints, checkpointCount: numbered.size, walls: wallList, solution: path };
    }

    const useWall = preference === 2 || (preference === 1 && guard % 2 === 0);
    if (useWall && tryAddWall(alternative, pathEdges, walls, cells)) continue;

    let diverge = 0;
    while (diverge < path.length && path[diverge] === alternative[diverge]) diverge++;
    if (diverge >= cells - 1 || !numbered.add(diverge)) numbered.add(largestGapMidpoint(numbered));
  }
  throw new Error(`No unique Logic puzzle for seed ${seed} at ${difficulty}.`);
}
