/**
 * Creates the server mint-authority keypair and writes it into web/.env.local (git-ignored):
 *   MINT_AUTHORITY_SECRET_KEY=[64 bytes JSON]   (server only)
 * and sets the public payment recipient if missing:
 *   NEXT_PUBLIC_PAYMENT_RECIPIENT=<base58>      (the owner's wallet; its secret never touches the server)
 *
 * Usage:  pnpm --filter web create-mint-authority [--force] [--recipient <base58>]
 *
 * The mint authority never pays fees or rent (buyers do), so it needs NO SOL. It co-signs prepared
 * purchase transactions and is the update authority of minted Core assets — keep it secret.
 * Migrates an old TREASURY_SECRET_KEY (reused as the authority) and drops NEXT_PUBLIC_TREASURY_PUBKEY.
 * Prints only public keys.
 */
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Keypair, PublicKey } from "@solana/web3.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const envPath = path.join(here, "..", ".env.local");
const force = process.argv.includes("--force");
const ri = process.argv.indexOf("--recipient");
const recipientArg = ri > 1 ? process.argv[ri + 1] : undefined;
const DEFAULT_RECIPIENT = "8SK4YoGYoTgxnpLpevFC5YakBJc2zAZPj8NbQSEv9N9n"; // owner wallet

function upsert(env: string, key: string, value: string): string {
  const line = `${key}=${value}`;
  const re = new RegExp(`^${key}=.*$`, "m");
  return re.test(env) ? env.replace(re, line) : `${env.replace(/\n*$/, "\n")}${line}\n`;
}
const remove = (env: string, key: string) => env.replace(new RegExp(`^${key}=.*\\n?`, "m"), "");
const get = (env: string, key: string) => new RegExp(`^${key}=(.*)$`, "m").exec(env)?.[1]?.trim() || "";

let env = existsSync(envPath) ? readFileSync(envPath, "utf8") : "";
const legacy = /^TREASURY_SECRET_KEY=(\[.+\])$/m.exec(env)?.[1];
const existing = /^MINT_AUTHORITY_SECRET_KEY=(\[.+\])$/m.exec(env)?.[1] ?? legacy;

let kp: Keypair;
if (existing && !force) {
  kp = Keypair.fromSecretKey(Uint8Array.from(JSON.parse(existing) as number[]));
  console.log(legacy && !get(env, "MINT_AUTHORITY_SECRET_KEY") ? "Reusing the old treasury key as mint authority." : "Mint authority already exists (use --force to replace).");
} else {
  kp = Keypair.generate();
}
env = upsert(env, "MINT_AUTHORITY_SECRET_KEY", JSON.stringify(Array.from(kp.secretKey)));
env = remove(remove(env, "TREASURY_SECRET_KEY"), "NEXT_PUBLIC_TREASURY_PUBKEY");

const recipient = recipientArg && !recipientArg.startsWith("--") ? recipientArg : get(env, "NEXT_PUBLIC_PAYMENT_RECIPIENT") || DEFAULT_RECIPIENT;
new PublicKey(recipient); // throws on a malformed address
if (recipient === kp.publicKey.toBase58()) throw new Error("The payment recipient must not be the mint authority.");
env = upsert(env, "NEXT_PUBLIC_PAYMENT_RECIPIENT", recipient);

writeFileSync(envPath, env, { mode: 0o600 });
console.log(`Updated ${envPath}`);
console.log(`Mint authority (server secret, needs no SOL): ${kp.publicKey.toBase58()}`);
console.log(`Payment recipient (public):                  ${recipient}`);
