"use client";

import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from "react";
import { backendReachable, fetchMyProfile } from "@/lib/api";
import { getBrowserSupabase } from "@/lib/supabase/client";
import type { Profile } from "@/lib/types";

interface SessionState {
  ready: boolean;
  online: boolean;
  userId: string | null;
  profile: Profile | null;
  refresh: () => Promise<void>;
}

const Ctx = createContext<SessionState>({ ready: false, online: false, userId: null, profile: null, refresh: async () => {} });

export function useSession() {
  return useContext(Ctx);
}

export function SessionProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<Omit<SessionState, "refresh">>({ ready: false, online: false, userId: null, profile: null });

  const refresh = useCallback(async () => {
    const online = await backendReachable();
    const client = getBrowserSupabase();
    const userId = online && client ? ((await client.auth.getSession()).data.session?.user.id ?? null) : null;
    let profile: Profile | null = null;
    try {
      profile = userId ? await fetchMyProfile() : null;
    } catch {
      profile = null;
    }
    setState({ ready: true, online, userId, profile });
  }, []);

  useEffect(() => {
    void refresh();
    const client = getBrowserSupabase();
    const sub = client?.auth.onAuthStateChange(() => void refresh());
    return () => sub?.data.subscription.unsubscribe();
  }, [refresh]);

  return <Ctx.Provider value={{ ...state, refresh }}>{children}</Ctx.Provider>;
}
