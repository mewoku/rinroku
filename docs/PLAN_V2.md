# RONRIKU v2 — Master Plan and Contracts

Updated: 2026-09-24. Owner feedback that drives v2 (condensed):

- Almost no real UI; UI unusable outside gameplay; oversized elements; single palette everywhere; Unity splash.
- Puzzles not intuitive; difficulty uneven (some trivial, some hard) — everything should sit in a **middle band**.
- Wanted: gradients + ambient light everywhere, darker background, gradient glow from buttons, **pixel UI almost everywhere**, **pixelated 3D**, more colour, more juice (haptics, tactility, gyroscope parallax with graceful skip), antistress "sticky" play of 5–10 minute sessions.
- **Adventure mode**: walkable level map, character, monsters you beat through puzzles, **boss levels** with paid entry (points or SOL). Replay any finished level. Levels stored compactly (seed/text), not as full maps.
- **Figures**: MagicaVoxel-looking little voxel people in 3×3 / 4×4 / 5×5 tiers, generated procedurally (plus editor), bought with points or SOL, owned as **Solana devnet NFTs (Metaplex Core)**, usable as avatars, tradeable, kept in an inventory.
- **Website**: the same game in the browser (Unity WebGL) inside a Next.js site with marketplace, inventory, leaderboard, profiles, friends.
- **Backend**: local self-hosted Supabase (Docker). Server, database, leaderboard, friends, security.

## 1. Repository layout

```
client/                 Unity 6000.6 project (Android + WebGL). C# domain is the source of truth for
                        puzzle and figure generation.
backend/supabase/       Supabase CLI project: config.toml, migrations/, seed.sql, functions/, tests/
packages/core/          TypeScript port of the deterministic domain (figures, level definitions,
                        puzzle validators) + golden fixtures exported from C# (fixtures/*.json).
web/                    Next.js 15 (App Router, TypeScript, Tailwind v4) site: landing, /play (WebGL),
                        /market, /inventory, /leaderboard, /u/[handle], /friends, /login.
docs/                   plans, state, architecture, risks, evidence
scripts/                build/test helpers
```

## 2. Design system v2 ("Pixel Ambient")

Shared by Unity and web. Tokens live in `client/.../RonrikuTheme.cs` and `web/src/styles/tokens.css`.

- **Base**: background `#07080B` → `#0E1016` (darker than v1), surfaces `#14161D`, `#1B1E27`, line `#2A2F3B`, text `#E7E8E5`, muted `#8A94A6`.
- **Brand**: teal `#11C5B3`, yellow `#E8DA37` (kept from v1).
- **World palettes** (each world/biome and each trial kind gets its own accent pair + ambient hue):
  - Lab (Spatial): teal `#11C5B3` / deep blue `#135B73`, ambient `#0B3B4A`
  - Pattern: magenta `#FF4FD8` / violet `#7B5CFF`, ambient `#2A1450`
  - Link (Logic): amber `#FFB347` / coral `#FF6B5A`, ambient `#4A1E12`
  - Forest: lime `#9BE35A` / moss `#2F8F4E`, ambient `#10301A`
  - Frost: ice `#8FE3FF` / cobalt `#3A6BFF`, ambient `#0C2250`
  - Boss: red `#FF3B5C` / gold `#FFC83D`, ambient `#3A0A14`
- **Ambient**: 2–3 large dithered radial gradient blobs (ordered Bayer 4×4 dithering, rendered low-res and point-filtered) drifting slowly behind every screen, tinted by the current palette. Respect reduced motion.
- **Glow**: primary buttons have a gradient fill (accent → accent2) and a soft dithered glow halo behind them; pressed state darkens and shifts down 2 px.
- **Pixel typography**: OFL pixel fonts — *Silkscreen* for headings/buttons, *Pixelify Sans* for body — in both Unity (TextCore font assets) and web (next/font). Integer sizes only.
- **Scale**: UI Toolkit `ConstantPhysicalSize`, reference DPI 160 → design in **dp**. Seeker/Pixel (1080×2400 @ ~400 dpi) ≈ 432×960 dp. 8 dp grid, 48 dp minimum touch target, body 14–16 dp, headings 24–40 dp. No element designed in raw 1080-px units.
- **Pixelated 3D**: voxel meshes rendered by an orthographic camera into a low-resolution RenderTexture (point filtering) and shown in UI; flat per-face shading (top light, left mid, right dark) like MagicaVoxel previews.
- **Juice**: pixel particle bursts, squash/stretch on taps, number pops, 60–180 ms motion, synthesized chiptune SFX (no audio assets needed), layered haptics (tick / thud / success / error / boss hit), gyroscope parallax on ambient + 3D views (disabled when no sensor or reduced motion).
- **No Unity splash**: `PlayerSettings.SplashScreen.show = false`; custom pixel boot sequence.

## 3. App structure (Unity and web share the information architecture)

Bottom navigation (pixel icons, 5 tabs): **PLAY** (adventure map) · **DAILY** · **BOSSES** · **SHOP** · **ME**.

- **PLAY** — world map. The player's avatar figure walks node to node on a pixel path. Nodes are levels guarded by monsters; every 12th node is a world boss. Finished nodes are replayable (stars, best time shown). Worlds unlock sequentially.
- **DAILY** — the existing 3-trial Daily (Pattern → Spatial → Link) with rating and streak.
- **BOSSES** — rotating boss events: multi-stage puzzle fights with HP bars. Entry costs shards or devnet SOL. Leaderboard per boss.
- **SHOP** — figure packs and individual figures for shards or devnet SOL; marketplace listings from other players.
- **ME** — profile (avatar figure, rating, stats), inventory (figures, equip as avatar, list for sale, mint to wallet), friends (add by handle, requests, friend leaderboard, compare), global/daily leaderboards, settings (sound, haptics, gyroscope, reduced motion, wallet).

## 4. Gameplay changes

- **Intuitive without text**: each mechanic's first adventure level is a guided micro-level: a ghost finger animates the gesture; the goal is shown visually (target glows / pulses). Text is limited to 1–3 word labels.
  - Pattern: examples animate their transformation (input morphs into output on loop). Test grid pulses; options wobble on hover/press.
  - Spatial: drag the board to rotate with snapping (plus buttons), live shadow preview, target tiles pulse.
  - Link: path head glows, next number pulses, invalid moves bump back with haptic.
- **Difficulty middle band**: target median solve 25–60 s per puzzle for new players. Daily uses Standard for all three. Adventure worlds move within Standard±: Pattern (single rules incl. invert/shift, 2–3 examples), Spatial (5 cubes, par 2–3), Link (5×5 with 5–8 numbers + walls). Remove trivial par-1 Spatial and 2-number 6×6 Link from player-facing content.
- **Monsters and bosses**: a level shows its guardian monster (voxel figure with monster traits). Progress deals damage (HP bar). Boss: 3 chained puzzles, a timer, and a bigger HP bar; losing costs the entry.
- **Antistress / engagement**: tactile idle toys (tap the avatar to hop, poke monsters, collect floating shards on the map), combo counter for consecutive perfect solves, stars (solve / at par / under time), chest rewards every 4 levels.

## 5. Figures (voxel people)

Source of truth: `client/Assets/Ronriku/Scripts/Domain/Figures/` (C#). TypeScript port in `packages/core/src/figures/` must reproduce `packages/core/fixtures/figures.json` exactly.

- Tiers: **S = 3, 4, 5**. Volume S (x) × S (y) × 2S (z, up).
- Voxel value 0 = empty, 1–15 = index into the figure's 15-colour palette.
- **Encoding** `RF1.{S}.{palette}.{voxels}`: palette = 15 × 6 hex chars (RRGGBB) concatenated; voxels = base64url (no padding) of nibble-packed values, x fastest, then y, then z, high nibble first.
- Generation from `(seed, tier)` with the documented `DeterministicRandom` (xorshift64 variant already used by puzzles; TypeScript uses BigInt). Traits: skin, outfit, accent, hair/hat, eyes, accessory. Rarity (Common / Rare / Epic / Legendary) derives from trait weights.
- Import/export `.vox` (MagicaVoxel) for authoring and QA.
- Rendering: pixelated 3D (section 2) in Unity; three.js + low-res canvas with `image-rendering: pixelated` on web; server renders a 256×256 PNG for NFT metadata.

## 6. Economy

- Currency **Shards** (off-chain, server-authoritative balance).
- Earn: level clear 20 (+10 per extra star), Daily completion 100 (+10 × streak, cap 100), boss win 300–1000.
- Spend: boss entry 150 shards (or 0.01 devnet SOL), figures Common 300 / Rare 800 / Epic 2000 shards, Legendary devnet SOL only (0.1).
- Figures bought with SOL are minted as Metaplex Core NFTs to the buyer's wallet. Shard-bought figures are off-chain until the owner claims them: the claim is the same buyer-paid transaction (mint fee to the recipient + Core create, rent and fees paid by the owner's wallet).
- Payments go to `NEXT_PUBLIC_PAYMENT_RECIPIENT` (a public address; its key is not on the server). The server mint authority (`MINT_AUTHORITY_SECRET_KEY`) co-signs each prepared transaction (memo binding user + item; Core create authority; update authority) and never pays, so it needs no SOL.
- Marketplace: off-chain listings for shards; NFT listings for devnet SOL use a Core **TransferDelegate** approved to the server mint authority; the server transfers after verifying payment (planned; the buyer must pay fees, as in purchases).

## 7. Backend contract (Supabase, local via `supabase start`)

Auth: Supabase **anonymous sign-in** first; optional email/OAuth link later; wallet link via signed nonce (ed25519 verify in an edge function).

Tables (all with RLS; writes to economy/progress only through `security definer` RPCs):

| table | key columns |
|---|---|
| `profiles` | `id uuid pk = auth.uid`, `handle citext unique`, `display_name`, `avatar_figure_id`, `rating int`, `shards int`, `streak int`, `best_streak int`, `last_daily_day int`, `wallet_address text unique null`, `created_at` |
| `level_progress` | `user_id`, `world int`, `level int`, `stars int`, `best_ms int`, `completed_at`; pk (user_id, world, level) |
| `daily_results` | `user_id`, `day int`, `challenge_id`, `elapsed_ms`, `solved int`, `points int`, `rating_before`, `rating_after`, `created_at`; pk (user_id, day) |
| `boss_events` | `id`, `name`, `palette`, `seed`, `stages jsonb`, `entry_shards`, `entry_lamports`, `starts_at`, `ends_at` |
| `boss_attempts` | `id`, `boss_id`, `user_id`, `paid_with`, `result`, `elapsed_ms`, `created_at` |
| `figures` | `id uuid`, `seed bigint`, `tier int`, `encoding text`, `rarity text`, `name`, `owner_id`, `mint_address text null`, `created_at` |
| `listings` | `id`, `figure_id`, `seller_id`, `price_shards int null`, `price_lamports bigint null`, `status`, `created_at` |
| `friendships` | `requester_id`, `addressee_id`, `status (pending/accepted/blocked)`, `created_at`; pk pair |
| `transactions` | `id`, `user_id`, `kind`, `shards_delta`, `lamports`, `signature text unique null`, `created_at` (append-only ledger) |
| `wallet_nonces` | `user_id`, `nonce`, `expires_at` |

RPCs: `complete_level(world, level, stars, elapsed_ms, proof jsonb)`, `submit_daily(day, outcomes jsonb)`, `buy_figure_shards(shop_item)`, `enter_boss(boss_id, pay_with)`, `finish_boss(attempt_id, result jsonb)`, `list_figure`, `buy_listing`, `send_friend_request(handle)`, `respond_friend_request`, `leaderboard(scope, limit)` (global / daily / friends / boss).

Edge functions / Next API: `wallet-link` (nonce + signature verify), `purchase/sol/prepare` (pre-checks, one partially-signed transaction: transfer → recipient, authority memo, Core create with payer = owner = buyer), `purchase/sol` (verify the landed transaction, claim its signature once, record ownership), `figure-metadata/[id]` (JSON + PNG).

Secrets: mint-authority keypair only in `.env.local` / `deploy/.env` (never committed); service-role key only server-side.

## 8. Level definitions (compact)

Levels are never stored as maps. `LevelDef(world, index)` is derived deterministically: `seed = Mix(worldSeed, index)`, kind rotation and difficulty band from a small per-world table. The database stores only progress rows. Boss stages likewise derive from `boss_events.seed`.

## 9. Workstreams and verification

| stream | owner | output |
|---|---|---|
| A. Unity v2 client (design system, shell, adventure, figures renderer, juice, backend client, WebGL build) | lead (main session) | `client/` |
| B. Backend + TS core (Supabase schema/RLS/RPC/tests, TS ports with fixtures) | agent | `backend/`, `packages/core/` |
| C. Web site (Next.js pages, figure viewer, wallet adapter devnet, marketplace, Unity embed) | agent | `web/` |
| R. Review | reviewer agent per milestone | findings, run tests |
| P. Production check | agent per milestone | security, secrets, performance, build matrix |

Milestones:
1. **M1 Foundation** — design system v2 in Unity, app shell with 5 tabs and real screens, no splash, figure generator + pixel-3D renderer + fixtures; Supabase running locally with schema; web scaffold with shared tokens.
2. **M2 Adventure** — world map, walker, monsters, levels, stars, bosses, economy, guided first levels, difficulty band, juice (particles, SFX, haptics, gyro).
3. **M3 Online** — Unity ↔ Supabase (anonymous auth, sync, leaderboards, friends, inventory, shop, boss entry), web marketplace + devnet NFT mint + wallet link, WebGL embed, Seeker MWA.
4. **M4 Production** — review + production pass, device QA on Pixel 6a, performance budgets, docs.
