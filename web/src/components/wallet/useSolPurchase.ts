"use client";

import { useCallback, useState } from "react";
import bs58 from "bs58";
import { useConnection, useWallet } from "@solana/wallet-adapter-react";
import { Transaction } from "@solana/web3.js";
import { accessToken } from "@/lib/api";
import { publicEnv } from "@/lib/env";
import { inspectPreparedTx } from "@/lib/solana/purchaseTx";
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

interface PrepareResponse {
  ok?: boolean;
  code?: string;
  error?: string;
  transaction?: string;
  lastValidBlockHeight?: number;
  lamports?: number;
  wallet?: string;
  recipient?: string;
}

function fromBase64(b64: string): Uint8Array {
  const bin = atob(b64);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

const LABEL: Record<Item, string> = { "figure-legendary": "Legendary figure", "mint-fee": "Mint to wallet", "boss-entry": "Raid entry" };

/**
 * One transaction per purchase (see lib/solana/purchaseTx.ts):
 *  1. server prepare: all pre-checks, price from the server; returns a transaction with the payment to
 *     the recipient (+ memo, + Core mint for figures) already signed by the server — nothing is paid if it fails;
 *  2. connected wallet must be the profile's linked wallet (C2) and the transaction is inspected locally;
 *  3. sign (buyer = fee payer, pays mint rent) → persist the signature locally → broadcast (H3: resumable);
 *  4. confirm → POST the signature; a failed POST stays in the resume banner.
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
        const p = (await fetch("/api/purchase/sol/prepare", {
          method: "POST",
          cache: "no-store",
          headers: { "content-type": "application/json", authorization: `Bearer ${token}` },
          body: JSON.stringify({ item, ...ref }),
        })
          .then((r) => r.json())
          .catch(() => null)) as PrepareResponse | null;
        if (!p?.ok || !p.transaction || !p.lamports || !p.wallet || !p.recipient || !p.lastValidBlockHeight) {
          throw new Error(p?.error ?? "Could not prepare this purchase — nothing was charged.");
        }
        if (!publicEnv.paymentRecipient || p.recipient !== publicEnv.paymentRecipient) throw new Error("Payment recipient mismatch — refusing to pay.");
        if (p.wallet !== publicKey.toBase58()) {
          throw new Error(`Connected wallet ${shortAddress(publicKey.toBase58())} is not your linked wallet ${shortAddress(p.wallet)}. Switch wallets first — nothing was charged.`);
        }
        const tx = Transaction.from(fromBase64(p.transaction));
        const problem = inspectPreparedTx(tx, { buyer: p.wallet, recipient: p.recipient, lamports: p.lamports, mint: item !== "boss-entry" });
        if (problem) throw new Error(`${problem} Nothing was charged.`);
        const blockhash = tx.recentBlockhash!;
        const lastValidBlockHeight = p.lastValidBlockHeight;

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
