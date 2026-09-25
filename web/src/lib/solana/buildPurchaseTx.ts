/**
 * Builds and partially signs the single purchase transaction described in purchaseTx.ts.
 * Server-side (and scripts/devnet-dry-run.ts); holds no secrets itself — the caller passes keypairs.
 */
import { createHmac } from "node:crypto";
import { createUmi } from "@metaplex-foundation/umi-bundle-defaults";
import { createV2, mplCore } from "@metaplex-foundation/mpl-core";
import { createNoopSigner, publicKey as umiPk, type Umi } from "@metaplex-foundation/umi";
import { Keypair, PublicKey, SystemProgram, Transaction, TransactionInstruction } from "@solana/web3.js";
import { MEMO_PROGRAM_ID } from "./purchaseTx";

let umi: Umi | null = null;
/** Umi is only used to encode the CreateV2 instruction; it never talks to an RPC here. */
function encoder(): Umi {
  return (umi ??= createUmi("http://127.0.0.1:0").use(mplCore()));
}

/** Deterministic ed25519 keypair from a secret and a label (HMAC-SHA256 → 32-byte seed). */
export function deriveKeypair(secret: Uint8Array, label: string): Keypair {
  return Keypair.fromSeed(new Uint8Array(createHmac("sha256", Buffer.from(secret)).update(label).digest()));
}

/** Per-figure Core asset keypair: fixed address per figure, so a figure can be created on-chain only once. */
export function assetKeypair(secret: Uint8Array, figureId: string): Keypair {
  return deriveKeypair(secret, `ronriku:core-asset:v1:${figureId}`);
}

/** Opaque per-user tag for the on-chain memo (does not reveal the Supabase user id). */
export function userTag(secret: Uint8Array, userId: string): string {
  return createHmac("sha256", Buffer.from(secret)).update(`ronriku:user-tag:v1:${userId}`).digest("hex").slice(0, 16);
}

/** Deterministic UUID for a SOL-bought Legendary (user, day, slot): known before the figure row exists. */
export function legendaryFigureId(secret: Uint8Array, userId: string, day: number, slot: number): string {
  const h = createHmac("sha256", Buffer.from(secret)).update(`ronriku:legendary-figure:v1:${userId}:${day}:${slot}`).digest();
  h[6] = (h[6]! & 0x0f) | 0x40; // version 4 layout
  h[8] = (h[8]! & 0x3f) | 0x80; // RFC 4122 variant
  const x = h.subarray(0, 16).toString("hex");
  return `${x.slice(0, 8)}-${x.slice(8, 12)}-${x.slice(12, 16)}-${x.slice(16, 20)}-${x.slice(20, 32)}`;
}

export interface BuildPurchaseTx {
  buyer: string;
  recipient: string;
  lamports: number;
  memo: string;
  authority: Keypair;
  mint?: { asset: Keypair; name: string; uri: string };
  blockhash: string;
  lastValidBlockHeight: number;
}

/** Transfer + authority-signed memo (+ Core create), fee payer = buyer, partially signed by the server. */
export function buildPurchaseTx(o: BuildPurchaseTx): Transaction {
  const buyer = new PublicKey(o.buyer);
  const tx = new Transaction({ feePayer: buyer, blockhash: o.blockhash, lastValidBlockHeight: o.lastValidBlockHeight });
  tx.add(SystemProgram.transfer({ fromPubkey: buyer, toPubkey: new PublicKey(o.recipient), lamports: o.lamports }));
  tx.add(
    new TransactionInstruction({
      programId: new PublicKey(MEMO_PROGRAM_ID),
      keys: [{ pubkey: o.authority.publicKey, isSigner: true, isWritable: false }],
      data: Buffer.from(o.memo, "utf8"),
    }),
  );
  const signers = [o.authority];
  if (o.mint) {
    const u = encoder();
    const ixs = createV2(u, {
      asset: createNoopSigner(umiPk(o.mint.asset.publicKey.toBase58())),
      authority: createNoopSigner(umiPk(o.authority.publicKey.toBase58())),
      payer: createNoopSigner(umiPk(o.buyer)),
      owner: umiPk(o.buyer),
      updateAuthority: umiPk(o.authority.publicKey.toBase58()),
      name: o.mint.name.slice(0, 32),
      uri: o.mint.uri,
    }).getInstructions();
    for (const ix of ixs) {
      tx.add(
        new TransactionInstruction({
          programId: new PublicKey(ix.programId),
          keys: ix.keys.map((k) => ({ pubkey: new PublicKey(k.pubkey), isSigner: k.isSigner, isWritable: k.isWritable })),
          data: Buffer.from(ix.data),
        }),
      );
    }
    signers.push(o.mint.asset);
  }
  tx.partialSign(...signers);
  return tx;
}

export function serializePartial(tx: Transaction): string {
  return tx.serialize({ requireAllSignatures: false, verifySignatures: false }).toString("base64");
}
