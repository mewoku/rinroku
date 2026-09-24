"use client";

import type { ReactNode } from "react";
import { VoxelViewer } from "./VoxelViewer";
import { Badge, rarityColor } from "../ui/Badge";
import type { FigureRecord } from "@/lib/types";
import { decodeRf1, filledCount } from "@/lib/rf1";

/** Modal body for a figure: large draggable viewer + facts. */
export function FigureDetail({ figure, children }: { figure: FigureRecord; children?: ReactNode }) {
  const c = rarityColor(figure.rarity);
  let voxels = 0;
  try {
    voxels = filledCount(decodeRf1(figure.encoding));
  } catch {
    voxels = 0;
  }
  const isDemo = figure.id.startsWith("demo-");
  return (
    <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
      <div className="dither-bg px-border grid shrink-0 place-items-center self-center p-2" style={{ background: `radial-gradient(60% 55% at 50% 60%, color-mix(in srgb, ${c} 26%, transparent), #07080b)` }}>
        <VoxelViewer encoding={figure.encoding} size={224} resolution={64} rim={c} label={`${figure.name} figure, drag to rotate`} />
        <p className="pb-1 font-pixel text-[10px] text-muted">DRAG TO ROTATE</p>
      </div>
      <div className="flex min-w-0 flex-1 flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <h3 className="text-[24px] leading-8">{figure.name || "UNNAMED"}</h3>
          <Badge rarity={figure.rarity} />
        </div>
        <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-[14px] leading-5">
          <dt className="text-muted">Tier</dt>
          <dd className="font-pixel text-[12px]">
            {figure.tier}×{figure.tier}×{figure.tier * 2}
          </dd>
          <dt className="text-muted">Voxels</dt>
          <dd className="tabular font-pixel text-[12px]">{voxels}</dd>
          <dt className="text-muted">Seed</dt>
          <dd className="tabular truncate font-pixel text-[12px]">{figure.seed}</dd>
          <dt className="text-muted">On-chain</dt>
          <dd className="truncate font-pixel text-[12px]">
            {figure.mintAddress ? (
              <a className="text-accent underline" href={`https://explorer.solana.com/address/${figure.mintAddress}?cluster=devnet`} target="_blank" rel="noopener noreferrer">
                {figure.mintAddress.slice(0, 6)}…
              </a>
            ) : (
              "off-chain"
            )}
          </dd>
        </dl>
        {!isDemo && (
          <a className="font-pixel text-[10px] text-muted underline hover:text-text" href={`/api/figures/${figure.id}/metadata`} target="_blank" rel="noopener noreferrer">
            METADATA JSON
          </a>
        )}
        {children}
      </div>
    </div>
  );
}
