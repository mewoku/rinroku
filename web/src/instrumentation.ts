/** Startup checks (Next.js instrumentation hook). Never prints secrets. */
export async function register() {
  if (process.env.NEXT_RUNTIME !== "nodejs") return;
  if (!process.env.MINT_AUTHORITY_SECRET_KEY && !process.env.NEXT_PUBLIC_PAYMENT_RECIPIENT) return; // SOL purchases not configured
  if (process.env.TREASURY_SECRET_KEY || process.env.NEXT_PUBLIC_TREASURY_PUBKEY) {
    console.warn("[odlet] TREASURY_SECRET_KEY / NEXT_PUBLIC_TREASURY_PUBKEY are obsolete: use MINT_AUTHORITY_SECRET_KEY + NEXT_PUBLIC_PAYMENT_RECIPIENT.");
  }
  let reason: string | null = null;
  try {
    const { Keypair, PublicKey } = await import("@solana/web3.js");
    const authority = Keypair.fromSecretKey(Uint8Array.from(JSON.parse(process.env.MINT_AUTHORITY_SECRET_KEY ?? "[]") as number[])).publicKey.toBase58();
    const recipient = new PublicKey(process.env.NEXT_PUBLIC_PAYMENT_RECIPIENT ?? "").toBase58();
    if (recipient === authority) reason = "NEXT_PUBLIC_PAYMENT_RECIPIENT must not be the mint authority";
    else console.log(`[odlet] SOL purchases: payments → ${recipient}, mint authority ${authority} (needs no SOL)`);
  } catch {
    reason = "MINT_AUTHORITY_SECRET_KEY or NEXT_PUBLIC_PAYMENT_RECIPIENT missing or malformed";
  }
  // The purchase routes re-check this on every request (lib/server/env.ts solConfig).
  if (reason) console.error(`[odlet] SOL purchases disabled: ${reason}`);
}
