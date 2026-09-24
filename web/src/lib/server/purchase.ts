import "server-only";

import type { SupabaseClient } from "@supabase/supabase-js";
import { dayNumber, shelfFigure } from "../shelf";
import { publicEnv } from "../env";
import { solPriceLamports } from "../economy";
import type { PurchaseSolBody } from "../validation";
import { mintCoreAsset } from "./mint";
import { verifyTransfer } from "./solana";

export class PurchaseError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
  }
}

/** The Legendary on today's (or yesterday's, grace for day rollover) Daily shelf at `slot`. */
export function legendaryFromShelf(day: number, slot: number, today = dayNumber()) {
  if (day !== today && day !== today - 1) throw new PurchaseError("That shelf has rotated.", 410);
  const item = shelfFigure(day, slot);
  if (item.figure.rarity !== "Legendary") throw new PurchaseError("That shelf slot is not a SOL item.", 400);
  return item;
}

export interface PurchaseOutcome {
  figureId: string | null;
  mintAddress: string | null;
  attemptId?: string | null;
}

/**
 * Verify → claim (ledger row keyed by the unique signature) → fulfil. Safe to retry with the same
 * signature: an existing claim by the same user resumes fulfilment instead of double-minting.
 */
export async function processSolPurchase(
  db: SupabaseClient,
  treasurySecret: Uint8Array,
  userId: string,
  body: PurchaseSolBody,
): Promise<PurchaseOutcome> {
  const price = solPriceLamports(body.item);
  if (!price) throw new PurchaseError("Item not purchasable with SOL.", 400);

  // Pre-checks that don't need the chain.
  const shelf = body.item === "figure-legendary" ? legendaryFromShelf(body.day!, body.slot!) : null;
  let mintFigure: { id: string; name: string; mint_address: string | null } | null = null;
  if (body.item === "mint-fee") {
    const { data } = await db.from("figures").select("id,name,owner_id,mint_address").eq("id", body.figureId!).maybeSingle();
    if (!data || data.owner_id !== userId) throw new PurchaseError("You don't own that figure.", 403);
    mintFigure = { id: String(data.id), name: String(data.name), mint_address: data.mint_address ? String(data.mint_address) : null };
  }

  const { data: prior } = await db.from("transactions").select("user_id").eq("signature", body.signature).maybeSingle();
  if (prior && prior.user_id !== userId) throw new PurchaseError("Payment already claimed.", 409);

  if (!prior) {
    const check = await verifyTransfer(body.signature, { buyer: body.buyer, treasury: publicEnv.treasuryPubkey, minLamports: price });
    if (!check.ok) throw new PurchaseError(check.reason, 402);
    if (body.item === "boss-entry") {
      // enter_boss_sol records the ledger row (signature unique) and opens the attempt.
      const { data, error } = await db.rpc("enter_boss_sol", { p_user: userId, p_boss_id: body.bossId, p_signature: body.signature, p_lamports: check.lamports });
      if (error) throw new PurchaseError(error.message, 409);
      return { figureId: null, mintAddress: null, attemptId: data ? String((data as { id?: string }).id ?? "") || null : null };
    }
    const { error } = await db.from("transactions").insert({
      user_id: userId,
      kind: body.item === "mint-fee" ? "sol_mint_fee" : "sol_figure_purchase",
      shards_delta: 0,
      lamports: check.lamports,
      signature: body.signature,
      ref_id: body.item === "mint-fee" ? body.figureId : null,
    });
    if (error) throw new PurchaseError(/duplicate|unique/i.test(error.message) ? "Payment already claimed." : "Could not record payment.", 409);
  } else if (body.item === "boss-entry") {
    throw new PurchaseError("Boss entry already recorded for this payment.", 409);
  }

  // Fulfil.
  let figureId: string;
  let name: string;
  if (body.item === "mint-fee") {
    if (mintFigure!.mint_address) return { figureId: mintFigure!.id, mintAddress: mintFigure!.mint_address };
    figureId = mintFigure!.id;
    name = mintFigure!.name;
  } else {
    const { seed: rawSeed, tier, figure: fig } = shelf!;
    const seed = BigInt.asIntN(64, rawSeed); // figures.seed is the signed reinterpretation
    const { data: existing } = await db
      .from("figures")
      .select("id,name,mint_address")
      .eq("seed", seed.toString())
      .eq("tier", tier)
      .eq("owner_id", userId)
      .maybeSingle();
    if (existing?.mint_address) return { figureId: String(existing.id), mintAddress: String(existing.mint_address) };
    if (existing) {
      figureId = String(existing.id);
      name = String(existing.name);
    } else {
      const { data, error } = await db
        .from("figures")
        .insert({ seed: seed.toString(), tier, encoding: fig.encode(), rarity: fig.rarity, name: fig.name, owner_id: userId, source: "sol" })
        .select("id,name")
        .single();
      if (error || !data) throw new PurchaseError("Could not create figure (payment recorded — retry to resume).", 500);
      figureId = String(data.id);
      name = String(data.name);
    }
  }

  let mintAddress: string;
  try {
    mintAddress = await mintCoreAsset(treasurySecret, { owner: body.buyer, name: `${name} #${figureId.slice(0, 8)}`, uri: `${publicEnv.siteUrl}/api/figures/${figureId}/metadata` });
  } catch {
    throw new PurchaseError("Mint failed on devnet (payment recorded — retry with the same signature to resume).", 502);
  }
  await db.from("figures").update({ mint_address: mintAddress }).eq("id", figureId);
  return { figureId, mintAddress };
}
