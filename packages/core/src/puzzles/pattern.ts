/**
 * Port of Ronriku.Domain.Puzzles.PatternPuzzle. Grids are 16-bit masks on a 4×4 board, bit index
 * x + 4y, y down. Infer the rule from example pairs and pick its output for the test grid.
 */
import { DeterministicRandom, Fnv64Hasher, u64 } from "../rng.js";
import { DIFFICULTY_VALUE, seedHex, timeLimitSeconds, type PuzzleDifficulty, type PuzzleMetadata } from "./types.js";

export const PatternRule = {
  Rotate90: 0, Rotate180: 1, Rotate270: 2, MirrorX: 3, MirrorY: 4, Transpose: 5, Invert: 6, ShiftRight: 7, ShiftDown: 8,
} as const;
export type PatternRule = (typeof PatternRule)[keyof typeof PatternRule];

const SIZE = 4;
const CELLS = 16;
const FULL = 0xffff;

export function gridGet(grid: number, x: number, y: number): boolean {
  return (grid & (1 << (x + SIZE * y))) !== 0;
}

export function gridCount(grid: number): number {
  let count = 0;
  for (let i = 0; i < CELLS; i++) if (grid & (1 << i)) count++;
  return count;
}

export function applyRule(rule: PatternRule, grid: number): number {
  if (rule === PatternRule.Invert) return ~grid & FULL;
  let result = 0;
  for (let y = 0; y < SIZE; y++)
    for (let x = 0; x < SIZE; x++) {
      if (!gridGet(grid, x, y)) continue;
      let nx: number, ny: number;
      switch (rule) {
        case PatternRule.Rotate90: nx = SIZE - 1 - y; ny = x; break;
        case PatternRule.Rotate180: nx = SIZE - 1 - x; ny = SIZE - 1 - y; break;
        case PatternRule.Rotate270: nx = y; ny = SIZE - 1 - x; break;
        case PatternRule.MirrorX: nx = SIZE - 1 - x; ny = y; break;
        case PatternRule.MirrorY: nx = x; ny = SIZE - 1 - y; break;
        case PatternRule.Transpose: nx = y; ny = x; break;
        case PatternRule.ShiftRight: nx = (x + 1) % SIZE; ny = y; break;
        case PatternRule.ShiftDown: nx = x; ny = (y + 1) % SIZE; break;
        default: throw new RangeError(`rule ${rule}`);
      }
      result |= 1 << (nx + SIZE * ny);
    }
  return result;
}

export function applyRules(rules: readonly PatternRule[], grid: number): number {
  for (const rule of rules) grid = applyRule(rule, grid);
  return grid;
}

export interface PatternPuzzleData {
  metadata: PuzzleMetadata;
  examples: Array<[input: number, output: number]>;
  testInput: number;
  options: number[];
  correctOption: number;
}

export const PATTERN_RULES_VERSION = 1;
export const OPTION_COUNT = 4;
const MAX_ATTEMPTS = 2048;

const EASY_RULES: PatternRule[] = [0, 1, 2, 3, 4];
const STANDARD_RULES: PatternRule[] = [0, 1, 2, 3, 4, 5, 6, 7, 8];

/** All singles then all ordered pairs of the standard vocabulary (C# order). */
export const HYPOTHESES: readonly PatternRule[][] = [
  ...STANDARD_RULES.map((a) => [a]),
  ...STANDARD_RULES.flatMap((a) => STANDARD_RULES.map((b) => [a, b])),
];

export const exampleCount = (difficulty: PuzzleDifficulty) => (difficulty === "Hard" ? 3 : 2);

/** True when every hypothesis consistent with the examples maps the test input to `answer`. */
export function answerIsForced(examples: ReadonlyArray<readonly [number, number]>, test: number, answer: number): boolean {
  for (const h of HYPOTHESES) {
    if (examples.every(([i, o]) => applyRules(h, i) === o) && applyRules(h, test) !== answer) return false;
  }
  return true;
}

function pickRule(random: DeterministicRandom, difficulty: PuzzleDifficulty): PatternRule[] {
  if (difficulty === "Easy") return [EASY_RULES[random.nextInt(EASY_RULES.length)]];
  if (difficulty === "Standard") return [STANDARD_RULES[random.nextInt(STANDARD_RULES.length)]];
  for (;;) {
    const a = STANDARD_RULES[random.nextInt(STANDARD_RULES.length)];
    const b = STANDARD_RULES[random.nextInt(STANDARD_RULES.length)];
    const pair = [a, b];
    if (a !== b && !equivalentToSingle(pair)) return pair;
  }
}

function equivalentToSingle(pair: PatternRule[]): boolean {
  const probeA = 0x1237, probeB = 0x8c41;
  for (const single of STANDARD_RULES)
    if (applyRule(single, probeA) === applyRules(pair, probeA) && applyRule(single, probeB) === applyRules(pair, probeB)) return true;
  return applyRules(pair, probeA) === probeA && applyRules(pair, probeB) === probeB;
}

function randomGrid(random: DeterministicRandom): number {
  const target = 5 + random.nextInt(4);
  let grid = 0;
  while (gridCount(grid) < target) grid |= 1 << random.nextInt(CELLS);
  return grid;
}

/** A grid is informative when no primitive rule leaves it unchanged. */
function isInformative(grid: number): boolean {
  return STANDARD_RULES.every((rule) => applyRule(rule, grid) !== grid);
}

function take(random: DeterministicRandom, pool: number[], into: number[]): void {
  while (into.length < OPTION_COUNT - 1 && pool.length > 0) {
    const index = random.nextInt(pool.length);
    into.push(pool[index]);
    pool.splice(index, 1);
  }
}

function pickDecoys(random: DeterministicRandom, rule: PatternRule[], test: number, answer: number): number[] | null {
  const near: number[] = [], far: number[] = [];
  for (const h of HYPOTHESES) {
    const output = applyRules(h, test);
    if (output === answer || output === test || near.includes(output) || far.includes(output)) continue;
    // C#: (h.Length == rule.Length && !rule.Contains(h[0])) || (pair && pair && h reversed == rule)
    const related = (h.length === rule.length && !rule.includes(h[0])) ||
      (h.length === 2 && rule.length === 2 && h[0] === rule[1] && h[1] === rule[0]);
    (related ? near : far).push(output);
  }
  const decoys: number[] = [];
  take(random, near, decoys);
  take(random, far, decoys);
  return decoys.length === OPTION_COUNT - 1 ? decoys : null;
}

export function generatePattern(seed: bigint, difficulty: PuzzleDifficulty, variantSeed = 0n): PatternPuzzleData {
  const dv = DIFFICULTY_VALUE[difficulty];
  const random = new DeterministicRandom(u64(seed) ^ 0x5041545445524en ^ BigInt(dv));
  const count = exampleCount(difficulty);

  for (let attempt = 0; attempt < MAX_ATTEMPTS; attempt++) {
    const rule = pickRule(random, difficulty);
    const inputs: number[] = [];
    let ok = true;
    for (let i = 0; i < count + 1 && ok; i++) {
      inputs[i] = randomGrid(random);
      ok = isInformative(inputs[i]) && inputs.indexOf(inputs[i]) === i;
    }
    if (!ok) continue;

    const examples: Array<[number, number]> = [];
    for (let i = 0; i < count; i++) examples.push([inputs[i], applyRules(rule, inputs[i])]);
    const test = inputs[count];
    const answer = applyRules(rule, test);
    if (answer === test || !answerIsForced(examples, test, answer)) continue;

    const decoys = pickDecoys(random, rule, test, answer);
    if (decoys === null) continue;

    const correct = random.nextInt(OPTION_COUNT);
    const options: number[] = [];
    for (let i = 0, d = 0; i < OPTION_COUNT; i++) options.push(i === correct ? answer : decoys[d++]);

    const h = new Fnv64Hasher().add(PATTERN_RULES_VERSION).add(seed).add(variantSeed).add(dv).add(test).add(correct);
    for (const [i, o] of examples) h.add(i).add(o);
    for (const option of options) h.add(option);
    const metadata: PuzzleMetadata = {
      id: `pattern-${seedHex(seed)}-${dv}`, version: 1, type: "pattern", seed, variantSeed, difficulty,
      rulesVersion: PATTERN_RULES_VERSION, goal: "FIND THE RULE. PICK THE MISSING OUTPUT",
      timeLimitSeconds: timeLimitSeconds("pattern", difficulty), skillDimensions: ["Pattern", "Speed"], contentHash: h.hex(),
    };
    return { metadata, examples, testInput: test, options, correctOption: correct };
  }
  throw new Error(`No valid Pattern puzzle for seed ${seed} at ${difficulty}.`);
}

export function validatePattern(data: Pick<PatternPuzzleData, "options" | "correctOption">, option: number): boolean {
  return !!data && Number.isInteger(option) && option >= 0 && option < data.options.length && option === data.correctOption;
}
