"use client";

import { useMemo } from "react";
import { BOSS_INDEX, LEVELS_PER_WORLD, generateFigure, levelDef } from "@ronriku/core";
import { VoxelViewer } from "../figure/VoxelViewer";
import { PALETTES, WORLD_PALETTES } from "@/lib/palettes";

const KIND_LABEL: Record<string, string> = { Pattern: "P", Spatial: "S", Logic: "L" };

/** Adventure overview: 5 worlds × 12 levels; level 12 is the world boss (real guardian from core). */
export function WorldMap() {
  const worlds = useMemo(
    () =>
      WORLD_PALETTES.map((w, i) => {
        const levels = Array.from({ length: LEVELS_PER_WORLD }, (_, k) => levelDef(i, k));
        const boss = levels[BOSS_INDEX]!;
        const monster = generateFigure(boss.monster.seed, boss.monster.tier, true);
        return { ...w, index: i, levels, bossName: monster.name, bossEncoding: monster.encode() };
      }),
    [],
  );

  return (
    <ol className="grid gap-4 md:grid-cols-2 xl:grid-cols-5" aria-label="Adventure worlds">
      {worlds.map((w) => {
        const p = PALETTES[w.palette];
        return (
          <li key={w.name} data-palette={w.palette} data-accent="true" className="px-panel flex flex-col gap-3 p-4">
            <div className="flex items-start justify-between gap-2">
              <div>
                <p className="font-pixel text-[10px] text-muted">WORLD {w.index + 1}</p>
                <h3 className="text-[24px] leading-8 text-accent">{w.name}</h3>
                <p className="text-[13px] leading-5 text-muted">{w.blurb}</p>
              </div>
              <VoxelViewer encoding={w.bossEncoding} size={72} resolution={28} interactive={false} rim={p.accent} phase={w.index} label={`${w.name} boss ${w.bossName}`} />
            </div>
            {/* 12-node path: level 12 is the boss */}
            <div className="flex flex-wrap items-center gap-[6px]" aria-label={`${LEVELS_PER_WORLD} levels`}>
              {w.levels.map((l) => (
                <span
                  key={l.index}
                  title={l.isBoss ? `Boss: ${w.bossName}` : `Level ${l.index + 1} · ${l.kind}`}
                  className="grid place-items-center font-pixel text-[8px] leading-none"
                  style={{
                    width: l.isBoss ? 24 : 16,
                    height: l.isBoss ? 24 : 16,
                    color: "#07080b",
                    background: l.isBoss ? `linear-gradient(135deg, ${PALETTES.boss.accent}, ${PALETTES.boss.accent2})` : l.index % 2 ? p.accent2 : p.accent,
                    boxShadow: l.isBoss ? `0 0 12px -2px ${PALETTES.boss.accent}` : "inset -2px -2px 0 rgb(0 0 0 / 0.3)",
                  }}
                >
                  {l.isBoss ? "☠" : KIND_LABEL[l.kind]}
                </span>
              ))}
            </div>
            <p className="font-pixel text-[10px] text-muted">
              BOSS · <span className="text-text">{w.bossName}</span>
            </p>
          </li>
        );
      })}
    </ol>
  );
}
