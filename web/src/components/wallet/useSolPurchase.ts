"use client";

import { useCallback, useState } from "react";
import bs58 from "bs58";
import { useConnection, useWallet } from "@solana/wallet-adapter-react";
import { PublicKey, SystemProgram, Transaction } from "@solana/web3.js";
import { accessToken } from "@/lib/api";
import { publicEnv } from "@/lib/env";
import { TERMINAL_CODES, postPurchase, removePending, savePending, updatePending, type PendingPurchase } from "@/lib/pendingPurchases";
import { shortAddress } from "./WalletConnect";

export interface SolPurchaseResult {
  figureId: string | null;
  mintAddress: string | null;
  attemptId: string | null;
  signature: string;
}

type Item = PendingPurchase["body"]["item"];
type Ref = { figureId?: string; bossId?: string; day?: number; slot?: number };

interface QuoteResponse {
  ok?: boolean;
  code?: string;
  error?: string;
  lamports?: number;
  wallet?: string;
  treasury?: string;
}

const LABEL: Record<Item, string> = { "figure-legendary": "Legendary figure", "mint-fee": "Mint to wallet", "boss-entry": "Raid entry" };

/**
 * Pay the treasury on devnet, then ask the server to verify and deliver.
 *  1. server quote (all pre-checks; price comes from the server, M4) — nothing is paid if it fails;
 *  2. connected wallet must be the profile's linked wallet (C2);
 *  3. sign → persist the signature locally → broadcast (H3: always resumable);
 *  4. confirm → POST; a failed POST stays in the resume banner.
 */
export function useSolPurchase() {
  const { connection } = useConnection();
  const { publicKey, sendTransaction, signTransaction } = useWallet();
  const [busy, setBusy] = useState(false);

  const purchase = useCallback(
    async (item: Item, ref: Ref = {}): Promise<SolPurchaseResult> => {
      if (!publicKey) throw new Error("Connect a wallet first.");
      const token = await accessToken();
      if (!token) throw new Error("Sign in (play as guest) before buying with SOL.");
      setBusy(true);
      try {
        const qs = new URLSearchParams({ item, ...Object.fromEntries(Object.entries(ref).map(([k, v]) => [k, String(v)])) });
        const q = (await fetch(`/api/purchase/sol?${qs}`, { cache: "no-store", headers: { authorization: `Bearer ${token}` } })
          .then((r) => r.json())
          .catch(() => null)) as QuoteResponse | null;
        if (!q?.ok || !q.lamports || !q.wallet || !q.treasury) throw new Error(q?.error ?? "Could not check this purchase — nothing was charged.");
        if (q.treasury !== publicEnv.treasuryPubkey) throw new Error("Treasury mismatch — refusing to pay.");
        if (q.wallet !== publicKey.toBase58()) {
          throw new Error(`Connected wallet ${shortAddress(publicKey.toBase58())} is not your linked wallet ${shortAddress(q.wallet)}. Switch wallets first — nothing was charged.`);
        }

        const tx = new Transaction().add(SystemProgram.transfer({ fromPubkey: publicKey, toPubkey: new PublicKey(q.treasury), lamports: q.lamports }));
        const { blockhash, lastValidBlockHeight } = await connection.getLatestBlockhash("confirmed");
        tx.recentBlockhash = blockhash;
        tx.feePayer = publicKey;

        let signature: string;
        const body = { item, ...ref } as PendingPurchase["body"];
        const pending = (sig: string): PendingPurchase => ({ signature: sig, body: { ...body, signature: sig }, label: LABEL[item], createdAt: Date.now() });
        if (signTransaction) {
          const signed = await signTransaction(tx);
          if (!signed.signature) throw new Error("Wallet returned an unsigned transaction.");
          signature = bs58.encode(signed.signature);
          savePending(pending(signature)); // persisted before it can hit the chain
          await connection.sendRawTransaction(signed.serialize(), { preflightCommitment: "confirmed" });
        } else {
          signature = await sendTransaction(tx, connection); // wallets without signTransaction
          savePending(pending(signature));
        }
        await connection.confirmTransaction({ signature, blockhash, lastValidBlockHeight }, "confirmed");

        const res = await postPurchase({ ...body, signature }, token);
        if (res.ok) {
          removePending(signature);
          return { figureId: res.figureId ?? null, mintAddress: res.mintAddress ?? null, attemptId: res.attemptId ?? null, signature };
        }
        if (res.code && TERMINAL_CODES.has(res.code)) removePending(signature);
        else updatePending(signature, { lastError: res.error ?? `HTTP ${res.status}` });
        throw new Error(res.error ?? `Purchase failed (${res.status}). It is saved — retry from the banner.`);
      } finally {
        setBusy(false);
      }
    },
    [connection, publicKey, sendTransaction, signTransaction],
  );

  return { purchase, busy, connected: !!publicKey };
}
