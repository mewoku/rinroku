"use client";

import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";

type Tone = "info" | "ok" | "error";
interface ToastItem {
  id: number;
  text: string;
  tone: Tone;
}

const ToastCtx = createContext<(text: string, tone?: Tone) => void>(() => {});

export function useToast() {
  return useContext(ToastCtx);
}

const TONE: Record<Tone, string> = { info: "var(--accent)", ok: "var(--ok)", error: "var(--danger)" };

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([]);
  const push = useCallback((text: string, tone: Tone = "info") => {
    const id = Date.now() + Math.random();
    setItems((xs) => [...xs.slice(-2), { id, text, tone }]);
    setTimeout(() => setItems((xs) => xs.filter((x) => x.id !== id)), 4200);
  }, []);
  const value = useMemo(() => push, [push]);
  return (
    <ToastCtx.Provider value={value}>
      {children}
      <div aria-live="polite" className="pointer-events-none fixed inset-x-0 bottom-[88px] z-50 flex flex-col items-center gap-3 px-4 md:bottom-6">
        {items.map((t) => (
          <div
            key={t.id}
            role={t.tone === "error" ? "alert" : "status"}
            className="px-rise px-border pointer-events-auto flex max-w-[420px] items-center gap-3 bg-surface-2 px-4 py-3 text-[14px] leading-5"
            style={{ "--pb": TONE[t.tone] } as React.CSSProperties}
          >
            <span aria-hidden="true" className="size-2 shrink-0" style={{ background: TONE[t.tone] }} />
            {t.text}
          </div>
        ))}
      </div>
    </ToastCtx.Provider>
  );
}
