import type { ReactNode } from "react";
import type { Rarity } from "@/lib/types";

const RARITY_VAR: Record<Rarity, string> = {
  Common: "var(--rarity-common)",
  Rare: "var(--rarity-rare)",
  Epic: "var(--rarity-epic)",
  Legendary: "var(--rarity-legendary)",
};

export function rarityColor(r: Rarity): string {
  return RARITY_VAR[r];
}

/** Small pixel tag. Pass `rarity` for rarity colours or `color` for anything else. */
export function Badge({ rarity, color, children, className = "" }: { rarity?: Rarity; color?: string; children?: ReactNode; className?: string }) {
  const c = rarity ? RARITY_VAR[rarity] : (color ?? "var(--muted)");
  return (
    <span
      className={`inline-flex h-5 items-center gap-1 px-1.5 font-pixel text-[10px] leading-3 uppercase ${className}`}
      style={{
        color: c,
        background: `color-mix(in srgb, ${c} 14%, transparent)`,
        boxShadow: `0 -2px 0 0 color-mix(in srgb, ${c} 60%, transparent), 0 2px 0 0 color-mix(in srgb, ${c} 60%, transparent), -2px 0 0 0 color-mix(in srgb, ${c} 60%, transparent), 2px 0 0 0 color-mix(in srgb, ${c} 60%, transparent)`,
        margin: "2px",
      }}
    >
      {rarity === "Legendary" && <span aria-hidden="true">★</span>}
      {children ?? rarity}
    </span>
  );
}

/** Visible marker that the numbers on screen are demo data, not live backend data. */
export function DemoBadge({ reason }: { reason?: string }) {
  return (
    <span title={reason ?? "Backend not reachable — showing demo data"} className="inline-flex items-center gap-2">
      <Badge color="var(--warn)">
        <span className="px-blink" aria-hidden="true">
          ●
        </span>
        Demo data
      </Badge>
      {reason && <span className="sr-only">{reason}</span>}
    </span>
  );
}
