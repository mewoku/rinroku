"use client";

import { useCallback, useState } from "react";
import { useConnection, useWallet } from "@solana/wallet-adapter-react";
import { PublicKey, SystemProgram, Transaction } from "@solana/web3.js";
import { accessToken } from "@/lib/api";
import { publicEnv } from "@/lib/env";
import { solPriceLamports, type ShopItemId } from "@/lib/economy";

export interface SolPurchaseResult {
  figureId: string | null;
  mintAddress: string | null;
  signature: string;
}

type Item = "figure-legendary" | "boss-entry" | "mint-fee";

/** Pays the treasury on devnet, then asks the server to verify the transfer and mint / record. */
export function useSolPurchase() {
  const { connection } = useConnection();
  const { publicKey, sendTransaction } = useWallet();
  const [busy, setBusy] = useState(false);

  const purchase = useCallback(
    async (item: Item, ref: { figureId?: string; bossId?: string; day?: number; slot?: number } = {}): Promise<SolPurchaseResult> => {
      if (!publicKey) throw new Error("Connect a wallet first.");
      if (!publicEnv.treasuryPubkey) throw new Error("Treasury not configured (NEXT_PUBLIC_TREASURY_PUBKEY).");
      const lamports = solPriceLamports(item as ShopItemId);
      if (!lamports) throw new Error("Item is not purchasable with SOL.");
      const token = await accessToken();
      if (!token) throw new Error("Sign in (play as guest) before buying with SOL.");
      setBusy(true);
      try {
        // Never take payment unless the server can fulfil it.
        const cfg = (await fetch("/api/purchase/sol", { cache: "no-store" }).then((r) => r.json()).catch(() => null)) as { enabled?: boolean; treasury?: string } | null;
        if (!cfg?.enabled) throw new Error("SOL purchases are not enabled on this server yet.");
        if (cfg.treasury !== publicEnv.treasuryPubkey) throw new Error("Treasury mismatch — refusing to pay.");
        const tx = new Transaction().add(
          SystemProgram.transfer({ fromPubkey: publicKey, toPubkey: new PublicKey(publicEnv.treasuryPubkey), lamports }),
        );
        const { blockhash, lastValidBlockHeight } = await connection.getLatestBlockhash("confirmed");
        tx.recentBlockhash = blockhash;
        tx.feePayer = publicKey;
        const signature = await sendTransaction(tx, connection);
        await connection.confirmTransaction({ signature, blockhash, lastValidBlockHeight }, "confirmed");
        const res = await fetch("/api/purchase/sol", {
          method: "POST",
          headers: { "content-type": "application/json", authorization: `Bearer ${token}` },
          body: JSON.stringify({ item, signature, buyer: publicKey.toBase58(), ...ref }),
        });
        const body = (await res.json().catch(() => ({}))) as { error?: string; figureId?: string; mintAddress?: string };
        if (!res.ok) throw new Error(body.error ?? `Purchase failed (${res.status}). Payment signature: ${signature}`);
        return { figureId: body.figureId ?? null, mintAddress: body.mintAddress ?? null, signature };
      } finally {
        setBusy(false);
      }
    },
    [connection, publicKey, sendTransaction],
  );

  return { purchase, busy, connected: !!publicKey };
}
