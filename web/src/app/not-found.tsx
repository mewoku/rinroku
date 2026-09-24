import { PageShell } from "@/components/layout/PageShell";
import { PixelButton } from "@/components/ui/PixelButton";

export default function NotFound() {
  return (
    <PageShell palette="boss">
      <div className="flex flex-col items-center gap-6 py-16 text-center">
        <p className="glow-text font-pixel text-[64px] leading-[64px] text-accent">404</p>
        <p className="text-muted">This tile is off the map.</p>
        <PixelButton href="/">Back home</PixelButton>
      </div>
    </PageShell>
  );
}
