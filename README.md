# ODLET

**Odlet** ([odlet.xyz](https://odlet.xyz)) is a pixel-art reasoning game for Android / Solana Seeker and the browser. Walk an adventure map of voxel monsters you defeat with puzzles, play the three-trial DAILY (Pattern → Shadow → Link), raid weekly bosses, and collect MagicaVoxel-style voxel figures — bought with shards or devnet SOL and minted as Metaplex Core NFTs.

- **Client**: Unity 6000.6 (Android + WebGL), UI Toolkit, pixelated 3D voxel renderer, offline-first.
- **Backend**: Supabase (Postgres + RLS + RPCs); the server replays submitted answers instead of trusting "solved" flags.
- **Core**: `packages/core`, a bit-for-bit TypeScript port of the C# domain (puzzles, figures, levels, shop, bosses).
- **Website**: `web/`, Next.js site with the WebGL game, marketplace, leaderboards, friends and devnet SOL purchases.

Status, verification and open items: [docs/STATE.md](docs/STATE.md). Architecture and contracts: [docs/PLAN_V2.md](docs/PLAN_V2.md). Risks: [docs/RISKS.md](docs/RISKS.md).

## Fresh clone

Requirements: Unity **6000.6.0f1** with Android + WebGL modules, Node 24, pnpm 9, Docker Desktop.

```bash
pnpm install
pnpm build:core                      # web consumes @ronriku/core from dist/

# Backend (local)
cd backend
npx supabase start
npx supabase db reset
pnpm publish:content                 # answer keys, shop shelf, bosses — required after every reset
cd ..

# Website
cp web/.env.example web/.env.local   # fill from `npx supabase status -o env` (in backend/)
pnpm --filter web create-mint-authority   # server mint authority (needs no SOL) + payment recipient into web/.env.local
```

Unity (open `client/`):

1. Menu **RONRIKU → Setup Fonts** (once; creates static pixel-font atlases).
2. **RONRIKU → Build WebGL (Website)** → `web/public/unity/` (git-ignored).
3. **RONRIKU → Build Android (Release)** → `Builds/Android/ODLET.apk`. Set `RONRIKU_KEYSTORE`, `RONRIKU_KEYSTORE_PASS`, `RONRIKU_KEY_ALIAS`, `RONRIKU_KEY_PASS` for a store-signed build; otherwise it is debug-signed.

```bash
pnpm --filter web build && pnpm --filter web start    # http://localhost:3000, game at /play
```

On a phone, `adb reverse tcp:54321 tcp:54321` lets the APK reach the local backend.

Self-hosting (Docker: Next.js + Caddy + Supabase on one origin, local or a VPS): [deploy/README.md](deploy/README.md).

SOL payments go to `NEXT_PUBLIC_PAYMENT_RECIPIENT`. Each purchase is one transaction the server prepares and partially signs (payment + memo + Metaplex Core mint); the buyer's wallet signs, pays all fees and rent, and sends it. `pnpm --filter web devnet-dry-run` exercises this on devnet.

## Tests

```bash
pnpm test:core                                 # 352 fixture tests vs C# exports
(cd backend && pnpm test && npx supabase test db)   # integration + pgTAP
pnpm --filter web test                         # web unit tests
```

Unity (editor open, via the Unity CLI): `unity command run_tests --mode EditMode`; PlayMode categories `Uncategorized`, `Online` (needs local backend) and `Capture` (writes `docs/evidence/`).

## License

Code: MIT (see [LICENSE](LICENSE)). Fonts: SIL OFL 1.1. Third-party notices: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
