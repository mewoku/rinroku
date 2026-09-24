import type { ReactNode } from "react";

/** Compact stat: pixel label above a big tabular number. */
export function StatTile({ label, value, hint, tone }: { label: string; value: ReactNode; hint?: string; tone?: "accent" | "yellow" | "text" }) {
  const color = tone === "yellow" ? "text-yellow" : tone === "accent" ? "text-accent" : "text-text";
  return (
    <div className="px-panel flex min-w-0 flex-col gap-1 p-3">
      <span className="font-pixel text-[10px] leading-3 uppercase text-muted">{label}</span>
      <span className={`tabular font-pixel text-[20px] leading-6 ${color}`}>{value}</span>
      {hint && <span className="text-[12px] leading-4 text-muted">{hint}</span>}
    </div>
  );
}
