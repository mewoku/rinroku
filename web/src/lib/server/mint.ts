import "server-only";

import { createUmi } from "@metaplex-foundation/umi-bundle-defaults";
import { create, mplCore } from "@metaplex-foundation/mpl-core";
import { generateSigner, keypairIdentity, publicKey } from "@metaplex-foundation/umi";
import { publicEnv } from "../env";

/** Mints a Metaplex Core asset owned by `owner`, paid for and update-authorised by the treasury. */
export async function mintCoreAsset(treasurySecret: Uint8Array, params: { owner: string; name: string; uri: string }): Promise<string> {
  const umi = createUmi(publicEnv.solanaRpcUrl).use(mplCore());
  umi.use(keypairIdentity(umi.eddsa.createKeypairFromSecretKey(treasurySecret)));
  const asset = generateSigner(umi);
  await create(umi, {
    asset,
    name: params.name.slice(0, 32),
    uri: params.uri,
    owner: publicKey(params.owner),
  }).sendAndConfirm(umi, { confirm: { commitment: "confirmed" } });
  return asset.publicKey.toString();
}

export function treasuryPublicKey(treasurySecret: Uint8Array): string {
  const umi = createUmi(publicEnv.solanaRpcUrl);
  return umi.eddsa.createKeypairFromSecretKey(treasurySecret).publicKey.toString();
}
