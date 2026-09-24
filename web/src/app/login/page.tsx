import type { Metadata } from "next";
import { PageShell } from "@/components/layout/PageShell";
import { LoginView } from "@/components/login/LoginView";

export const metadata: Metadata = { title: "Sign in" };

export default function LoginPage() {
  return (
    <PageShell palette="lab">
      <LoginView />
    </PageShell>
  );
}
