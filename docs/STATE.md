# Current State

Updated: 2026-09-24. Plan and contracts: `docs/PLAN_V2.md`.

## v2 summary

| Area | Status |
|---|---|
| Unity client (Android + WebGL) | v2 shipped: Pixel Ambient UI, 5 tabs, adventure, bosses, shop, profile, online sync |
| Backend (local Supabase) | Schema, RLS, RPCs, publisher, wallet-link; hardened after independent review |
| TypeScript core | Bit-for-bit port of the C# domain, fixture-verified |
| Website (Next.js) | 12 pages + 3 API routes, game embed, market, ranks, friends, devnet SOL flow |
| Devnet NFTs | Mint route built and unit-tested; treasury unfunded (faucet failed) so no live mint yet |

## Unity client

- **Design**: dp layout (432×960 reference), OFL pixel fonts (Silkscreen, Pixelify Sans), dithered ambient backgrounds per palette, glowing gradient buttons, pixel icons, pixelated 3D voxel renderer (RenderTexture, point-filtered), synthesized chiptune SFX, haptics, gravity-sensor parallax, custom boot sequence (Unity splash disabled).
- **Tabs**: PLAY (5 worlds × 12 levels, walking avatar, guardian monsters with HP, stars, world bosses as 3 stages, floating shard pickups capped 10/day), DAILY (Pattern → Shadow → Link), BOSSES (weekly raids, shard entry, first win pays), SHOP (daily shelf of 6 voxel figures), ME (avatar, stats, collection/equip, social when online, settings).
- **Difficulty**: every adventure level and Daily trial uses the Standard band.
- **Online**: `Infrastructure/Online` — anonymous Supabase session over UnityWebRequest, offline-first; when connected the server is authoritative (shards, rating, streak, figures, levels) and results are submitted as answers + move/reset counts for server replay.
- **Tests**: EditMode 35, PlayMode 3 (offline), Online 1 (end-to-end vs local Supabase), Capture (screenshots at 3 aspect ratios → `docs/evidence/`).
- **Builds**: `RONRIKU/Build Android (Release)` → `Builds/Android/RONRIKU.apk` (~16 MB); `(Development)` → `RONRIKU-dev.apk`; `RONRIKU/Build WebGL (Website)` → `web/public/unity/`.

## Backend (`backend/`)

- Every table has RLS; clients have no write grants; all writes via `security definer` RPCs with pinned search_path; default privileges closed; pgTAP allowlist check.
- `scripts/publish.ts` writes private answer keys (Dailies −2…+30 days, all 60 levels, bosses), the daily shop shelf, the boss roster and the starter figure pool. **Run after every `db reset`.**
- Anti-abuse: boss reward once per boss, server-measured boss time, Daily reward needs ≥1 solve, marketplace progress gate + seller daily cap, race-safe profile locking.
- Tests: core 352, backend integration 36, pgTAP 8.

## Website (`web/`)

- Pages: `/`, `/play`, `/daily`, `/bosses`, `/market`, `/inventory`, `/u/[handle]`, `/friends`, `/leaderboard`, `/login`.
- API: figure metadata + PNG, SOL purchase (verify transfer → claim signature once → mint Metaplex Core / enter boss).
- Tests 41, lint clean. Screenshots: `docs/evidence/web/`.

## Verified on device (Pixel 6a)

- v1.x flows (Daily, trials by tap/swipe/drag, persistence, release build 16 MB, cold start ~0.2–0.3 s).
- **v2 not yet hands-on on device**: the phone was locked during the v2 build; next device pass pending.

## Open items

1. Device pass of v2 on Pixel 6a (adb reverse tcp:54321 for online).
2. Fund devnet treasury (`web/.env.local` → `NEXT_PUBLIC_TREASURY_PUBKEY`) at faucet.solana.com and run a live mint.
3. Seeker Mobile Wallet Adapter inside the Unity app (web already supports MWA).
4. Offline progress made before first connecting is not migrated to the server.
5. Production: HTTPS backend (disable `insecureHttpOption`), hosted Supabase, publisher cron, real domain in wallet message.

## Commands

```bash
pnpm install && pnpm build:core
cd backend && npx supabase start && npx supabase db reset && pnpm publish:content
pnpm test:core; (cd backend && pnpm test && npx supabase test db); pnpm --filter web test
unity command run_tests --mode EditMode --timeout 900
unity command run_tests --mode PlayMode --filter Uncategorized --filter_type category --async_tests true
unity command run_tests --mode PlayMode --filter Online --filter_type category --async_tests true
pnpm --filter web build && pnpm --filter web start   # site on :3000, game at /play
```
