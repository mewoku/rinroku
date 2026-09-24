/**
 * Content-Security-Policy builder (edge-safe, no Node APIs).
 *  - scripts: nonce + 'strict-dynamic', no 'unsafe-inline'. Only /play (Unity WebGL) gets
 *    'unsafe-eval' / 'wasm-unsafe-eval' and blob: workers. Dev adds 'unsafe-eval' for React refresh.
 *  - styles keep 'unsafe-inline': React style attributes and next/font inject inline styles.
 *  - frame-src: Solflare's web wallet runs in an iframe.
 */
export function buildCsp(o: { nonce: string; unity: boolean; dev: boolean; supabaseUrl: string; rpcUrl: string }): string {
  const ws = (u: string) => u.replace(/^http/, "ws");
  const script = ["'self'", `'nonce-${o.nonce}'`, "'strict-dynamic'"];
  if (o.unity) script.push("'unsafe-eval'", "'wasm-unsafe-eval'", "blob:");
  else if (o.dev) script.push("'unsafe-eval'");
  const connect = [
    "'self'",
    o.supabaseUrl,
    ws(o.supabaseUrl),
    o.rpcUrl,
    ws(o.rpcUrl),
    "https://*.solana.com",
    "wss://*.solana.com",
    "https://*.solflare.com",
    "wss://*.solflare.com",
  ];
  if (o.unity) connect.push("blob:", "data:");
  if (o.dev) connect.push("ws://localhost:*");
  return [
    "default-src 'self'",
    `script-src ${script.join(" ")}`,
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob:",
    "font-src 'self' data:",
    `connect-src ${connect.join(" ")}`,
    `worker-src 'self'${o.unity ? " blob:" : ""}`,
    "frame-src https://connect.solflare.com https://*.solflare.com",
    "frame-ancestors 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "object-src 'none'",
  ].join("; ");
}
