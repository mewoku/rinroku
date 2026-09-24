# Third-party notices

RONRIKU's own code is MIT licensed (see `LICENSE`). It includes or depends on the following.

## Fonts (SIL Open Font License 1.1)

- **Silkscreen** by Jason Kottke — `client/Assets/Ronriku/Fonts/OFL-Silkscreen.txt`
- **Pixelify Sans** by Stefie Justprince — `client/Assets/Ronriku/Fonts/OFL-PixelifySans.txt`

The website loads the same families from Google Fonts via `next/font`.

## Unity

- Unity engine runtime (Unity Software Inc., Unity Terms of Service).
- `com.unity.nuget.newtonsoft-json` — Newtonsoft.Json, MIT.
- `com.unity.pipeline` — editor automation used in development only; its runtime is compiled only into development builds.

## JavaScript (selected; full list via `pnpm licenses list --prod`)

- `@metaplex-foundation/mpl-core`, `@metaplex-foundation/umi*` — Apache-2.0 (NOTICE files ship in the packages).
- `react-unity-webgl` — Apache-2.0.
- `@solana/wallet-adapter-*`, `@solana-mobile/wallet-standard-mobile`, Solflare adapter — Apache-2.0 / MIT.
- `@solana/web3.js` — MIT. It depends on **`rpc-websockets` (LGPL-3.0-only)**, bundled unmodified into the browser build; its source is available at https://github.com/elpheria/rpc-websockets. Replacing web3.js v1 with `@solana/kit` would remove this dependency.
- `@supabase/supabase-js` — MIT.
- `three` — MIT.
- `next`, `react` — MIT.
- `sharp` / libvips (`@img/sharp-*`) — Apache-2.0 / LGPL-3.0, used server-side only.
- `caniuse-lite` — CC-BY-4.0, build-time only.
- `pngjs`, `bs58`, `tweetnacl`, `zod` — MIT / Unlicense.

## Backend

- Supabase CLI and local stack (Apache-2.0 / PostgreSQL License).
