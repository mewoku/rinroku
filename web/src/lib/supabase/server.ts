import "server-only";

import { createClient, type SupabaseClient } from "@supabase/supabase-js";
import { publicEnv } from "../env";
import { serviceRoleKey } from "../server/env";

/** Service-role client (bypasses RLS). Server only; null when not configured. */
export function getServiceSupabase(): SupabaseClient | null {
  const key = serviceRoleKey();
  if (!publicEnv.supabaseUrl || !key) return null;
  return createClient(publicEnv.supabaseUrl, key, { auth: { persistSession: false, autoRefreshToken: false } });
}

/** Anon client for public reads from route handlers. */
export function getAnonServerSupabase(): SupabaseClient | null {
  if (!publicEnv.supabaseUrl || !publicEnv.supabaseAnonKey) return null;
  return createClient(publicEnv.supabaseUrl, publicEnv.supabaseAnonKey, {
    auth: { persistSession: false, autoRefreshToken: false },
  });
}
