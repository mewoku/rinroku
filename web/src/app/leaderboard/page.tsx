import type { Metadata } from "next";
import { PageHeader, PageShell } from "@/components/layout/PageShell";
import { MeTabs } from "@/components/layout/MeTabs";
import { LeaderboardView } from "@/components/leaderboard/LeaderboardView";

export const metadata: Metadata = { title: "Leaderboard" };

export default function LeaderboardPage() {
  return (
    <PageShell palette="frost">
      <PageHeader kicker="Me" title="Ranks" />
      <MeTabs />
      <LeaderboardView />
    </PageShell>
  );
}
