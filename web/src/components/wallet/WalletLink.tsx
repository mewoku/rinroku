"use client";

import { useState } from "react";
import bs58 from "bs58";
import { useWallet } from "@solana/wallet-adapter-react";
import { PixelButton } from "../ui/PixelButton";
import { useToast } from "../ui/Toast";
import { useSession } from "../SessionProvider";
import { requestWalletNonce, verifyWalletLink } from "@/lib/api";
import { WalletConnect, shortAddress } from "./WalletConnect";

/** Links the connected wallet to the profile: server nonce → signMessage → `wallet-link` verify. */
export function WalletLink() {
  const { publicKey, signMessage } = useWallet();
  const { profile, online, refresh } = useSession();
  const toast = useToast();
  const [busy, setBusy] = useState(false);

  if (profile?.walletAddress) {
    return (
      <p className="font-pixel text-[12px] text-ok">
        WALLET LINKED · {shortAddress(profile.walletAddress)}
      </p>
    );
  }
  if (!publicKey) return <WalletConnect />;

  const link = async () => {
    if (!signMessage) {
      toast("This wallet cannot sign messages.", "error");
      return;
    }
    setBusy(true);
    try {
      const address = publicKey.toBase58();
      const { message } = await requestWalletNonce();
      const sig = await signMessage(new TextEncoder().encode(message));
      await verifyWalletLink(address, bs58.encode(sig));
      toast("Wallet linked to your profile.", "ok");
      await refresh();
    } catch (e) {
      toast(e instanceof Error ? e.message : "Wallet link failed", "error");
    } finally {
      setBusy(false);
    }
  };

  return (
    <PixelButton onClick={link} disabled={busy || !online || !profile} palette="frost" variant="secondary">
      {busy ? "Sign in wallet…" : `Link ${shortAddress(publicKey.toBase58())}`}
    </PixelButton>
  );
}
