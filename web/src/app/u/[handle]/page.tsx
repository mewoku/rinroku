import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { PageShell } from "@/components/layout/PageShell";
import { ProfileView } from "@/components/profile/ProfileView";
import { handleSchema } from "@/lib/validation";

export async function generateMetadata({ params }: { params: Promise<{ handle: string }> }): Promise<Metadata> {
  const { handle } = await params;
  return { title: `@${decodeURIComponent(handle).slice(0, 20)}` };
}

export default async function ProfilePage({ params }: { params: Promise<{ handle: string }> }) {
  const { handle } = await params;
  const parsed = handleSchema.safeParse(decodeURIComponent(handle));
  if (!parsed.success) notFound();
  return (
    <PageShell palette="frost">
      <ProfileView handle={parsed.data} />
    </PageShell>
  );
}
