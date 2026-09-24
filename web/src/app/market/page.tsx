import type { Metadata } from "next";
import { PageHeader, PageShell } from "@/components/layout/PageShell";
import { MarketView } from "@/components/market/MarketView";

export const metadata: Metadata = { title: "Market" };

export default function MarketPage() {
  return (
    <PageShell palette="link" wide>
      <PageHeader kicker="Shop" title="Figures" />
      <MarketView />
    </PageShell>
  );
}
