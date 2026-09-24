"use client";

/**
 * Paid-but-not-yet-confirmed SOL purchases, kept in localStorage so a closed tab, a crash or a
 * server error never leaves the buyer with nothing: the signature is saved *before* the transaction
 * is broadcast, and the resume banner re-posts it (the server resumes idempotently).
 */
export interface PendingPurchase {
  signature: string;
  body: { item: "figure-legendary" | "mint-fee" | "boss-entry"; signature: string; figureId?: string; bossId?: string; day?: number; slot?: number };
  label: string;
  createdAt: number;
  lastError?: string;
}

const KEY = "ronriku:pending-purchases:v1";
const EVENT = "ronriku:pending-purchases";

export function listPending(): PendingPurchase[] {
  try {
    const raw = localStorage.getItem(KEY);
    const arr = raw ? (JSON.parse(raw) as PendingPurchase[]) : [];
    return Array.isArray(arr) ? arr : [];
  } catch {
    return [];
  }
}

function write(list: PendingPurchase[]) {
  try {
    localStorage.setItem(KEY, JSON.stringify(list.slice(-20)));
  } catch {
    /* storage unavailable (private mode): the purchase still proceeds */
  }
  try {
    window.dispatchEvent(new Event(EVENT));
  } catch {
    /* ignore */
  }
}

export function savePending(p: PendingPurchase) {
  write([...listPending().filter((x) => x.signature !== p.signature), p]);
}

export function updatePending(signature: string, patch: Partial<PendingPurchase>) {
  write(listPending().map((x) => (x.signature === signature ? { ...x, ...patch } : x)));
}

export function removePending(signature: string) {
  write(listPending().filter((x) => x.signature !== signature));
}

export function subscribePending(cb: () => void): () => void {
  window.addEventListener(EVENT, cb);
  window.addEventListener("storage", cb);
  return () => {
    window.removeEventListener(EVENT, cb);
    window.removeEventListener("storage", cb);
  };
}

export interface PostResult {
  ok: boolean;
  status: number;
  code?: string;
  error?: string;
  figureId?: string | null;
  mintAddress?: string | null;
  attemptId?: string | null;
}

/** Terminal outcomes: retrying the same signature can never succeed, so the entry can be dropped. */
export const TERMINAL_CODES = new Set(["refund_due", "signature_used", "claim_mismatch"]);

export async function postPurchase(body: PendingPurchase["body"], token: string): Promise<PostResult> {
  const res = await fetch("/api/purchase/sol", {
    method: "POST",
    headers: { "content-type": "application/json", authorization: `Bearer ${token}` },
    body: JSON.stringify(body),
  });
  const json = (await res.json().catch(() => ({}))) as Omit<PostResult, "ok" | "status">;
  return { ok: res.ok, status: res.status, ...json };
}
