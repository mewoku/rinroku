/**
 * Local Supabase connection settings: environment (or backend/.env.local) first, otherwise read from
 * `npx supabase status -o json` in backend/. Never hard-codes keys.
 */
import { execSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

export interface SupabaseEnv {
  url: string;
  anonKey: string;
  serviceRoleKey: string;
}

const backendDir = join(dirname(fileURLToPath(import.meta.url)), "..");

function loadDotEnv(): Record<string, string> {
  const file = join(backendDir, ".env.local");
  if (!existsSync(file)) return {};
  const out: Record<string, string> = {};
  for (const line of readFileSync(file, "utf8").split(/\r?\n/)) {
    const m = /^\s*([A-Z0-9_]+)\s*=\s*(.*)\s*$/.exec(line);
    if (m && m[2]) out[m[1]] = m[2].replace(/^["']|["']$/g, "");
  }
  return out;
}

let cached: SupabaseEnv | undefined;

export function supabaseEnv(): SupabaseEnv {
  if (cached) return cached;
  const env = { ...loadDotEnv(), ...process.env };
  if (env.SUPABASE_URL && env.SUPABASE_ANON_KEY && env.SUPABASE_SERVICE_ROLE_KEY) {
    cached = { url: env.SUPABASE_URL, anonKey: env.SUPABASE_ANON_KEY, serviceRoleKey: env.SUPABASE_SERVICE_ROLE_KEY };
    return cached;
  }
  const raw = execSync("npx supabase status -o json", { cwd: backendDir, encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] });
  const status = JSON.parse(raw.slice(raw.indexOf("{")));
  cached = { url: status.API_URL, anonKey: status.ANON_KEY, serviceRoleKey: status.SERVICE_ROLE_KEY };
  return cached;
}
