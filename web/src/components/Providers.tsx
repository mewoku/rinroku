"use client";

import type { ReactNode } from "react";
import { SessionProvider } from "./SessionProvider";
import { ToastProvider } from "./ui/Toast";
import { WalletProviders } from "./wallet/WalletProviders";

export function Providers({ children }: { children: ReactNode }) {
  return (
    <WalletProviders>
      <SessionProvider>
        <ToastProvider>{children}</ToastProvider>
      </SessionProvider>
    </WalletProviders>
  );
}
