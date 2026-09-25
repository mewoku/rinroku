import "server-only";

import { createClient, type SupabaseClient } from "@supabase/supabase-js";
import { publicEnv } from "../env";
import { serverSupabaseUrl, serviceRoleKey } from "../server/env";

/** Service-role client (bypasses RLS). Server only; null when not configured. */
export function getServiceSupabase(): SupabaseClient | null {
  const key = serviceRoleKey();
  if (!serverSupabaseUrl() || !key) return null;
  return createClient(serverSupabaseUrl(), key, { auth: { persistSession: false, autoRefreshToken: false } });
}

/** Anon client for public reads from route handlers. */
export function getAnonServerSupabase(): SupabaseClient | null {
  if (!serverSupabaseUrl() || !publicEnv.supabaseAnonKey) return null;
  return createClient(serverSupabaseUrl(), publicEnv.supabaseAnonKey, {
    auth: { persistSession: false, autoRefreshToken: false },
  });
}
