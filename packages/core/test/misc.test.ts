import { PNG } from "pngjs";
import { describe, expect, it } from "vitest";
import { renderFigurePng } from "../src/figures/render.js";
import {
  BOSS_INDEX, applyMove, dailyReward, edgeKey, generateFigure, generateTrial, levelDef, levelReward, mix,
  orientationMatrix, rasterizeFigure, solveLogic, solveSpatial, validateLogic, validateTrial, worldSeed,
} from "../src/index.js";

describe("render", () => {
  it.each([3, 4, 5])("renders a %i-tier figure to a 256x256 transparent PNG", (size) => {
    const png = PNG.sync.read(renderFigurePng(generateFigure(7n, size)));
    expect(png.width).toBe(256);
    expect(png.height).toBe(256);
    let opaque = 0;
    for (let i = 3; i < png.data.length; i += 4) if (png.data[i] === 255) opaque++;
    expect(opaque).toBeGreaterThan(2000);
    expect(png.data[3]).toBe(0); // corner is transparent
  });

  it("uses the three flat face shades", () => {
    const f = generateFigure(1n, 4);
    const img = rasterizeFigure(f);
    const colours = new Set<number>();
    for (let i = 0; i < img.data.length; i += 4)
      if (img.data[i + 3]) colours.add((img.data[i] << 16) | (img.data[i + 1] << 8) | img.data[i + 2]);
    const skin = f.palette[0];
    const k = (rgb: number, s: number) =>
      (Math.round(((rgb >> 16) & 255) * s) << 16) | (Math.round(((rgb >> 8) & 255) * s) << 8) | Math.round((rgb & 255) * s);
    expect(colours.has(k(skin, 1))).toBe(true);
    expect(colours.has(k(skin, 0.8))).toBe(true);
  });
});

describe("spatial orientations", () => {
  it("builds 24 distinct rotations with inverse moves", () => {
    const seen = new Set<string>();
    for (let o = 0; o < 24; o++) {
      seen.add(orientationMatrix(o).join(","));
      expect(applyMove(applyMove(o, 0), 1)).toBe(o);
      expect(applyMove(applyMove(o, 2), 3)).toBe(o);
    }
    expect(seen.size).toBe(24);
  });
});

describe("logic walls", () => {
  it("rejects a path that crosses a wall", () => {
    const data = { size: 2, checkpoints: [1, 0, 0, 2], checkpointCount: 2, walls: [] as number[] };
    const path = [0, 1, 2, 3].length ? [0, 2, 3, 1] : []; // not ending on 2 → invalid
    expect(validateLogic(data, path)).toBe(false);
    const ok = [0, 1, 3, 2];
    expect(validateLogic({ ...data, checkpoints: [1, 0, 2, 0] }, ok)).toBe(true);
    expect(validateLogic({ ...data, checkpoints: [1, 0, 2, 0], walls: [edgeKey(1, 3, 4)] }, ok)).toBe(false);
  });
});

describe("levels", () => {
  it("derives deterministic levels per the C# LevelDef contract", () => {
    const a = levelDef(0, 0);
    expect(a.seed).toBe(mix(worldSeed(0), 0n));
    expect(levelDef(0, 0)).toEqual(a);
    expect([0, 1, 2, 3].map((i) => levelDef(0, i).kind)).toEqual(["Pattern", "Spatial", "Logic", "Pattern"]);
    const boss = levelDef(2, BOSS_INDEX);
    expect(boss.isBoss).toBe(true);
    expect(boss.stages.map((s) => s.kind)).toEqual(["Pattern", "Spatial", "Logic"]);
    expect(boss.monster.tier).toBe(5);
    expect(levelDef(0, 0).monster.tier).toBe(3);
    expect(levelDef(0, 6).monster.tier).toBe(4);
    expect(levelDef(0, 10).monster.tier).toBe(4);
    expect(worldSeed(1)).not.toBe(worldSeed(0));
    expect(() => levelDef(0, 12)).toThrow();
    expect(() => levelDef(5, 0)).toThrow();
  });

  it("every level of every world generates a valid, answerable trial", () => {
    for (let w = 0; w < 5; w++)
      for (let i = 0; i < 12; i++)
        for (const stage of levelDef(w, i).stages) {
          const t = generateTrial(stage.kind, stage.seed, stage.difficulty);
          const answer = t.kind === "Pattern" ? t.data.correctOption
            : t.kind === "Spatial" ? solveSpatial(t.data.cubes, t.data.startOrientation, t.data.targetShadow)
            : solveLogic(t.data);
          expect(validateTrial(t, answer)).toBe(true);
        }
  });
});

describe("economy", () => {
  it("matches PLAN §6", () => {
    expect(levelReward(1, false)).toBe(20);
    expect(levelReward(3, false)).toBe(40);
    expect(levelReward(1, true)).toBe(300);
    expect(dailyReward(0)).toBe(100);
    expect(dailyReward(25)).toBe(200);
  });
});

describe("shop and bosses", () => {
  it("builds the daily shelf per ShopCatalogue.ForDay", async () => {
    const { shopShelf } = await import("../src/index.js");
    const shelf = shopShelf(23);
    expect(shelf.map((i) => i.size)).toEqual([3, 3, 4, 4, 5, 5]);
    expect(shelf[0].id).toBe(`fig-${shelf[0].seed}-3`);
    expect(shopShelf(23).map((i) => i.id)).toEqual(shelf.map((i) => i.id));
    for (const item of shelf) expect(item.priceShards).toBe({ Common: 300, Rare: 800, Epic: 2000, Legendary: -1 }[item.figure.rarity]);
  });

  it("derives the weekly boss roster per BossEvent.ForWeek", async () => {
    const { bossEventsForWeek } = await import("../src/index.js");
    const week = bossEventsForWeek(new Date(Date.UTC(2026, 8, 24, 10))); // Thursday
    expect(week.map((b) => b.id)).toEqual(["boss-w2-0", "boss-w2-1", "boss-w2-2"]);
    expect(week[0].startsUtc.toISOString()).toBe("2026-09-21T00:00:00.000Z");
    expect(week[2].entryShards).toBe(250);
    expect(week[2].reward).toBe(950);
    expect(bossEventsForWeek(new Date(Date.UTC(2026, 8, 27, 23)))[1].seed).toBe(week[1].seed); // Sunday
    expect(bossEventsForWeek(new Date(Date.UTC(2026, 8, 28)))[0].id).toBe("boss-w3-0");
  });
});
