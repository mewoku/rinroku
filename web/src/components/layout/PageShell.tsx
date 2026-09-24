import type { ReactNode } from "react";
import { AmbientBackground } from "../ambient/AmbientBackground";
import type { PaletteName } from "@/lib/palettes";

/** Per-page wrapper: sets the palette (CSS vars) and the dithered ambient backdrop. */
export function PageShell({ palette, children, wide = false }: { palette: PaletteName; children: ReactNode; wide?: boolean }) {
  return (
    <div data-palette={palette} className="relative">
      <AmbientBackground palette={palette} />
      <main className={`mx-auto w-full ${wide ? "max-w-[1200px]" : "max-w-[1040px]"} px-4 pt-6 pb-[112px] md:pt-10 md:pb-16`}>{children}</main>
    </div>
  );
}

export function PageHeader({ kicker, title, children }: { kicker: string; title: string; children?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-4 md:mb-8">
      <div>
        <p className="font-pixel text-[12px] leading-4 text-accent uppercase">{kicker}</p>
        <h1 className="glow-text mt-1 text-[32px] leading-10 text-text md:text-[40px] md:leading-[48px]">{title}</h1>
      </div>
      {children && <div className="flex flex-wrap items-center gap-3">{children}</div>}
    </div>
  );
}
