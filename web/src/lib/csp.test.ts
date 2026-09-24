import { describe, expect, it } from "vitest";
import { NextRequest } from "next/server";
import { buildCsp } from "./csp";
import { middleware } from "../middleware";

const base = { nonce: "abc", dev: false, supabaseUrl: "http://127.0.0.1:54321", rpcUrl: "https://api.devnet.solana.com" };
const directive = (csp: string, name: string) => csp.split("; ").find((d) => d.startsWith(`${name} `)) ?? "";

describe("CSP (M6)", () => {
  it("pages: nonce + strict-dynamic, no inline or eval scripts", () => {
    const s = directive(buildCsp({ ...base, unity: false }), "script-src");
    expect(s).toContain("'nonce-abc'");
    expect(s).toContain("'strict-dynamic'");
    expect(s).not.toContain("'unsafe-inline'");
    expect(s).not.toContain("eval");
    expect(s).not.toContain("blob:");
  });

  it("/play only: Unity needs eval, wasm and blob: workers", () => {
    const csp = buildCsp({ ...base, unity: true });
    expect(directive(csp, "script-src")).toContain("'wasm-unsafe-eval'");
    expect(directive(csp, "script-src")).toContain("blob:");
    expect(directive(csp, "worker-src")).toContain("blob:");
  });

  it("allows the Solflare wallet iframe", () => {
    expect(directive(buildCsp({ ...base, unity: false }), "frame-src")).toContain("https://connect.solflare.com");
  });

  it("middleware scopes eval to /play and passes a fresh nonce", () => {
    const home = middleware(new NextRequest("http://localhost/market")).headers.get("content-security-policy")!;
    const play = middleware(new NextRequest("http://localhost/play")).headers.get("content-security-policy")!;
    expect(directive(home, "script-src")).not.toContain("wasm-unsafe-eval");
    expect(directive(play, "script-src")).toContain("wasm-unsafe-eval");
    const n1 = /'nonce-([^']+)'/.exec(home)![1];
    const n2 = /'nonce-([^']+)'/.exec(play)![1];
    expect(n1).not.toBe(n2);
  });
});
