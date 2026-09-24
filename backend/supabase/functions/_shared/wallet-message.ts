/**
 * The exact text a wallet signs to link itself to a RONRIKU profile (Sign-In-With-Solana style).
 * Shared by the edge function and clients (Unity MWA / web wallet adapter): sign the UTF-8 bytes of
 * the `message` string returned by `{action:"nonce"}` verbatim; never rebuild it client-side.
 *
 *   {domain} wants you to link your Solana wallet to RONRIKU.
 *   <blank line>
 *   User: {profile uuid}
 *   Nonce: {32 hex chars}
 *   Expires: {ISO-8601 UTC}
 *
 * `domain` is the WALLET_LINK_DOMAIN env var of the edge function (default "ronriku.local"); wallets
 * show it so users can spot phishing origins.
 */
export const DEFAULT_WALLET_LINK_DOMAIN = "ronriku.local";

export function walletLinkMessage(domain: string, userId: string, nonce: string, expiresAt: string): string {
  return `${domain} wants you to link your Solana wallet to RONRIKU.\n\nUser: ${userId}\nNonce: ${nonce}\nExpires: ${expiresAt}`;
}
