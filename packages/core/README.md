# @ronriku/core

A TypeScript port of the deterministic RONRIKU domain. The C# code in `client/Assets/Ronriku/Scripts/Domain/` is the source of truth. Each module must match the golden fixtures in `fixtures/` (exported from C#) bit for bit.

| module | C# source | contents |
|---|---|---|
| `rng` | `DeterministicRandom.cs` | xorshift RNG on BigInt (unsigned 64-bit wraparound), FNV-1a 64, `u64`/`i64` casts |
| `figures/figure` | `Figures/Figure.cs` | `Figure`, `encode`/`decode` (RF1), `toVox()` |
| `figures/generator` | `Figures/FigureGenerator.cs` | `generateFigure(seed: bigint, size, monster)` |
| `figures/raster` | — | pure isometric pixel rasterizer (RGBA buffer, browser-safe) |
| `figures/render` (`@ronriku/core/render`) | — | `renderFigurePng(figure)` → 256×256 PNG (pngjs, Node only) |
| `daily` | `Daily/DailyPlan.cs` | `dayNumber`, `challengeId`, `dailySeed`, `mix` (SplitMix64), `dailyPlan(day)` |
| `puzzles/*` | `Puzzles/*.cs` | generators + solvers + replay validators for Spatial / Pattern / Logic; `generateTrial` / `validateTrial` |
| `levels` | `Adventure/Adventure.cs` (`LevelDef`) | `levelDef(world, index)`, `worldSeed`, `bossStages` |
| `bosses` | `Adventure/BossEvents.cs` | `bossEventsForWeek(date)` weekly roster |
| `shop` | `Shop/Shop.cs` | `shopShelf(day)` daily shelf of 6 figures |
| `economy` | `Adventure.cs` (`Economy`) | reward and price constants |

Seeds are `bigint`. C# `long` values (daily/level seeds) are signed and C# `ulong` values (figure seeds) are unsigned. Every generator applies `u64()`, so either form works as input.

## Commands

```bash
pnpm install                              # from the repo root
pnpm --filter @ronriku/core test          # vitest: all fixtures + render/levels/shop/boss tests
pnpm --filter @ronriku/core typecheck
pnpm --filter @ronriku/core build         # tsc → dist/
```

## Using it

- **Node / server:** `import { generateFigure } from "@ronriku/core"` resolves to `dist/`, so run `build` first. The PNG renderer is `import { renderFigurePng } from "@ronriku/core/render"`.
- **Next.js:** add `transpilePackages: ["@ronriku/core"]`. Each export also has a `source` condition that points at `src/*.ts`, and relative imports use `.js` extensions (NodeNext style).
- The main entry does not depend on Node. `rasterizeFigure()` returns RGBA that a canvas can draw.

## Renderer

The renderer draws 2:1 pixel isometric with the camera at (+x, −y, +z). The figure's front (y = 0) is the left face. Faces are flat-shaded: top ×1.0, left ×0.8, right ×0.62. Voxels are 4 px on a 64×64 base canvas, upscaled ×4 with nearest neighbour to 256×256. The background is transparent.
