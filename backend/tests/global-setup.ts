import { createClient } from "@supabase/supabase-js";
import { supabaseEnv } from "../scripts/env.ts";
import { publish } from "../scripts/publish.ts";

/** Makes sure the local DB has today's puzzle keys, shelf, roster and starter pool. */
export default async function setup(): Promise<void> {
  const env = supabaseEnv();
  const db = createClient(env.url, env.serviceRoleKey, { auth: { persistSession: false } });
  await publish(db, { daysBack: 1, daysAhead: 2 });
}
