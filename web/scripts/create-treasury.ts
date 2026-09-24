/**
 * Creates a devnet treasury keypair and writes it into web/.env.local (git-ignored):
 *   TREASURY_SECRET_KEY=[64 bytes JSON]   (server only)
 *   NEXT_PUBLIC_TREASURY_PUBKEY=<base58>  (public)
 * then requests a 2 SOL devnet airdrop.
 *
 * Usage:  pnpm --filter web create-treasury [--force]
 *
 * The public devnet faucet is rate-limited (often 1–2 requests per IP per ~8 h, sometimes fully
 * throttled). If the airdrop fails, fund the printed address at https://faucet.solana.com
 * (GitHub login raises the limit) or with `solana airdrop 2 <address> --url devnet`.
 * The treasury only needs SOL for Core mint rent + fees (~0.003 SOL per mint).
 */
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Connection, Keypair, LAMPORTS_PER_SOL } from "@solana/web3.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const envPath = path.join(here, "..", ".env.local");
const force = process.argv.includes("--force");
const rpc = process.env.NEXT_PUBLIC_SOLANA_RPC_URL || "https://api.devnet.solana.com";

function upsert(env: string, key: string, value: string): string {
  const line = `${key}=${value}`;
  const re = new RegExp(`^${key}=.*$`, "m");
  return re.test(env) ? env.replace(re, line) : `${env.replace(/\n*$/, "\n")}${line}\n`;
}

async function main() {
  let env = existsSync(envPath) ? readFileSync(envPath, "utf8") : "";
  const existing = /^TREASURY_SECRET_KEY=(\[.+\])$/m.exec(env)?.[1];
  let kp: Keypair;
  if (existing && !force) {
    kp = Keypair.fromSecretKey(Uint8Array.from(JSON.parse(existing) as number[]));
    console.log("Treasury already exists in .env.local (use --force to replace).");
  } else {
    kp = Keypair.generate();
    env = upsert(env, "TREASURY_SECRET_KEY", JSON.stringify(Array.from(kp.secretKey)));
    env = upsert(env, "NEXT_PUBLIC_TREASURY_PUBKEY", kp.publicKey.toBase58());
    writeFileSync(envPath, env, { mode: 0o600 });
    console.log(`Wrote treasury to ${envPath}`);
  }
  const address = kp.publicKey.toBase58();
  console.log(`Treasury (devnet): ${address}`);

  const connection = new Connection(rpc, "confirmed");
  try {
    const sig = await connection.requestAirdrop(kp.publicKey, 2 * LAMPORTS_PER_SOL);
    const bh = await connection.getLatestBlockhash();
    await connection.confirmTransaction({ signature: sig, ...bh }, "confirmed");
    console.log(`Airdropped 2 SOL: ${sig}`);
  } catch (e) {
    console.warn(`Airdrop failed (${e instanceof Error ? e.message : String(e)}).`);
    console.warn(`Fund it manually: https://faucet.solana.com  →  ${address}`);
  }
  const balance = await connection.getBalance(kp.publicKey).catch(() => null);
  if (balance != null) console.log(`Balance: ${balance / LAMPORTS_PER_SOL} SOL`);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
