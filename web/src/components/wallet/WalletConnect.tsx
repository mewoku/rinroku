"use client";

import { useState } from "react";
import { useWallet } from "@solana/wallet-adapter-react";
import { WalletReadyState } from "@solana/wallet-adapter-base";
import { PixelButton } from "../ui/PixelButton";
import { Modal } from "../ui/Modal";
import { useToast } from "../ui/Toast";

export function shortAddress(a: string): string {
  return a.length > 10 ? `${a.slice(0, 4)}…${a.slice(-4)}` : a;
}

/** Pixel wallet picker built on @solana/wallet-adapter-react (no default rounded UI). */
export function WalletConnect({ size = "md", variant = "secondary" }: { size?: "sm" | "md" | "lg"; variant?: "primary" | "secondary" }) {
  const { wallets, select, connect, disconnect, publicKey, connecting, wallet } = useWallet();
  const [open, setOpen] = useState(false);
  const toast = useToast();

  if (publicKey) {
    return (
      <PixelButton size={size} variant="secondary" onClick={() => void disconnect()} aria-label="Disconnect wallet" palette="frost">
        <span aria-hidden="true" className="size-2 bg-ok" />
        {shortAddress(publicKey.toBase58())}
      </PixelButton>
    );
  }

  const usable = wallets.filter((w) => w.readyState === WalletReadyState.Installed || w.readyState === WalletReadyState.Loadable);
  const others = wallets.filter((w) => !usable.includes(w));

  return (
    <>
      <PixelButton size={size} variant={variant} onClick={() => setOpen(true)} disabled={connecting} palette="frost">
        {connecting ? "Connecting…" : "Connect wallet"}
      </PixelButton>
      <Modal open={open} onClose={() => setOpen(false)} title="Connect wallet" palette="frost">
        <p className="mb-4 text-[14px] leading-5 text-muted">Solana devnet. No real funds are used.</p>
        <ul className="flex flex-col gap-3">
          {[...usable, ...others].map((w) => {
            const ready = usable.includes(w);
            return (
              <li key={w.adapter.name}>
                <button
                  className="px-border flex min-h-12 w-full items-center gap-3 bg-surface-2 px-4 text-left hover:bg-line disabled:opacity-50"
                  onClick={async () => {
                    if (!ready) {
                      window.open(w.adapter.url, "_blank", "noopener,noreferrer");
                      return;
                    }
                    try {
                      select(w.adapter.name);
                      if (wallet?.adapter.name === w.adapter.name) await connect();
                      setOpen(false);
                    } catch (e) {
                      toast(e instanceof Error ? e.message : "Wallet connection failed", "error");
                    }
                  }}
                >
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img src={w.adapter.icon} alt="" width={24} height={24} className="pixelated size-6" />
                  <span className="font-pixel text-[14px] leading-4">{w.adapter.name}</span>
                  <span className="ml-auto text-[12px] text-muted">{ready ? "Detected" : "Install"}</span>
                </button>
              </li>
            );
          })}
          {wallets.length === 0 && <li className="text-[14px] text-muted">No wallets found. Install Phantom or Solflare, or open on Seeker.</li>}
        </ul>
      </Modal>
    </>
  );
}
