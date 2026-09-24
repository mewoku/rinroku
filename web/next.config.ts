import path from "node:path";
import type { NextConfig } from "next";

const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL ?? "http://127.0.0.1:54321";
const rpcUrl = process.env.NEXT_PUBLIC_SOLANA_RPC_URL ?? "https://api.devnet.solana.com";
const wsUrl = rpcUrl.replace(/^http/, "ws");
const isDev = process.env.NODE_ENV !== "production";

// CSP: Unity WebGL needs 'unsafe-eval' + 'wasm-unsafe-eval' and blob: workers; Next needs inline
// bootstrap scripts. Everything else is same-origin plus Supabase + Solana RPC.
const csp = [
  "default-src 'self'",
  `script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval' 'unsafe-eval' blob:`,
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: blob:",
  "font-src 'self' data:",
  `connect-src 'self' blob: data: ${supabaseUrl} ${supabaseUrl.replace(/^http/, "ws")} ${rpcUrl} ${wsUrl} https://*.solana.com wss://*.solana.com${isDev ? " ws://localhost:*" : ""}`,
  "worker-src 'self' blob:",
  "frame-ancestors 'none'",
  "base-uri 'self'",
  "form-action 'self'",
  "object-src 'none'",
].join("; ");

const nextConfig: NextConfig = {
  // Monorepo root (pnpm workspace) — avoids Next guessing from stray lockfiles in the home dir.
  outputFileTracingRoot: path.join(__dirname, ".."),
  // Keep the Solana/Umi stack as plain Node requires in route handlers.
  serverExternalPackages: ["@metaplex-foundation/umi-bundle-defaults", "@metaplex-foundation/mpl-core", "@metaplex-foundation/umi"],
  reactStrictMode: true,
  poweredByHeader: false,
  transpilePackages: ["@ronriku/core"],
  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          { key: "Content-Security-Policy", value: csp },
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          { key: "X-Frame-Options", value: "DENY" },
          { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=()" },
        ],
      },
      // Unity pre-compressed builds (Compression Format = Brotli/Gzip without decompression fallback).
      {
        source: "/unity/Build/:file*.wasm.br",
        headers: [{ key: "Content-Encoding", value: "br" }, { key: "Content-Type", value: "application/wasm" }],
      },
      {
        source: "/unity/Build/:file*.js.br",
        headers: [{ key: "Content-Encoding", value: "br" }, { key: "Content-Type", value: "application/javascript" }],
      },
      {
        source: "/unity/Build/:file*.data.br",
        headers: [{ key: "Content-Encoding", value: "br" }, { key: "Content-Type", value: "application/octet-stream" }],
      },
      {
        source: "/unity/Build/:file*.wasm.gz",
        headers: [{ key: "Content-Encoding", value: "gzip" }, { key: "Content-Type", value: "application/wasm" }],
      },
      {
        source: "/unity/Build/:file*.js.gz",
        headers: [{ key: "Content-Encoding", value: "gzip" }, { key: "Content-Type", value: "application/javascript" }],
      },
      {
        source: "/unity/Build/:file*.data.gz",
        headers: [{ key: "Content-Encoding", value: "gzip" }, { key: "Content-Type", value: "application/octet-stream" }],
      },
    ];
  },
};

export default nextConfig;
