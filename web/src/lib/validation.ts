import { z } from "zod";

/** base58 alphabet (no 0, O, I, l). */
const BASE58 = /^[1-9A-HJ-NP-Za-km-z]+$/;

export const base58PubkeySchema = z
  .string()
  .min(32)
  .max(44)
  .regex(BASE58, "Must be a base58 public key.");

/** Solana transaction signatures are 64 bytes → 86–88 base58 chars. */
export const txSignatureSchema = z.string().min(64).max(88).regex(BASE58, "Must be a base58 signature.");

export const uuidSchema = z.uuid();

/** Figure ids: real uuids from the `figures` table, or `demo-<seed>-<tier>` for demo fixtures. */
export const figureIdSchema = z.union([uuidSchema, z.string().regex(/^demo-\d{1,19}-[345]$/)]);

export const handleSchema = z
  .string()
  .trim()
  .min(3, "3–20 characters.")
  .max(20, "3–20 characters.")
  .regex(/^[a-zA-Z0-9_]+$/, "Letters, numbers and _ only.");

const purchaseFields = {
  item: z.enum(["figure-legendary", "boss-entry", "mint-fee"]),
  /** Required for mint-fee: the off-chain figure to mint. */
  figureId: uuidSchema.optional(),
  /** Required for boss-entry. */
  bossId: uuidSchema.optional(),
  /** Required for figure-legendary: the Daily shelf (day, slot) of the Legendary being bought. */
  day: z.number().int().min(1).max(100_000).optional(),
  slot: z.number().int().min(0).max(5).optional(),
};

type PurchaseShape = { item: string; figureId?: string; bossId?: string; day?: number; slot?: number };
function refsPresent<T extends z.ZodType<PurchaseShape>>(schema: T) {
  return schema
    .refine((b) => b.item !== "figure-legendary" || (b.day !== undefined && b.slot !== undefined), {
      message: "day and slot are required for figure-legendary.",
      path: ["slot"],
    })
    .refine((b) => b.item !== "mint-fee" || !!b.figureId, { message: "figureId is required for mint-fee.", path: ["figureId"] })
    .refine((b) => b.item !== "boss-entry" || !!b.bossId, { message: "bossId is required for boss-entry.", path: ["bossId"] });
}

/**
 * POST body. There is deliberately no buyer field: the paying wallet is always the caller's
 * linked wallet (profiles.wallet_address), so nobody can claim someone else's transfer.
 */
export const purchaseSolBodySchema = refsPresent(z.object({ ...purchaseFields, signature: txSignatureSchema }).strict());
export type PurchaseSolBody = z.infer<typeof purchaseSolBodySchema>;

/** GET quote query (pre-payment checks), numbers arrive as strings. */
export const purchaseQuoteSchema = refsPresent(
  z
    .object({
      ...purchaseFields,
      day: z.coerce.number().int().min(1).max(100_000).optional(),
      slot: z.coerce.number().int().min(0).max(5).optional(),
    })
    .strict(),
);

export function zodMessage(err: z.ZodError): string {
  return err.issues.map((i) => `${i.path.join(".") || "body"}: ${i.message}`).join("; ");
}
