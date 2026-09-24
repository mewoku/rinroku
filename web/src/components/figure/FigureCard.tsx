"use client";

import type { ReactNode } from "react";
import { Badge, rarityColor } from "../ui/Badge";
import { VoxelViewer } from "./VoxelViewer";
import type { FigureRecord } from "@/lib/types";

/** Figure tile: pixel 3D view on a rarity-tinted pedestal + name/tier/rarity. */
export function FigureCard({
  figure,
  footer,
  onOpen,
  size = 128,
  phase = 0,
  highlight = false,
}: {
  figure: FigureRecord;
  footer?: ReactNode;
  onOpen?: () => void;
  size?: number;
  phase?: number;
  highlight?: boolean;
}) {
  const c = rarityColor(figure.rarity);
  return (
    <article className="px-panel group flex flex-col p-0" style={{ "--pb": highlight ? "var(--yellow)" : undefined } as React.CSSProperties}>
      <button
        type="button"
        onClick={onOpen}
        disabled={!onOpen}
        aria-label={`Open ${figure.name || "figure"} details`}
        className="relative grid place-items-center overflow-hidden pt-2 disabled:cursor-default"
        style={{ background: `radial-gradient(60% 55% at 50% 60%, color-mix(in srgb, ${c} 22%, transparent), transparent)` }}
      >
        <VoxelViewer encoding={figure.encoding} size={size} interactive={false} rim={c} phase={phase} label={`${figure.name} voxel figure`} />
        <span className="absolute top-2 left-2 font-pixel text-[10px] text-muted">{figure.tier}×{figure.tier}</span>
        {figure.mintAddress && (
          <span className="absolute top-2 right-2 font-pixel text-[10px] text-frost-accent" title="Minted on devnet" style={{ color: "var(--frost-accent)" }}>
            NFT
          </span>
        )}
      </button>
      <div className="flex flex-col gap-2 border-t-2 border-line p-3">
        <div className="flex items-center justify-between gap-2">
          <h3 className="truncate text-[14px] leading-4 text-text">{figure.name || "UNNAMED"}</h3>
          <Badge rarity={figure.rarity} />
        </div>
        {footer}
      </div>
    </article>
  );
}
