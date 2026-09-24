import { describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";
import { Keypair, PublicKey, type ParsedTransactionWithMeta } from "@solana/web3.js";
import { purchaseQuoteSchema, purchaseSolBodySchema } from "@/lib/validation";
import { checkTransfer } from "@/lib/server/solana";
import { clientKey, TokenBucket } from "@/lib/server/rateLimit";

// Keep the route in "not configured" mode regardless of the developer's .env.local.
vi.stubEnv("SUPABASE_SERVICE_ROLE_KEY", "");
vi.stubEnv("TREASURY_SECRET_KEY", "");

const { POST, GET } = await import("./purchase/sol/route");
const metadata = await import("./figures/[id]/metadata/route");

const SIG = "5".repeat(87);
const BUYER = Keypair.generate().publicKey.toBase58();
const LEGENDARY = { item: "figure-legendary", signature: SIG, day: 24, slot: 5 };

function post(body: unknown, headers: Record<string, string> = {}) {
  return POST(
    new NextRequest("http://localhost/api/purchase/sol", {
      method: "POST",
      headers: { "content-type": "application/json", ...headers },
      body: typeof body === "string" ? body : JSON.stringify(body),
    }),
  );
}

describe("POST /api/purchase/sol input validation", () => {
  it("rejects non-JSON content type", async () => {
    expect((await post("{}", { "content-type": "text/plain" })).status).toBe(415);
  });

  it("rejects malformed JSON", async () => {
    expect((await post("{nope")).status).toBe(400);
  });

  it("rejects oversized bodies by declared content-length before reading (M2)", async () => {
    expect((await post("{}", { "content-length": "999999" })).status).toBe(413);
  });

  it("rejects oversized streamed bodies without a content-length (M2)", async () => {
    expect((await post({ ...LEGENDARY, pad: "x".repeat(5000) })).status).toBe(413);
  });

  it.each([
    ["missing everything", {}],
    ["unknown item", { item: "figure-free", signature: SIG }],
    ["bad signature charset", { item: "mint-fee", signature: "0OIl".repeat(22), figureId: crypto.randomUUID() }],
    ["short signature", { item: "mint-fee", signature: "abc", figureId: crypto.randomUUID() }],
    ["mint-fee without figureId", { item: "mint-fee", signature: SIG }],
    ["mint-fee with non-uuid figureId", { item: "mint-fee", signature: SIG, figureId: "1; drop table" }],
    ["legendary without slot", { item: "figure-legendary", signature: SIG, day: 24 }],
    ["legendary with slot out of range", { ...LEGENDARY, slot: 6 }],
    ["boss-entry without bossId", { item: "boss-entry", signature: SIG }],
    ["a client-chosen buyer (C2: buyer comes from the linked wallet only)", { ...LEGENDARY, buyer: BUYER }],
    ["unexpected field", { ...LEGENDARY, lamports: 1 }],
  ])("400 on %s", async (_name, body) => {
    const res = await post(body);
    expect(res.status).toBe(400);
    expect((await res.json()).error).toBeTruthy();
  });

  it("401 without a bearer token for a valid body", async () => {
    expect((await post(LEGENDARY)).status).toBe(401);
  });

  it("503 when the server has no service role / treasury configured", async () => {
    expect((await post(LEGENDARY, { authorization: `Bearer ${"x".repeat(40)}` })).status).toBe(503);
  });

  it("GET reports purchases disabled without config", async () => {
    const res = await GET(new NextRequest("http://localhost/api/purchase/sol"));
    expect(await res.json()).toMatchObject({ enabled: false, treasury: null });
  });

  it("GET quote validates its query and is disabled without config", async () => {
    const res = await GET(new NextRequest("http://localhost/api/purchase/sol?item=figure-legendary&day=24&slot=5"));
    expect(res.status).toBe(503);
    expect(purchaseQuoteSchema.safeParse({ item: "figure-legendary", day: "24", slot: "9" }).success).toBe(false);
    expect(purchaseQuoteSchema.safeParse({ item: "figure-legendary", day: "24", slot: "5" }).success).toBe(true);
  });
});

describe("treasury config (startup check)", () => {
  it("disables purchases when NEXT_PUBLIC_TREASURY_PUBKEY does not match the secret", async () => {
    const kp = Keypair.generate();
    vi.resetModules();
    vi.stubEnv("TREASURY_SECRET_KEY", JSON.stringify(Array.from(kp.secretKey)));
    vi.stubEnv("NEXT_PUBLIC_TREASURY_PUBKEY", Keypair.generate().publicKey.toBase58());
    const env = await import("@/lib/server/env");
    expect(env.treasuryConfig()).toMatchObject({ ok: false, reason: expect.stringMatching(/does not match/) });
    vi.resetModules();
    vi.stubEnv("NEXT_PUBLIC_TREASURY_PUBKEY", kp.publicKey.toBase58());
    const env2 = await import("@/lib/server/env");
    expect(env2.treasuryConfig()).toMatchObject({ ok: true, pubkey: kp.publicKey.toBase58() });
    vi.stubEnv("TREASURY_SECRET_KEY", "");
    vi.stubEnv("NEXT_PUBLIC_TREASURY_PUBKEY", "");
  });
});

describe("rate limiting (M1)", () => {
  it("ignores forwarding headers unless TRUSTED_PROXY=1", () => {
    const h = new Headers({ "x-forwarded-for": "6.6.6.6, 10.0.0.1" });
    expect(clientKey(h, {})).toBe("direct");
    expect(clientKey(h, { TRUSTED_PROXY: "1" })).toBe("ip:10.0.0.1"); // the hop our proxy appended
    expect(clientKey(new Headers({ "x-real-ip": "1.2.3.4" }), { TRUSTED_PROXY: "1" })).toBe("ip:1.2.3.4");
  });

  it("token bucket limits bursts and refills", () => {
    const b = new TokenBucket(3, 1);
    const t = 1_000_000;
    expect([1, 2, 3, 4].map(() => b.take("k", t).ok)).toEqual([true, true, true, false]);
    expect(b.take("k", t + 1000).ok).toBe(true);
  });

  it("evicts the least recently used key instead of clearing everyone", () => {
    const b = new TokenBucket(1, 0.001, 3);
    const t = 1_000_000;
    b.take("a", t); // a exhausted
    b.take("b", t);
    b.take("c", t);
    b.take("a", t); // touch a (still limited) → most recent
    b.take("d", t); // evicts b, not a
    expect(b.size).toBe(3);
    expect(b.take("a", t).ok).toBe(false); // a kept its state
    expect(b.take("b", t).ok).toBe(true); // b was evicted → fresh
  });
});

describe("GET /api/figures/[id]/metadata", () => {
  const call = (id: string) => metadata.GET(new NextRequest(`http://localhost/api/figures/${id}/metadata`), { params: Promise.resolve({ id }) });

  it("400 on invalid ids", async () => {
    for (const id of ["abc", "demo-1-9", "../../etc", "1'or'1"]) expect((await call(id)).status).toBe(400);
  });

  it("returns Metaplex JSON for a demo figure with traits", async () => {
    const res = await call("demo-1-3");
    expect(res.status).toBe(200);
    const json = await res.json();
    expect(json.symbol).toBe("RNRK");
    expect(json.image).toMatch(/\/api\/figures\/demo-1-3\/image$/);
    const traits = Object.fromEntries((json.attributes as { trait_type: string; value: unknown }[]).map((a) => [a.trait_type, a.value]));
    expect(traits.Tier).toBe("3x3");
    expect(traits.Rarity).toBe("Epic");
    expect(traits.Skin).toBeDefined();
  });
});

describe("schema", () => {
  it("accepts a well-formed legendary purchase", () => {
    expect(purchaseSolBodySchema.safeParse({ ...LEGENDARY, slot: 4 }).success).toBe(true);
  });
});

describe("checkTransfer", () => {
  const treasury = Keypair.generate().publicKey.toBase58();
  const tx = (lamports: number, opts: { err?: boolean; signer?: boolean; source?: string; blockTime?: number | null } = {}) =>
    ({
      blockTime: opts.blockTime === undefined ? 1_000_000 : opts.blockTime,
      meta: { err: opts.err ? { InstructionError: [0, "x"] } : null, innerInstructions: [] },
      transaction: {
        message: {
          accountKeys: [{ pubkey: new PublicKey(BUYER), signer: opts.signer ?? true, writable: true }],
          instructions: [
            {
              programId: new PublicKey("11111111111111111111111111111111"),
              program: "system",
              parsed: { type: "transfer", info: { source: opts.source ?? BUYER, destination: treasury, lamports } },
            },
          ],
        },
      },
    }) as unknown as ParsedTransactionWithMeta;
  const exp = { buyer: BUYER, treasury, minLamports: 100_000_000 };
  const now = 1_000_060;

  it("accepts an exact payment", () => expect(checkTransfer(tx(100_000_000), exp, now)).toEqual({ ok: true, lamports: 100_000_000 }));
  it("rejects underpayment", () => expect(checkTransfer(tx(99_999_999), exp, now).ok).toBe(false));
  it("rejects failed tx", () => expect(checkTransfer(tx(100_000_000, { err: true }), exp, now).ok).toBe(false));
  it("rejects missing tx", () => expect(checkTransfer(null, exp, now).ok).toBe(false));
  it("rejects a tx without block time (M3)", () => expect(checkTransfer(tx(100_000_000, { blockTime: null }), exp, now).ok).toBe(false));
  it("rejects non-signer buyer", () => expect(checkTransfer(tx(100_000_000, { signer: false }), exp, now).ok).toBe(false));
  it("rejects transfers from someone else", () => expect(checkTransfer(tx(100_000_000, { source: treasury }), exp, now).ok).toBe(false));
  it("rejects stale payments", () => expect(checkTransfer(tx(100_000_000), exp, now + 3600).ok).toBe(false));
});
