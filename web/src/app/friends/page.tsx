import type { Metadata } from "next";
import { PageHeader, PageShell } from "@/components/layout/PageShell";
import { MeTabs } from "@/components/layout/MeTabs";
import { FriendsView } from "@/components/friends/FriendsView";

export const metadata: Metadata = { title: "Friends" };

export default function FriendsPage() {
  return (
    <PageShell palette="frost">
      <PageHeader kicker="Me" title="Friends" />
      <MeTabs />
      <FriendsView />
    </PageShell>
  );
}
