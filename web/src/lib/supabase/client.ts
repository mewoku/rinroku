"use client";

import { createClient, type SupabaseClient } from "@supabase/supabase-js";
import { publicEnv, supabaseConfigured } from "../env";

let browserClient: SupabaseClient | null = null;

/** Browser Supabase client (anon key, persisted session). Null when not configured. */
export function getBrowserSupabase(): SupabaseClient | null {
  if (!supabaseConfigured()) return null;
  if (!browserClient) {
    browserClient = createClient(publicEnv.supabaseUrl, publicEnv.supabaseAnonKey, {
      auth: { persistSession: true, autoRefreshToken: true, storageKey: "ronriku-auth" },
    });
  }
  return browserClient;
}
