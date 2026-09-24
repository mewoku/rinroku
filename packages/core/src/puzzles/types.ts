/** Mirrors Ronriku.Domain.Puzzles.PuzzleDifficulty (Easy = 1, Standard = 2, Hard = 3). */
export type PuzzleDifficulty = "Easy" | "Standard" | "Hard";
export type TrialKind = "Pattern" | "Spatial" | "Logic";

export const DIFFICULTY_VALUE: Record<PuzzleDifficulty, number> = { Easy: 1, Standard: 2, Hard: 3 };

export interface PuzzleMetadata {
  id: string;
  version: number;
  type: "spatial" | "pattern" | "logic";
  seed: bigint;
  variantSeed: bigint;
  difficulty: PuzzleDifficulty;
  rulesVersion: number;
  goal: string;
  timeLimitSeconds: number;
  skillDimensions: string[];
  contentHash: string;
}

/** C# `{seed:x16}` on a long: two's complement, lower-case hex, 16 digits. */
export function seedHex(seed: bigint): string {
  return BigInt.asUintN(64, seed).toString(16).padStart(16, "0");
}

export function timeLimitSeconds(type: string, difficulty: PuzzleDifficulty): number {
  if (type === "pattern") return difficulty === "Easy" ? 45 : difficulty === "Standard" ? 60 : 90;
  if (type === "logic") return difficulty === "Easy" ? 60 : difficulty === "Standard" ? 120 : 180;
  return 90;
}

export function targetMilliseconds(type: string, difficulty: PuzzleDifficulty): number {
  if (type === "pattern") return difficulty === "Easy" ? 20000 : difficulty === "Standard" ? 30000 : 45000;
  if (type === "logic") return difficulty === "Easy" ? 30000 : difficulty === "Standard" ? 60000 : 90000;
  return difficulty === "Easy" ? 30000 : difficulty === "Standard" ? 45000 : 60000;
}

/**
 * TrialScoring.Points: max(100, 700 + 300·difficulty + max(0, target − elapsed)/100
 *   − overParPenalty·max(0, moves − par) − 120·resets), or 0 when unsolved.
 */
export function trialPoints(difficulty: PuzzleDifficulty, solved: boolean, elapsedMs: number, targetMs: number,
  moves: number, par: number, resets: number, overParPenalty: number): number {
  if (!solved) return 0;
  const base = 700 + DIFFICULTY_VALUE[difficulty] * 300;
  const speed = Math.trunc(Math.max(0, targetMs - Math.max(0, elapsedMs)) / 100);
  const penalty = Math.max(0, moves - par) * overParPenalty + Math.max(0, resets) * 120;
  return Math.max(100, base + speed - penalty);
}
