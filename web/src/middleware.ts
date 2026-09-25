import { NextResponse, type NextRequest } from "next/server";
import { buildCsp } from "./lib/csp";

/**
 * Per-request nonce CSP for pages. Next.js reads the nonce from the request's
 * Content-Security-Policy header and stamps it on its own scripts; 'strict-dynamic' lets those
 * scripts load chunks (and, on /play only, the Unity loader + its blob: workers).
 */
export function middleware(req: NextRequest) {
  const nonce = btoa(crypto.randomUUID());
  const csp = buildCsp({
    nonce,
    unity: req.nextUrl.pathname === "/play" || req.nextUrl.pathname.startsWith("/play/"),
    dev: process.env.NODE_ENV !== "production",
    supabaseUrl: process.env.NEXT_PUBLIC_SUPABASE_URL ?? "http://127.0.0.1:54321", // "same-origin" → 'self' only
    rpcUrl: process.env.NEXT_PUBLIC_SOLANA_RPC_URL || "https://api.devnet.solana.com",
  });
  const requestHeaders = new Headers(req.headers);
  requestHeaders.set("x-nonce", nonce);
  requestHeaders.set("Content-Security-Policy", csp);
  const res = NextResponse.next({ request: { headers: requestHeaders } });
  res.headers.set("Content-Security-Policy", csp);
  return res;
}

export const config = {
  matcher: [
    {
      source: "/((?!api/|_next/static|_next/image|favicon.ico|icon.png|unity/).*)",
      missing: [
        { type: "header", key: "next-router-prefetch" },
        { type: "header", key: "purpose", value: "prefetch" },
      ],
    },
  ],
};
