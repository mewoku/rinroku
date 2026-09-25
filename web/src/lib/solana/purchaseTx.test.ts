import { describe, expect, it } from "vitest";
import { Keypair, PublicKey, SystemProgram, Transaction, TransactionInstruction, type ParsedTransactionWithMeta } from "@solana/web3.js";
import { assetKeypair, buildPurchaseTx, legendaryFigureId, serializePartial, userTag } from "./buildPurchaseTx";
import { checkPurchaseTx, CORE_CREATE_V2_DISCRIMINATOR, inspectPreparedTx, MEMO_PROGRAM_ID, MPL_CORE_PROGRAM_ID, purchaseMemo } from "./purchaseTx";

const authority = Keypair.generate();
const buyer = Keypair.generate().publicKey.toBase58();
const recipient = Keypair.generate().publicKey.toBase58();
const figureId = "44444444-4444-4444-8444-444444444444";
const memo = purchaseMemo("sol_mint_fee", `mint:${figureId}`, userTag(authority.secretKey, "user-1"));
const blockhash = Keypair.generate().publicKey.toBase58(); // any base58 32 bytes

function build(mint = true, lamports = 5_000_000) {
  return buildPurchaseTx({
    buyer,
    recipient,
    lamports,
    memo,
    authority,
    mint: mint ? { asset: assetKeypair(authority.secretKey, figureId), name: "A VERY LONG FIGURE NAME THAT OVERFLOWS #44444444", uri: "http://x/api/figures/f/metadata" } : undefined,
    blockhash,
    lastValidBlockHeight: 100,
  });
}
const roundTrip = (tx: Transaction) => Transaction.from(Buffer.from(serializePartial(tx), "base64"));

describe("buildPurchaseTx", () => {
  it("buyer is fee payer and the only unsigned signer; server authority + asset are pre-signed", () => {
    const tx = roundTrip(build());
    expect(tx.feePayer?.toBase58()).toBe(buyer);
    const sigs = Object.fromEntries(tx.signatures.map((s) => [s.publicKey.toBase58(), !!s.signature]));
    expect(sigs).toEqual({ [buyer]: false, [authority.publicKey.toBase58()]: true, [assetKeypair(authority.secretKey, figureId).publicKey.toBase58()]: true });
  });

  it("the mint authority is never writable, so it can never be charged fees or rent", () => {
    const msg = build().compileMessage();
    const i = msg.accountKeys.findIndex((k) => k.toBase58() === authority.publicKey.toBase58());
    expect(i).toBeGreaterThan(0);
    expect(msg.isAccountWritable(i)).toBe(false);
    expect(msg.accountKeys[0]!.toBase58()).toBe(buyer);
  });

  it("layout: transfer → memo (authority signer) → Core CreateV2 (payer = owner = buyer, update authority = server)", () => {
    const tx = build();
    expect(tx.instructions.map((i) => i.programId.toBase58())).toEqual([SystemProgram.programId.toBase58(), MEMO_PROGRAM_ID, MPL_CORE_PROGRAM_ID]);
    expect(Buffer.from(tx.instructions[1]!.data).toString("utf8")).toBe(memo);
    const create = tx.instructions[2]!;
    expect(create.data[0]).toBe(CORE_CREATE_V2_DISCRIMINATOR);
    // CreateV2 accounts: asset, collection, authority, payer, owner, updateAuthority, system, logWrapper
    const k = create.keys.map((x) => x.pubkey.toBase58());
    expect(k[2]).toBe(authority.publicKey.toBase58());
    expect(k[3]).toBe(buyer);
    expect(k[4]).toBe(buyer);
    expect(k[5]).toBe(authority.publicKey.toBase58());
    expect(create.keys[3]).toMatchObject({ isSigner: true, isWritable: true });
  });

  it("fits in one packet", () => {
    expect(Buffer.from(serializePartial(build()), "base64").length).toBeLessThan(1232);
  });

  it("derivations are deterministic and secret-dependent", () => {
    const other = Keypair.generate().secretKey;
    expect(assetKeypair(authority.secretKey, figureId).publicKey.equals(assetKeypair(authority.secretKey, figureId).publicKey)).toBe(true);
    expect(assetKeypair(other, figureId).publicKey.equals(assetKeypair(authority.secretKey, figureId).publicKey)).toBe(false);
    const id = legendaryFigureId(authority.secretKey, "u", 24, 5);
    expect(id).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
    expect(legendaryFigureId(authority.secretKey, "u", 24, 5)).toBe(id);
    expect(legendaryFigureId(authority.secretKey, "v", 24, 5)).not.toBe(id);
    expect(userTag(authority.secretKey, "user-1")).not.toContain("user-1");
  });
});

describe("inspectPreparedTx (browser, before signing)", () => {
  const exp = { buyer, recipient, lamports: 5_000_000, mint: true };
  it("accepts the server's transaction", () => expect(inspectPreparedTx(roundTrip(build()), exp)).toBeNull());
  it("accepts a boss payment without mint", () => expect(inspectPreparedTx(roundTrip(build(false)), { ...exp, mint: false })).toBeNull());
  it("rejects a different price", () => expect(inspectPreparedTx(roundTrip(build(true, 6_000_000)), exp)).toMatch(/charges/));
  it("rejects a different fee payer", () => expect(inspectPreparedTx(roundTrip(build()), { ...exp, buyer: recipient })).toMatch(/fee payer/));
  it("rejects a payment to someone else", () => expect(inspectPreparedTx(roundTrip(build()), { ...exp, recipient: buyer })).toMatch(/wrong account/));
  it("rejects an extra drain transfer", () => {
    const tx = build();
    tx.add(SystemProgram.transfer({ fromPubkey: new PublicKey(buyer), toPubkey: authority.publicKey, lamports: 1 }));
    expect(inspectPreparedTx(tx, exp)).toMatch(/wrong account/);
  });
  it("rejects unknown programs (e.g. token approvals)", () => {
    const tx = build();
    tx.add(new TransactionInstruction({ programId: new PublicKey("TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA"), keys: [], data: Buffer.from([4]) }));
    expect(inspectPreparedTx(tx, exp)).toMatch(/unexpected program/);
  });
  it("rejects non-create Core instructions (e.g. transferring an asset the buyer owns)", () => {
    const tx = build();
    tx.add(new TransactionInstruction({ programId: new PublicKey(MPL_CORE_PROGRAM_ID), keys: [], data: Buffer.from([14]) }));
    expect(inspectPreparedTx(tx, exp)).toMatch(/Core instruction/);
  });
  it("rejects a mint when none was expected", () => expect(inspectPreparedTx(roundTrip(build()), { ...exp, mint: false })).toMatch(/mints/));
});

describe("checkPurchaseTx (server, after landing)", () => {
  const asset = assetKeypair(authority.secretKey, figureId).publicKey.toBase58();
  const exp = { memo, recipient, authority: authority.publicKey.toBase58(), minLamports: 5_000_000, asset };
  type Opts = { err?: boolean; authoritySigner?: boolean; memo?: string; lamports?: number; source?: string; noAsset?: boolean; blockTime?: number | null };
  const parsed = (o: Opts = {}) =>
    ({
      blockTime: o.blockTime === undefined ? 1_000_000 : o.blockTime,
      meta: { err: o.err ? { InstructionError: [0, "x"] } : null, innerInstructions: [] },
      transaction: {
        message: {
          accountKeys: [
            { pubkey: new PublicKey(buyer), signer: true, writable: true },
            ...(o.noAsset ? [] : [{ pubkey: new PublicKey(asset), signer: true, writable: true }]),
            { pubkey: authority.publicKey, signer: o.authoritySigner ?? true, writable: false },
          ],
          instructions: [
            { programId: SystemProgram.programId, program: "system", parsed: { type: "transfer", info: { source: o.source ?? buyer, destination: recipient, lamports: o.lamports ?? 5_000_000 } } },
            { programId: new PublicKey(MEMO_PROGRAM_ID), program: "spl-memo", parsed: o.memo ?? memo },
            { programId: new PublicKey(MPL_CORE_PROGRAM_ID), accounts: [], data: "" },
          ],
        },
      },
    }) as unknown as ParsedTransactionWithMeta;

  it("accepts the prepared transaction and reports the fee payer as buyer", () => expect(checkPurchaseTx(parsed(), exp)).toEqual({ ok: true, lamports: 5_000_000, buyer }));
  it("accepts old transactions (no age window: memo + one-time ledger prevent replay)", () => expect(checkPurchaseTx(parsed({ blockTime: 1 }), exp).ok).toBe(true));
  it("rejects a missing transaction", () => expect(checkPurchaseTx(null, exp).ok).toBe(false));
  it("rejects a failed transaction", () => expect(checkPurchaseTx(parsed({ err: true }), exp).ok).toBe(false));
  it("rejects a transaction without block time", () => expect(checkPurchaseTx(parsed({ blockTime: null }), exp).ok).toBe(false));
  it("rejects a transaction the server authority did not sign", () => expect(checkPurchaseTx(parsed({ authoritySigner: false }), exp)).toMatchObject({ ok: false, reason: expect.stringMatching(/not prepared/) }));
  it("rejects a memo for another user or item", () => expect(checkPurchaseTx(parsed({ memo: memo.replace("mint:", "boss:") }), exp)).toMatchObject({ ok: false, reason: expect.stringMatching(/different user or item/) }));
  it("rejects underpayment", () => expect(checkPurchaseTx(parsed({ lamports: 4_999_999 }), exp).ok).toBe(false));
  it("rejects a payment from someone other than the fee payer", () => expect(checkPurchaseTx(parsed({ source: recipient }), exp).ok).toBe(false));
  it("rejects a transaction that did not create the expected asset", () => expect(checkPurchaseTx(parsed({ noAsset: true }), exp).ok).toBe(false));
  it("boss payments need no asset", () => expect(checkPurchaseTx(parsed({ noAsset: true }), { ...exp, asset: null }).ok).toBe(true));
});
