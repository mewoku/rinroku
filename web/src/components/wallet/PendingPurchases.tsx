"use client";

import { useEffect, useState } from "react";
import { PixelButton } from "../ui/PixelButton";
import { useToast } from "../ui/Toast";
import { useSession } from "../SessionProvider";
import { accessToken } from "@/lib/api";
import { TERMINAL_CODES, listPending, postPurchase, removePending, subscribePending, updatePending, type PendingPurchase } from "@/lib/pendingPurchases";

/** Resume banner for SOL payments that were sent but not yet delivered (H3). */
export function PendingPurchases() {
  const [items, setItems] = useState<PendingPurchase[]>([]);
  const [busy, setBusy] = useState<string | null>(null);
  const toast = useToast();
  const { refresh } = useSession();

  useEffect(() => {
    const load = () => setItems(listPending());
    load();
    return subscribePending(load);
  }, []);

  if (!items.length) return null;

  const retry = async (p: PendingPurchase) => {
    setBusy(p.signature);
    try {
      const token = await accessToken();
      if (!token) throw new Error("Sign in with the account that paid, then retry.");
      const res = await postPurchase(p.body, token);
      if (res.ok) {
        removePending(p.signature);
        toast(`${p.label} delivered.`, "ok");
        await refresh();
      } else if (res.code && TERMINAL_CODES.has(res.code)) {
        removePending(p.signature);
        toast(res.error ?? "This payment cannot be completed.", "error");
      } else {
        updatePending(p.signature, { lastError: res.error ?? `HTTP ${res.status}` });
        toast(res.error ?? "Still failing — try again shortly.", "error");
      }
    } catch (e) {
      toast(e instanceof Error ? e.message : "Retry failed", "error");
    } finally {
      setBusy(null);
    }
  };

  return (
    <div role="region" aria-label="Unfinished payments" className="border-b-2 border-warn bg-[rgb(74_30_18/0.85)]">
      <ul className="mx-auto flex max-w-[1200px] flex-col gap-2 px-4 py-3">
        {items.map((p) => (
          <li key={p.signature} className="flex flex-wrap items-center gap-3 text-[13px] leading-5">
            <span className="font-pixel text-[10px] text-warn">UNFINISHED PAYMENT</span>
            <span className="text-text">{p.label}</span>
            <a className="font-pixel text-[10px] text-muted underline" href={`https://explorer.solana.com/tx/${p.signature}?cluster=devnet`} target="_blank" rel="noopener noreferrer">
              {p.signature.slice(0, 8)}…
            </a>
            {p.lastError && <span className="text-muted">({p.lastError})</span>}
            <span className="ml-auto flex gap-2">
              <PixelButton size="sm" palette="link" onClick={() => retry(p)} disabled={busy !== null}>
                {busy === p.signature ? "…" : "Resume"}
              </PixelButton>
              <PixelButton
                size="sm"
                variant="ghost"
                onClick={() => {
                  if (window.confirm("Hide this payment? Keep the signature if you need support.")) removePending(p.signature);
                }}
              >
                Dismiss
              </PixelButton>
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
