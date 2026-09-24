import type { Metadata } from "next";
import { PageHeader, PageShell } from "@/components/layout/PageShell";
import { BossesView } from "@/components/bosses/BossesView";

export const metadata: Metadata = { title: "Bosses" };

export default function BossesPage() {
  return (
    <PageShell palette="boss" wide>
      <PageHeader kicker="Weekly raids" title="Bosses" />
      <BossesView />
    </PageShell>
  );
}
