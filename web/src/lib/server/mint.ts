import "server-only";

import { createHmac } from "node:crypto";
import { createUmi } from "@metaplex-foundation/umi-bundle-defaults";
import { create, mplCore } from "@metaplex-foundation/mpl-core";
import { createSignerFromKeypair, keypairIdentity, publicKey, type Umi } from "@metaplex-foundation/umi";
import { publicEnv } from "../env";
import type { ChainDeps } from "./purchase";
import { verifyTransfer } from "./solana";

function umiFor(treasurySecret: Uint8Array): Umi {
  const umi = createUmi(publicEnv.solanaRpcUrl, { commitment: "confirmed" }).use(mplCore());
  umi.use(keypairIdentity(umi.eddsa.createKeypairFromSecretKey(treasurySecret)));
  return umi;
}

/**
 * Deterministic per-figure asset keypair: HMAC(treasury secret, figure id). The address is fixed for
 * a figure, so a second `create` for the same figure fails on-chain (account already exists) — a
 * figure can never be minted twice, even by concurrent retries. Not guessable without the secret.
 */
function assetKeypair(umi: Umi, treasurySecret: Uint8Array, figureId: string) {
  const seed = createHmac("sha256", Buffer.from(treasurySecret)).update(`ronriku:core-asset:v1:${figureId}`).digest();
  return umi.eddsa.createKeypairFromSeed(new Uint8Array(seed));
}

/** Umi / Metaplex Core + web3 bindings for the purchase flow. */
export function solanaChain(treasurySecret: Uint8Array): ChainDeps {
  const umi = umiFor(treasurySecret);
  return {
    verifyTransfer,
    assetAddress: (figureId) => assetKeypair(umi, treasurySecret, figureId).publicKey.toString(),
    assetExists: (address) => umi.rpc.accountExists(publicKey(address), { commitment: "confirmed" }),
    async mint({ figureId, owner, name, uri }) {
      const asset = createSignerFromKeypair(umi, assetKeypair(umi, treasurySecret, figureId));
      await create(umi, { asset, name: name.slice(0, 32), uri, owner: publicKey(owner) }).sendAndConfirm(umi, { confirm: { commitment: "confirmed" } });
      return asset.publicKey.toString();
    },
  };
}
