import bs58 from "bs58";
import nacl from "tweetnacl";
import { describe, expect, it } from "vitest";
import { admin, env, newPlayer, rpc, type Player } from "./helpers.ts";

async function call(p: Player, body: unknown): Promise<{ status: number; body: any }> {
  const { data } = await p.db.auth.getSession();
  const res = await fetch(`${env.url}/functions/v1/wallet-link`, {
    method: "POST",
    headers: { Authorization: `Bearer ${data.session!.access_token}`, apikey: env.anonKey, "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  return { status: res.status, body: await res.json() };
}

describe("wallet-link edge function", () => {
  it("links a wallet with a signed nonce; rejects bad signatures, reuse and duplicates", async () => {
    const p = await newPlayer();
    const wallet = nacl.sign.keyPair();
    const address = bs58.encode(wallet.publicKey);

    const issued = await call(p, { action: "nonce" });
    expect(issued.status).toBe(200);
    expect(issued.body.message).toContain(p.id);
    expect(issued.body.message.startsWith("ronriku.local wants you to link your Solana wallet to RONRIKU.")).toBe(true);

    const forged = nacl.sign.detached(new TextEncoder().encode(issued.body.message), nacl.sign.keyPair().secretKey);
    expect((await call(p, { action: "verify", address, signature: bs58.encode(forged) })).status).toBe(422);

    const sig = nacl.sign.detached(new TextEncoder().encode(issued.body.message), wallet.secretKey);
    const ok = await call(p, { action: "verify", address, signature: bs58.encode(sig) });
    expect(ok).toEqual({ status: 200, body: { wallet_address: address } });
    expect((await rpc(p.db, "ensure_profile")).wallet_address).toBe(address);
    expect((await call(p, { action: "verify", address, signature: bs58.encode(sig) })).status).toBe(404); // single use

    // The same wallet cannot be linked to a second profile.
    const q = await newPlayer();
    const n2 = await call(q, { action: "nonce" });
    const sig2 = nacl.sign.detached(new TextEncoder().encode(n2.body.message), wallet.secretKey);
    expect((await call(q, { action: "verify", address, signature: bs58.encode(sig2) })).status).toBe(409);

    const { data } = await admin.from("wallet_nonces").select("user_id").eq("user_id", p.id);
    expect(data).toEqual([]);
  });
});
