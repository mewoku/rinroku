"use client";

import { useEffect, useState } from "react";
import { VoxelViewer } from "../figure/VoxelViewer";
import { Badge, rarityColor } from "../ui/Badge";
import { usePrefersReducedMotion } from "@/lib/hooks";
import type { FigureRecord } from "@/lib/types";

/** Three pedestals; the centre one cycles through featured figures. */
export function HeroFigures({ figures }: { figures: FigureRecord[] }) {
  const [i, setI] = useState(0);
  const reduced = usePrefersReducedMotion();
  useEffect(() => {
    if (reduced || figures.length < 2) return;
    const t = setInterval(() => setI((x) => (x + 1) % figures.length), 5200);
    return () => clearInterval(t);
  }, [figures.length, reduced]);
  if (!figures.length) return null;
  const at = (k: number) => figures[(i + k + figures.length) % figures.length]!;
  const centre = at(0);
  const c = rarityColor(centre.rarity);
  return (
    <div className="relative flex items-end justify-center gap-0 sm:gap-2" aria-live="polite">
      <Pedestal fig={at(-1)} size={120} className="hidden translate-y-2 opacity-80 min-[400px]:block" phase={1.2} />
      <div className="flex flex-col items-center">
        <div
          className="relative"
          style={{ background: `radial-gradient(50% 45% at 50% 62%, color-mix(in srgb, ${c} 30%, transparent), transparent)` }}
        >
          <VoxelViewer key={centre.id} encoding={centre.encoding} size={224} resolution={72} rim={c} label={`${centre.name}, ${centre.rarity} ${centre.tier}×${centre.tier} figure`} />
        </div>
        <PedestalBase color={c} width={176} />
        <div className="mt-3 flex items-center gap-2">
          <span className="font-pixel text-[14px] text-text">{centre.name}</span>
          <Badge rarity={centre.rarity} />
        </div>
      </div>
      <Pedestal fig={at(1)} size={120} className="hidden translate-y-2 opacity-80 min-[400px]:block" phase={2.4} />
    </div>
  );
}

function Pedestal({ fig, size, className = "", phase }: { fig: FigureRecord; size: number; className?: string; phase: number }) {
  const c = rarityColor(fig.rarity);
  return (
    <div className={`flex flex-col items-center ${className}`}>
      <VoxelViewer key={fig.id} encoding={fig.encoding} size={size} resolution={40} rim={c} interactive={false} phase={phase} label={`${fig.name} figure`} />
      <PedestalBase color={c} width={Math.round(size * 0.7)} />
    </div>
  );
}

function PedestalBase({ color, width }: { color: string; width: number }) {
  return (
    <div aria-hidden="true" className="-mt-4 flex flex-col items-center">
      <div style={{ width, height: 8, background: `linear-gradient(90deg, ${color}, color-mix(in srgb, ${color} 40%, #07080b))`, boxShadow: `0 0 24px -4px ${color}` }} />
      <div style={{ width: width - 16, height: 8, background: "#14161d", boxShadow: "inset 0 -2px 0 #07080b" }} />
    </div>
  );
}
