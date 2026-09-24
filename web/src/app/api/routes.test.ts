import { beforeEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";
import { Keypair, PublicKey, type ParsedTransactionWithMeta } from "@solana/web3.js";
import { purchaseSolBodySchema } from "@/lib/validation";
import { checkTransfer } from "@/lib/server/solana";

// Keep the route in "not configured" mode regardless of the developer's .env.local.
vi.stubEnv("SUPABASE_SERVICE_ROLE_KEY", "");
vi.stubEnv("TREASURY_SECRET_KEY", "");

const { POST, GET } = await import("./purchase/sol/route");
const metadata = await import("./figures/[id]/metadata/route");

const SIG = "5".repeat(87);
const BUYER = Keypair.generate().publicKey.toBase58();
let ip = 0;

function post(body: unknown, headers: Record<string, string> = {}) {
  return POST(
    new NextRequest("http://localhost/api/purchase/sol", {
      method: "POST",
      headers: { "content-type": "application/json", "x-forwarded-for": `10.0.0.${ip}`, ...headers },
      body: typeof body === "string" ? body : JSON.stringify(body),
    }),
  );
}

beforeEach(() => {
  ip++;
});

describe("POST /api/purchase/sol input validation", () => {
  it("rejects non-JSON content type", async () => {
    expect((await post("{}", { "content-type": "text/plain" })).status).toBe(415);
  });

  it("rejects malformed JSON", async () => {
    expect((await post("{nope")).status).toBe(400);
  });

  it("rejects oversized bodies", async () => {
    expect((await post({ item: "figure-legendary", pad: "x".repeat(5000) })).status).toBe(413);
  });

  it.each([
    ["missing everything", {}],
    ["unknown item", { item: "figure-free", signature: SIG, buyer: BUYER }],
    ["bad signature charset", { item: "mint-fee", signature: "0OIl".repeat(22), buyer: BUYER, figureId: crypto.randomUUID() }],
    ["short signature", { item: "mint-fee", signature: "abc", buyer: BUYER, figureId: crypto.randomUUID() }],
    ["bad buyer", { item: "mint-fee", signature: SIG, buyer: "not-a-key", figureId: crypto.randomUUID() }],
    ["mint-fee without figureId", { item: "mint-fee", signature: SIG, buyer: BUYER }],
    ["mint-fee with non-uuid figureId", { item: "mint-fee", signature: SIG, buyer: BUYER, figureId: "1; drop table" }],
    ["legendary without slot", { item: "figure-legendary", signature: SIG, buyer: BUYER, day: 24 }],
    ["legendary with slot out of range", { item: "figure-legendary", signature: SIG, buyer: BUYER, day: 24, slot: 6 }],
    ["boss-entry without bossId", { item: "boss-entry", signature: SIG, buyer: BUYER }],
    ["unexpected field", { item: "figure-legendary", signature: SIG, buyer: BUYER, day: 24, slot: 5, lamports: 1 }],
  ])("400 on %s", async (_name, body) => {
    const res = await post(body);
    expect(res.status).toBe(400);
    expect((await res.json()).error).toBeTruthy();
  });

  it("401 without a bearer token for a valid body", async () => {
    const res = await post({ item: "figure-legendary", signature: SIG, buyer: BUYER, day: 24, slot: 5 });
    expect(res.status).toBe(401);
  });

  it("503 when the server has no service role / treasury configured", async () => {
    const res = await post({ item: "figure-legendary", signature: SIG, buyer: BUYER, day: 24, slot: 5 }, { authorization: `Bearer ${"x".repeat(40)}` });
    expect(res.status).toBe(503);
  });

  it("rate-limits bursts from one IP", async () => {
    const statuses: number[] = [];
    for (let i = 0; i < 7; i++) statuses.push((await post("{nope")).status);
    expect(statuses.slice(0, 5).every((s) => s === 400)).toBe(true);
    expect(statuses.at(-1)).toBe(429);
  });

  it("GET reports purchases disabled without config", async () => {
    expect(await (await GET()).json()).toMatchObject({ enabled: false, treasury: null });
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
    expect(purchaseSolBodySchema.safeParse({ item: "figure-legendary", signature: SIG, buyer: BUYER, day: 24, slot: 4 }).success).toBe(true);
  });
});

describe("checkTransfer", () => {
  const treasury = Keypair.generate().publicKey.toBase58();
  const tx = (lamports: number, opts: { err?: boolean; signer?: boolean; source?: string; blockTime?: number } = {}) =>
    ({
      blockTime: opts.blockTime ?? 1_000_000,
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
  it("rejects non-signer buyer", () => expect(checkTransfer(tx(100_000_000, { signer: false }), exp, now).ok).toBe(false));
  it("rejects transfers from someone else", () => expect(checkTransfer(tx(100_000_000, { source: treasury }), exp, now).ok).toBe(false));
  it("rejects stale payments", () => expect(checkTransfer(tx(100_000_000), exp, now + 3600).ok).toBe(false));
});
