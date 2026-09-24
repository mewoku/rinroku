"use client";

import { useEffect, useMemo, type ReactNode } from "react";
import { ConnectionProvider, WalletProvider } from "@solana/wallet-adapter-react";
import { PhantomWalletAdapter } from "@solana/wallet-adapter-phantom";
import { SolflareWalletAdapter } from "@solana/wallet-adapter-solflare";
import { publicEnv } from "@/lib/env";

let mwaRegistered = false;

/** Registers the Mobile Wallet Adapter (Wallet Standard) so Seeker / Android Chrome sees Seed Vault wallets. */
async function registerMobileWalletAdapter() {
  if (mwaRegistered || typeof window === "undefined") return;
  mwaRegistered = true;
  try {
    const mwa = await import("@solana-mobile/wallet-standard-mobile");
    mwa.registerMwa({
      appIdentity: { name: "RONRIKU", uri: window.location.origin, icon: "/icon.png" },
      authorizationCache: mwa.createDefaultAuthorizationCache(),
      chains: ["solana:devnet"],
      chainSelector: mwa.createDefaultChainSelector(),
      onWalletNotFound: mwa.createDefaultWalletNotFoundHandler(),
    });
  } catch {
    // MWA is optional; desktop browsers simply won't list it.
  }
}

export function WalletProviders({ children }: { children: ReactNode }) {
  const wallets = useMemo(() => [new PhantomWalletAdapter(), new SolflareWalletAdapter()], []);
  useEffect(() => {
    void registerMobileWalletAdapter();
  }, []);
  return (
    <ConnectionProvider endpoint={publicEnv.solanaRpcUrl} config={{ commitment: "confirmed" }}>
      <WalletProvider wallets={wallets} autoConnect>
        {children}
      </WalletProvider>
    </ConnectionProvider>
  );
}
