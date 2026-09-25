# Current State

Updated: 2026-09-26. Plan and contracts: `docs/PLAN_V2.md` (platform) and `docs/PLAN_V3.md` (arcade adventure, music).

## v3 summary (arcade adventure)

| Area | Status |
|---|---|
| Adventure | Each world: Battle ×4, Rune Hand ×3 (Balatro-style), Ice Dash ×2, Beat Crawl ×2, Boss (battle → rune hand). 7 battle micro-challenges. Flat middle difficulty; Rune Hand balanced by bot simulation (~68 % greedy win) |
| Music | Procedural chiptune: 9 tracks × 3 intensity layers, stingers, beat clock (Crawl syncs to it). Toggle in ME |
| Pattern (Daily) | Examples act out their rule (rotate/flip/slide/invert) |
| Tuning | `Resources/RonrikuTuning.asset` — Inspector knobs, linked from the RONRIKU object |
| Server | `complete_arcade_level` (layout, unlock, physical time floors, proof stored); arcade proofs not yet replay-verified |
| Tests | EditMode 50, PlayMode 6 (+Capture 2, Online 1), backend 38 |

## v2 summary

| Area | Status |
|---|---|
| Unity client (Android + WebGL) | v2 shipped: Pixel Ambient UI, 5 tabs, adventure, bosses, shop, profile, online sync |
| Backend (local Supabase) | Schema, RLS, RPCs, publisher, wallet-link; hardened after independent review |
| TypeScript core | Bit-for-bit port of the C# domain, fixture-verified |
| Website (Next.js) | 12 pages + 3 API routes, game embed, market, ranks, friends, devnet SOL flow |
| Devnet NFTs | Single-transaction purchase (buyer pays price + mint rent + fees; server mint authority only signs, needs no SOL). Unit-tested; devnet dry run passes in simulation (faucet rate-limited, no live mint yet) |
| Self-hosting | `deploy/`: Next standalone image + Caddy single origin in front of the Supabase CLI stack; `deploy/up.sh` for a VPS |

## Unity client

- **Design**: dp layout (432×960 reference), OFL pixel fonts (Silkscreen, Pixelify Sans), dithered ambient backgrounds per palette, glowing gradient buttons, pixel icons, pixelated 3D voxel renderer (RenderTexture, point-filtered), synthesized chiptune SFX, haptics, gravity-sensor parallax, custom boot sequence (Unity splash disabled).
- **Tabs**: PLAY (5 worlds × 12 arcade levels — see PLAN_V3, walking avatar, guardian monsters, stars, world bosses, floating shard pickups capped 10/day), DAILY (Pattern → Shadow → Link), BOSSES (weekly raids, shard entry, first win pays), SHOP (daily shelf of 6 voxel figures), ME (avatar, stats, collection/equip, social when online, settings).
- **Difficulty**: Daily trials use the Standard band; arcade levels use tier 0 for W1 L1–6 and tier 1 elsewhere (no per-world ramp).
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
- API: figure metadata + PNG, SOL purchase: `POST /api/purchase/sol/prepare` (checks, builds one partially-signed tx: payment → `NEXT_PUBLIC_PAYMENT_RECIPIENT` + authority memo + Core create with payer/owner = buyer) → wallet signs/sends → `POST /api/purchase/sol` (verify tx, claim signature once, record ownership / enter boss).
- Tests 113, lint clean. Screenshots: `docs/evidence/web/`.

## Verified on device (Pixel 6a)

- v1.x flows (Daily, trials by tap/swipe/drag, persistence, release build 16 MB, cold start ~0.2–0.3 s).
- **v2/v3 not yet hands-on on device**: the v3 release APK is installed (2026-09-26), but the phone was locked in bedtime mode; next device pass pending.

## Open items

1. Device pass of v3 on Pixel 6a (adb reverse tcp:54321 for online): feel, music on the speaker, Crawl beat timing, frame rate on boards.
1b. Port the arcade rules to `packages/core` and replay-verify arcade proofs on the server (today: layout, unlock, time floors, stored proof).
1c. WebGL: verify music layers start (`PlayScheduled`/`timeSamples`) after the first tap.
2. Live devnet mint: fund the dry-run buyer printed by `pnpm --filter web devnet-dry-run` at faucet.solana.com and rerun it (the mint authority itself needs no SOL).
3. Seeker Mobile Wallet Adapter inside the Unity app (web already supports MWA).
4. Offline progress made before first connecting is not migrated to the server.
5. Production: HTTPS backend (disable `insecureHttpOption`; `deploy/` gives HTTPS via Caddy), non-default Supabase JWT secret (hosted or self-hosted compose instead of the CLI stack), real domain in wallet message (`WALLET_LINK_DOMAIN`). Publisher cron: installed by `deploy/up.sh`.

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
