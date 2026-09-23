# Security

Phase 1 contains no credentials, wallet keys, backend service roles, remote content, or transactions. Local analytics logs event names and minimal non-personal properties only. Puzzle seeds are content identifiers, not secrets.

Future competitive clients are untrusted. The backend will own attempt issuance, expiry, answer validation, scoring, leaderboard insertion, and reward eligibility. Wallet linking will require a nonce and signed-message verification. Challenge links and creator schemas will be bounded, versioned, and validated at the server boundary.

Never commit private keys, seed phrases, keystores, service-role keys, private RPC URLs, raw signed transactions, or production tokens.

