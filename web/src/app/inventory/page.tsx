import type { Metadata } from "next";
import { PageHeader, PageShell } from "@/components/layout/PageShell";
import { MeTabs } from "@/components/layout/MeTabs";
import { InventoryView } from "@/components/inventory/InventoryView";

export const metadata: Metadata = { title: "Inventory" };

export default function InventoryPage() {
  return (
    <PageShell palette="forest" wide>
      <PageHeader kicker="Me" title="Collection" />
      <MeTabs />
      <InventoryView />
    </PageShell>
  );
}
