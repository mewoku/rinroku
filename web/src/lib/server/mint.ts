import "server-only";

import { Keypair, PublicKey } from "@solana/web3.js";
import type { ChainDeps } from "./purchase";
import { devnetConnection, PAYMENT_COMMITMENT } from "./solana";
import { assetKeypair, buildPurchaseTx, legendaryFigureId, serializePartial, userTag } from "../solana/buildPurchaseTx";
import { checkPurchaseTx, purchaseMemo } from "../solana/purchaseTx";

/**
 * web3.js + Metaplex Core bindings for the purchase flow. The mint authority only *signs*
 * (memo + Core create authority + asset keypair derivation); the buyer pays every fee and rent.
 */
export function solanaChain(authoritySecret: Uint8Array): ChainDeps {
  const authority = Keypair.fromSecretKey(authoritySecret);
  const connection = devnetConnection();
  const assetKp = (figureId: string) => assetKeypair(authoritySecret, figureId);
  return {
    authority: authority.publicKey.toBase58(),
    assetAddress: (figureId) => assetKp(figureId).publicKey.toBase58(),
    legendaryFigureId: (userId, day, slot) => legendaryFigureId(authoritySecret, userId, day, slot),
    memo: (userId, kind, ref) => purchaseMemo(kind, ref, userTag(authoritySecret, userId)),
    assetExists: async (address) => (await connection.getAccountInfo(new PublicKey(address), PAYMENT_COMMITMENT)) !== null,
    async buildTx({ buyer, recipient, lamports, memo, mint }) {
      const { blockhash, lastValidBlockHeight } = await connection.getLatestBlockhash(PAYMENT_COMMITMENT);
      const tx = buildPurchaseTx({
        buyer,
        recipient,
        lamports,
        memo,
        authority,
        mint: mint && { asset: assetKp(mint.figureId), name: mint.name, uri: mint.uri },
        blockhash,
        lastValidBlockHeight,
      });
      return { transaction: serializePartial(tx), lastValidBlockHeight };
    },
    async verifyTx(signature, expected) {
      const tx = await connection.getParsedTransaction(signature, { commitment: PAYMENT_COMMITMENT, maxSupportedTransactionVersion: 0 });
      return checkPurchaseTx(tx, expected);
    },
  };
}
