/**
 * Kind-dispatching helpers so a server can regenerate any trial from (kind, seed, difficulty) and
 * replay a submitted answer. Answers: Pattern = option index, Spatial = move list (0..3),
 * Logic = cell path (x + size·y).
 */
import { generateLogic, validateLogic, type LogicPuzzleData } from "./logic.js";
import { generatePattern, validatePattern, type PatternPuzzleData } from "./pattern.js";
import { generateSpatial, validateSpatial, type SpatialPuzzleData } from "./spatial.js";
import type { PuzzleDifficulty, TrialKind } from "./types.js";

export type TrialData =
  | { kind: "Pattern"; data: PatternPuzzleData }
  | { kind: "Spatial"; data: SpatialPuzzleData }
  | { kind: "Logic"; data: LogicPuzzleData };

export type TrialAnswer = number | readonly number[];

export function generateTrial(kind: TrialKind, seed: bigint, difficulty: PuzzleDifficulty, variantSeed = 0n): TrialData {
  switch (kind) {
    case "Pattern": return { kind, data: generatePattern(seed, difficulty, variantSeed) };
    case "Spatial": return { kind, data: generateSpatial(seed, difficulty, variantSeed) };
    case "Logic": {
      const { solution: _solution, ...data } = generateLogic(seed, difficulty, variantSeed);
      return { kind, data };
    }
  }
}

export function validateTrial(trial: TrialData, answer: unknown): boolean {
  switch (trial.kind) {
    case "Pattern": return typeof answer === "number" && validatePattern(trial.data, answer);
    case "Spatial": return Array.isArray(answer) && validateSpatial(trial.data, answer);
    case "Logic": return Array.isArray(answer) && validateLogic(trial.data, answer);
  }
}

/** JSON.stringify replacer that writes bigints as decimal strings (seeds are 64-bit). */
export const bigintReplacer = (_key: string, value: unknown) => (typeof value === "bigint" ? value.toString() : value);
