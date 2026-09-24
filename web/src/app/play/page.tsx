import type { Metadata } from "next";
import { PageHeader, PageShell } from "@/components/layout/PageShell";
import { UnityPlayer } from "@/components/play/UnityPlayer";
import { WorldMap } from "@/components/play/WorldMap";
import { SectionTitle } from "@/components/ui/PixelPanel";
import { findUnityBuild } from "@/lib/server/unityBuild";

export const metadata: Metadata = { title: "Play" };
export const dynamic = "force-dynamic";

export default async function PlayPage() {
  const build = await findUnityBuild();
  return (
    <PageShell palette="lab" wide>
      <PageHeader kicker="Play · Adventure" title="World map" />
      <UnityPlayer build={build} />
      <section className="mt-12" aria-label="Worlds">
        <SectionTitle kicker="5 worlds × 12 levels" title="Beat the guardians" />
        <p className="mb-4 max-w-[640px] text-[15px] leading-6 text-muted">
          Walk node to node; every level is guarded by a voxel monster you beat with a puzzle. Level 12 of each world is its boss — three chained puzzles against the clock.
        </p>
        <WorldMap />
      </section>
    </PageShell>
  );
}
