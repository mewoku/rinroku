/**
 * NEXT_PUBLIC_SUPABASE_URL=same-origin: the Supabase API is reverse-proxied under this site's own
 * origin (deploy/Caddyfile), so the browser uses window.location.origin and one image works on any
 * domain. Server code then needs SUPABASE_INTERNAL_URL (lib/server/env.ts).
 */
export const SAME_ORIGIN = "same-origin";

function browserSupabaseUrl(raw: string): string {
  if (raw !== SAME_ORIGIN) return raw;
  return typeof window === "undefined" ? "" : window.location.origin;
}

/** Public runtime config (inlined into the client bundle by Next). Server secrets live in lib/server/env.ts. */
export const publicEnv = {
  supabaseUrl: browserSupabaseUrl(process.env.NEXT_PUBLIC_SUPABASE_URL ?? ""),
  supabaseAnonKey: process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY ?? "",
  solanaRpcUrl: process.env.NEXT_PUBLIC_SOLANA_RPC_URL || "https://api.devnet.solana.com",
  /** Wallet that receives SOL payments (public address; its secret key never touches this server). */
  paymentRecipient: process.env.NEXT_PUBLIC_PAYMENT_RECIPIENT ?? "",
  /** Canonical public origin (metadataBase, on-chain NFT metadata URIs). Set it to http://localhost:3000 in dev. */
  siteUrl: (process.env.NEXT_PUBLIC_SITE_URL || "https://odlet.xyz").replace(/\/$/, ""),
  /** Public support / privacy contact shown on /privacy and /terms (store listings require one). */
  contactEmail: process.env.NEXT_PUBLIC_CONTACT_EMAIL ?? "",
} as const;

export const SOLANA_CLUSTER = "devnet" as const;

export function supabaseConfigured(): boolean {
  return !!publicEnv.supabaseUrl && !!publicEnv.supabaseAnonKey;
}
