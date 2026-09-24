"use client";

import { useId, useRef, type KeyboardEvent } from "react";

export interface TabItem<T extends string> {
  id: T;
  label: string;
}

/** Accessible pixel tab bar (roving tabindex, arrow keys). */
export function PixelTabs<T extends string>({
  tabs,
  value,
  onChange,
  label,
  className = "",
}: {
  tabs: readonly TabItem<T>[];
  value: T;
  onChange: (id: T) => void;
  label: string;
  className?: string;
}) {
  const base = useId();
  const refs = useRef<(HTMLButtonElement | null)[]>([]);
  const onKey = (e: KeyboardEvent<HTMLDivElement>) => {
    const i = tabs.findIndex((t) => t.id === value);
    let next = i;
    if (e.key === "ArrowRight") next = (i + 1) % tabs.length;
    else if (e.key === "ArrowLeft") next = (i - 1 + tabs.length) % tabs.length;
    else if (e.key === "Home") next = 0;
    else if (e.key === "End") next = tabs.length - 1;
    else return;
    e.preventDefault();
    const t = tabs[next];
    if (t) {
      onChange(t.id);
      refs.current[next]?.focus();
    }
  };
  return (
    <div role="tablist" aria-label={label} onKeyDown={onKey} className={`flex gap-2 overflow-x-auto p-1 ${className}`}>
      {tabs.map((t, i) => {
        const active = t.id === value;
        return (
          <button
            key={t.id}
            ref={(el) => {
              refs.current[i] = el;
            }}
            role="tab"
            id={`${base}-${t.id}`}
            aria-selected={active}
            tabIndex={active ? 0 : -1}
            onClick={() => onChange(t.id)}
            className={`px-border min-h-10 shrink-0 px-4 font-pixel text-[12px] uppercase transition-colors ${
              active ? "bg-accent text-bg-0" : "bg-surface-1 text-muted hover:text-text"
            }`}
            style={active ? ({ "--pb": "var(--accent)" } as React.CSSProperties) : undefined}
          >
            {t.label}
          </button>
        );
      })}
    </div>
  );
}
