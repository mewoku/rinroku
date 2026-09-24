import type { Metadata } from "next";
import { PageHeader, PageShell } from "@/components/layout/PageShell";
import { DailyView } from "@/components/daily/DailyView";

export const metadata: Metadata = { title: "Daily" };

export default function DailyPage() {
  return (
    <PageShell palette="pattern">
      <PageHeader kicker="Daily" title="Three trials" />
      <DailyView />
    </PageShell>
  );
}
