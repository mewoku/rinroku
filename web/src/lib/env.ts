/** Public runtime config (inlined into the client bundle by Next). Server secrets live in lib/server/env.ts. */
export const publicEnv = {
  supabaseUrl: process.env.NEXT_PUBLIC_SUPABASE_URL ?? "",
  supabaseAnonKey: process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY ?? "",
  solanaRpcUrl: process.env.NEXT_PUBLIC_SOLANA_RPC_URL || "https://api.devnet.solana.com",
  treasuryPubkey: process.env.NEXT_PUBLIC_TREASURY_PUBKEY ?? "",
  siteUrl: (process.env.NEXT_PUBLIC_SITE_URL || "http://localhost:3000").replace(/\/$/, ""),
} as const;

export const SOLANA_CLUSTER = "devnet" as const;

export function supabaseConfigured(): boolean {
  return !!publicEnv.supabaseUrl && !!publicEnv.supabaseAnonKey;
}
