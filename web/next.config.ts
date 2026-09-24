import path from "node:path";
import type { NextConfig } from "next";

// Page CSP (nonce-based, /play-only eval) is set per request in src/middleware.ts (src/lib/csp.ts).
// API responses and static assets get a locked-down policy here.
const apiCsp = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

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
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          { key: "X-Frame-Options", value: "DENY" },
          { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=()" },
        ],
      },
      { source: "/api/:path*", headers: [{ key: "Content-Security-Policy", value: apiCsp }] },
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
