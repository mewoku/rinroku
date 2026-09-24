import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import {
  DeterministicRandom, Figure, challengeId, dailyPlan, dailySeed, dayNumber, generateFigure, generateLogic,
  generatePattern, generateSpatial, solveLogic, solveSpatial, validateLogic, validatePattern, validateSpatial,
  type PuzzleDifficulty,
} from "../src/index.js";

const dir = join(dirname(fileURLToPath(import.meta.url)), "..", "fixtures");
const load = (name: string) => JSON.parse(readFileSync(join(dir, name), "utf8"));

describe("rng.json", () => {
  const { cases } = load("rng.json") as { cases: { seed: string; maxes: number[]; values: number[] }[] };
  it.each(cases)("seed $seed", ({ seed, maxes, values }) => {
    const r = new DeterministicRandom(BigInt(seed));
    const got = values.map((_, i) => r.nextInt(maxes[i % maxes.length]));
    expect(got).toEqual(values);
  });
});

describe("figures.json", () => {
  const { figures } = load("figures.json") as {
    figures: { seed: string; size: number; monster: boolean; name: string; rarity: string; filled: number; encoding: string }[];
  };
  it("has rows", () => expect(figures.length).toBeGreaterThan(100));
  it.each(figures)("seed $seed size $size monster $monster", (row) => {
    const f = generateFigure(BigInt(row.seed), row.size, row.monster);
    expect(f.encode()).toBe(row.encoding);
    expect(f.name).toBe(row.name);
    expect(f.rarity).toBe(row.rarity);
    expect(f.filledCount).toBe(row.filled);
    const back = Figure.decode(row.encoding);
    expect(back.encode()).toBe(row.encoding);
    expect(back.filledCount).toBe(row.filled);
  });
});

describe("vox fixtures", () => {
  for (const seed of [1, 2, 3, 4, 5, 6])
    for (const size of [3, 4, 5])
      it(`figure-${seed}-${size}.vox`, () => {
        const expected = new Uint8Array(readFileSync(join(dir, "vox", `figure-${seed}-${size}.vox`)));
        expect(generateFigure(BigInt(seed), size).toVox()).toEqual(expected);
      });
});

describe("daily.json", () => {
  const { days } = load("daily.json") as {
    days: { day: number; challengeId: string; seed: string; trials: { kind: string; difficulty: string; seed: string }[] }[];
  };
  it.each(days)("day $day", (row) => {
    expect(challengeId(row.day)).toBe(row.challengeId);
    expect(dailySeed(row.day).toString()).toBe(row.seed);
    const plan = dailyPlan(row.day);
    expect(plan.trials.map((t) => ({ kind: t.kind, difficulty: t.difficulty, seed: t.seed.toString() }))).toEqual(row.trials);
  });
  it("calendar", () => {
    expect(dayNumber(new Date(Date.UTC(2026, 8, 1, 0, 0, 0)))).toBe(1);
    expect(dayNumber(new Date(Date.UTC(2026, 8, 1, 23, 59, 59)))).toBe(1);
    expect(dayNumber(new Date(Date.UTC(2026, 8, 24, 12)))).toBe(24);
    expect(dayNumber(new Date(Date.UTC(2026, 7, 31, 23)))).toBe(0);
  });
});

type Diff = PuzzleDifficulty;

describe("puzzles.json spatial", () => {
  const { spatial } = load("puzzles.json") as {
    spatial: { seed: string; difficulty: Diff; hash: string; cubes: number[][]; start: number; targetShadow: number; par: number; solution: number[] }[];
  };
  it.each(spatial)("seed $seed $difficulty", (row) => {
    const p = generateSpatial(BigInt(row.seed), row.difficulty, 0n);
    expect(p.metadata.contentHash).toBe(row.hash);
    expect(p.cubes).toEqual(row.cubes);
    expect(p.startOrientation).toBe(row.start);
    expect(p.targetShadow).toBe(row.targetShadow);
    expect(p.par).toBe(row.par);
    expect(solveSpatial(p.cubes, p.startOrientation, p.targetShadow)).toEqual(row.solution);
    expect(validateSpatial(p, row.solution)).toBe(true);
    expect(validateSpatial(p, [])).toBe(false);
    expect(validateSpatial(p, [7])).toBe(false);
  });
});

describe("puzzles.json pattern", () => {
  const { pattern } = load("puzzles.json") as {
    pattern: { seed: string; difficulty: Diff; hash: string; examples: number[][]; test: number; options: number[]; correct: number }[];
  };
  it.each(pattern)("seed $seed $difficulty", (row) => {
    const p = generatePattern(BigInt(row.seed), row.difficulty, 0n);
    expect(p.metadata.contentHash).toBe(row.hash);
    expect(p.examples).toEqual(row.examples);
    expect(p.testInput).toBe(row.test);
    expect(p.options).toEqual(row.options);
    expect(p.correctOption).toBe(row.correct);
    expect(validatePattern(p, row.correct)).toBe(true);
    expect(validatePattern(p, (row.correct + 1) % 4)).toBe(false);
  });
});

describe("puzzles.json logic", () => {
  const { logic } = load("puzzles.json") as {
    logic: { seed: string; difficulty: Diff; hash: string; size: number; checkpoints: number[]; walls: number[]; solution: number[] }[];
  };
  it.each(logic)("seed $seed $difficulty", (row) => {
    const p = generateLogic(BigInt(row.seed), row.difficulty, 0n);
    expect(p.metadata.contentHash).toBe(row.hash);
    expect(p.size).toBe(row.size);
    expect(p.checkpoints).toEqual(row.checkpoints);
    expect(p.walls).toEqual(row.walls);
    expect(solveLogic(p)).toEqual(row.solution);
    expect(validateLogic(p, row.solution)).toBe(true);
    expect(validateLogic(p, [...row.solution].reverse())).toBe(false);
    expect(validateLogic(p, row.solution.slice(1))).toBe(false);
  });
});
