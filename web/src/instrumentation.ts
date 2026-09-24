/** Startup checks (Next.js instrumentation hook). Never prints secrets. */
export async function register() {
  if (process.env.NEXT_RUNTIME !== "nodejs") return;
  const raw = process.env.TREASURY_SECRET_KEY;
  const pub = process.env.NEXT_PUBLIC_TREASURY_PUBKEY;
  if (!raw && !pub) return; // SOL purchases not configured
  let reason: string | null = null;
  try {
    const { Keypair } = await import("@solana/web3.js");
    const derived = Keypair.fromSecretKey(Uint8Array.from(JSON.parse(raw ?? "[]") as number[])).publicKey.toBase58();
    if (derived !== pub) reason = "NEXT_PUBLIC_TREASURY_PUBKEY does not match TREASURY_SECRET_KEY";
  } catch {
    reason = "TREASURY_SECRET_KEY missing or malformed";
  }
  // The purchase route re-checks this on every request (lib/server/env.ts treasuryConfig).
  if (reason) console.error(`[ronriku] SOL purchases disabled: ${reason}`);
}
