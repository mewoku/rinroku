"use client";

import { useEffect, useState, useSyncExternalStore } from "react";
import type { Sourced } from "./types";

const REDUCED_QUERY = "(prefers-reduced-motion: reduce)";

function subscribeReduced(cb: () => void) {
  const mq = window.matchMedia(REDUCED_QUERY);
  mq.addEventListener("change", cb);
  return () => mq.removeEventListener("change", cb);
}

export function usePrefersReducedMotion(): boolean {
  return useSyncExternalStore(
    subscribeReduced,
    () => window.matchMedia(REDUCED_QUERY).matches,
    () => false,
  );
}

export type AsyncState<T> =
  | { status: "loading"; value: null; error: null }
  | { status: "ready"; value: T; error: null }
  | { status: "error"; value: null; error: string };

/** Minimal async loader for data-layer calls. `deps` re-run the loader. */
export function useAsync<T>(loader: () => Promise<T>, deps: readonly unknown[]): AsyncState<T> & { reload: () => void } {
  const [state, setState] = useState<AsyncState<T>>({ status: "loading", value: null, error: null });
  const [nonce, setNonce] = useState(0);
  useEffect(() => {
    let alive = true;
    setState({ status: "loading", value: null, error: null });
    loader().then(
      (value) => alive && setState({ status: "ready", value, error: null }),
      (e: unknown) => alive && setState({ status: "error", value: null, error: e instanceof Error ? e.message : String(e) }),
    );
    return () => {
      alive = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, nonce]);
  return { ...state, reload: () => setNonce((n) => n + 1) };
}

export function isDemo<T>(s: AsyncState<Sourced<T>>): boolean {
  return s.status === "ready" && s.value.source === "demo";
}
