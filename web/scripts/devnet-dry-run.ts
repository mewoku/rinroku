/**
 * Devnet end-to-end dry run of the single-transaction purchase (no Supabase needed).
 * Throwaway mint authority with ZERO SOL, throwaway buyer, payment recipient = owner wallet.
 *
 * LIVE mode (buyer has ≥ 0.05 devnet SOL):
 *   1. figure purchase: payment → recipient + authority memo + Core create (payer/owner = buyer)
 *   2. boss-style payment: payment + memo, no mint
 *   3. replay of the same figure: a second create at the deterministic address must fail atomically
 *   Each landed transaction is checked with the same code the server (checkPurchaseTx) and the
 *   browser (inspectPreparedTx) use; the asset is fetched to confirm owner and update authority.
 *
 * SIMULATE mode (airdrop rate-limited, the usual case on the public faucet): the same transactions
 *   are built, inspected, and simulated on devnet with sigVerify=false, using a funded devnet
 *   account as the fee payer. The real System / Memo / Core programs execute (signer checks, rent
 *   from the payer, CreateV2), nothing is written. Force with --simulate.
 *
 * The buyer keypair is kept in the OS temp dir (ronriku-dry-run-buyer.json) so you can fund it at
 * https://faucet.solana.com and simply rerun.
 *
 * Usage:  pnpm --filter web devnet-dry-run [--recipient <base58>] [--simulate]
 */
import { randomUUID } from "node:crypto";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { Connection, Keypair, LAMPORTS_PER_SOL, PublicKey, Transaction, VersionedTransaction } from "@solana/web3.js";
import { createUmi } from "@metaplex-foundation/umi-bundle-defaults";
import { fetchAssetV1, mplCore } from "@metaplex-foundation/mpl-core";
import { publicKey as umiPk } from "@metaplex-foundation/umi";
import { assetKeypair, buildPurchaseTx, legendaryFigureId, serializePartial, userTag } from "../src/lib/solana/buildPurchaseTx";
import { checkPurchaseTx, inspectPreparedTx, purchaseMemo } from "../src/lib/solana/purchaseTx";

const rpc = process.env.NEXT_PUBLIC_SOLANA_RPC_URL || "https://api.devnet.solana.com";
const arg = (name: string) => {
  const i = process.argv.indexOf(name);
  return i > 1 ? process.argv[i + 1] : undefined;
};
const recipient = arg("--recipient") || process.env.NEXT_PUBLIC_PAYMENT_RECIPIENT || "8SK4YoGYoTgxnpLpevFC5YakBJc2zAZPj8NbQSEv9N9n";
const connection = new Connection(rpc, "confirmed");
const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));
const MIN_LIVE = 0.05 * LAMPORTS_PER_SOL;
const buyerFile = path.join(tmpdir(), "ronriku-dry-run-buyer.json");
const msg = (e: unknown) => (e instanceof Error ? e.message.split("\n")[0] : String(e));

function loadBuyer(): Keypair {
  if (existsSync(buyerFile)) return Keypair.fromSecretKey(Uint8Array.from(JSON.parse(readFileSync(buyerFile, "utf8")) as number[]));
  const kp = Keypair.generate();
  writeFileSync(buyerFile, JSON.stringify(Array.from(kp.secretKey)), { mode: 0o600 });
  return kp;
}

async function tryAirdrop(buyer: PublicKey): Promise<boolean> {
  for (const sol of [1, 0.5]) {
    try {
      const sig = await connection.requestAirdrop(buyer, sol * LAMPORTS_PER_SOL);
      await connection.confirmTransaction({ signature: sig, ...(await connection.getLatestBlockhash()) }, "confirmed");
      console.log(`airdrop ${sol} SOL ok: ${sig}`);
      return true;
    } catch (e) {
      console.warn(`airdrop ${sol} SOL failed: ${msg(e)}`);
      await sleep(1500);
    }
  }
  return false;
}

/** A devnet account with SOL to act as fee payer in simulation (fee payer of a recent memo tx). */
async function fundedDevnetAccount(): Promise<PublicKey> {
  const sigs = await connection.getSignaturesForAddress(new PublicKey("MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr"), { limit: 15 });
  for (const s of sigs) {
    const tx = await connection.getTransaction(s.signature, { maxSupportedTransactionVersion: 0 }).catch(() => null);
    const payer = tx?.transaction.message.staticAccountKeys[0];
    if (payer && (await connection.getBalance(payer)) > MIN_LIVE) return payer;
  }
  throw new Error("No funded devnet account found for simulation.");
}

interface Case {
  label: string;
  lamports: number;
  memo: string;
  figureId?: string;
}

async function prepared(buyer: PublicKey, authority: Keypair, c: Case) {
  const { blockhash, lastValidBlockHeight } = await connection.getLatestBlockhash("confirmed");
  const asset = c.figureId ? assetKeypair(authority.secretKey, c.figureId) : null;
  const built = buildPurchaseTx({
    buyer: buyer.toBase58(),
    recipient,
    lamports: c.lamports,
    memo: c.memo,
    authority,
    mint: asset ? { asset, name: `DRYRUN #${c.figureId!.slice(0, 8)}`, uri: `https://example.invalid/api/figures/${c.figureId}/metadata` } : undefined,
    blockhash,
    lastValidBlockHeight,
  });
  // Exactly what the browser receives:
  const wire = serializePartial(built);
  const tx = Transaction.from(Buffer.from(wire, "base64"));
  const problem = inspectPreparedTx(tx, { buyer: buyer.toBase58(), recipient, lamports: c.lamports, mint: !!asset });
  if (problem) throw new Error(`[${c.label}] client inspection failed: ${problem}`);
  const sigs = tx.signatures.map((s) => `${s.publicKey.toBase58().slice(0, 6)}…:${s.signature ? "signed" : "pending"}`).join(", ");
  console.log(`   ${c.label}: ${tx.instructions.length} instructions, ${Buffer.from(wire, "base64").length} bytes, signatures [${sigs}], client inspection OK`);
  return { tx, blockhash, lastValidBlockHeight, asset: asset?.publicKey.toBase58() ?? null };
}

async function simulate(payer: PublicKey, authority: Keypair, c: Case) {
  const { tx } = await prepared(payer, authority, c);
  const vtx = new VersionedTransaction(tx.compileMessage());
  const sim = await connection.simulateTransaction(vtx, { sigVerify: false, replaceRecentBlockhash: true, commitment: "confirmed" });
  const ok = sim.value.err == null;
  console.log(`   ${c.label}: simulation ${ok ? "OK" : `FAILED ${JSON.stringify(sim.value.err)}`}, ${sim.value.unitsConsumed} CU`);
  if (!ok) console.log((sim.value.logs ?? []).map((l) => `      ${l}`).join("\n"));
  return ok;
}

async function live(buyer: Keypair, authority: Keypair, c: Case) {
  const p = await prepared(buyer.publicKey, authority, c);
  p.tx.partialSign(buyer);
  const before = await connection.getBalance(buyer.publicKey);
  try {
    const signature = await connection.sendRawTransaction(p.tx.serialize(), { preflightCommitment: "confirmed" });
    const conf = await connection.confirmTransaction({ signature, blockhash: p.blockhash, lastValidBlockHeight: p.lastValidBlockHeight }, "confirmed");
    if (conf.value.err) throw new Error(JSON.stringify(conf.value.err));
    let parsed = null;
    for (let i = 0; i < 10 && !parsed; i++) {
      parsed = await connection.getParsedTransaction(signature, { commitment: "confirmed", maxSupportedTransactionVersion: 0 });
      if (!parsed) await sleep(1000);
    }
    const check = checkPurchaseTx(parsed, { memo: c.memo, recipient, authority: authority.publicKey.toBase58(), minLamports: c.lamports, asset: p.asset });
    const after = await connection.getBalance(buyer.publicKey);
    console.log(`   ${c.label}: landed ${signature}\n      server check ${JSON.stringify(check)}; buyer paid ${(before - after) / LAMPORTS_PER_SOL} SOL (price + rent + fees)`);
    return { landed: true as const, check, asset: p.asset };
  } catch (e) {
    const after = await connection.getBalance(buyer.publicKey);
    console.log(`   ${c.label}: rejected (${msg(e)}); buyer delta ${(before - after) / LAMPORTS_PER_SOL} SOL`);
    return { landed: false as const };
  }
}

async function main() {
  const authority = Keypair.generate(); // never funded
  const buyer = loadBuyer();
  const userId = randomUUID();
  const tag = userTag(authority.secretKey, userId);
  const figureId = legendaryFigureId(authority.secretKey, userId, 24, 5);
  const figure: Case = { label: "figure purchase", lamports: 5_000_000, memo: purchaseMemo("sol_legendary", "legendary:24:5", tag), figureId };
  const boss: Case = { label: "boss payment", lamports: 10_000_000, memo: purchaseMemo("boss_entry", `boss:${randomUUID()}`, tag) };
  console.log(`rpc ${rpc}\nrecipient ${recipient}\nmint authority (throwaway, never funded) ${authority.publicKey.toBase58()}\nbuyer ${buyer.publicKey.toBase58()} (key kept in ${buyerFile})`);

  let balance = await connection.getBalance(buyer.publicKey);
  if (!process.argv.includes("--simulate") && balance < MIN_LIVE && (await tryAirdrop(buyer.publicKey))) balance = await connection.getBalance(buyer.publicKey);

  if (process.argv.includes("--simulate") || balance < MIN_LIVE) {
    const payer = await fundedDevnetAccount();
    console.log(`SIMULATE mode (buyer balance ${balance / LAMPORTS_PER_SOL} SOL). Fee payer for simulation: ${payer.toBase58()}`);
    const ok = (await simulate(payer, authority, figure)) && (await simulate(payer, authority, boss));
    console.log(ok ? "DRY RUN (SIMULATED) PASSED — fund the buyer above at https://faucet.solana.com and rerun for a live mint." : "DRY RUN (SIMULATED) FAILED");
    process.exit(ok ? 0 : 1);
  }

  console.log(`LIVE mode (buyer balance ${balance / LAMPORTS_PER_SOL} SOL)`);
  const recipientBefore = await connection.getBalance(new PublicKey(recipient));
  const r1 = await live(buyer, authority, figure);
  let assetOk = false;
  if (r1.landed) {
    const a = await fetchAssetV1(createUmi(rpc).use(mplCore()), umiPk(r1.asset!));
    assetOk = a.owner === buyer.publicKey.toBase58() && a.updateAuthority.address === authority.publicKey.toBase58();
    console.log(`      asset ${r1.asset}: owner ${a.owner}, updateAuthority ${a.updateAuthority.address}, name "${a.name}" → ${assetOk ? "OK" : "MISMATCH"}`);
  }
  const r2 = await live(buyer, authority, boss);
  const r3 = await live(buyer, authority, { ...figure, label: "replay same figure (must fail)" });
  const authBal = await connection.getBalance(authority.publicKey);
  const recv = (await connection.getBalance(new PublicKey(recipient))) - recipientBefore;
  console.log(`mint authority balance ${authBal} lamports (must be 0); recipient received ${recv / LAMPORTS_PER_SOL} SOL`);
  const pass = r1.landed && r1.check.ok && assetOk && r2.landed && r2.check.ok && !r3.landed && authBal === 0;
  console.log(pass ? "DRY RUN (LIVE) PASSED" : "DRY RUN (LIVE) FAILED");
  process.exit(pass ? 0 : 1);
}

main().catch((e) => {
  console.error(msg(e));
  process.exit(1);
});
