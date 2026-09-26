# Odlet backend (local Supabase)

The backend is a Supabase CLI project in `supabase/`, set up per `docs/PLAN_V2.md` §7:

- `migrations/20260924000001_schema.sql`: tables, RLS and grants.
- `migrations/20260924000002_rpc.sql`: RPCs and the auth trigger.
- `seed.sql`: price catalogue and one always-active launch boss.
- `functions/wallet-link`: Deno edge function for wallet ownership.
- `tests/security.test.sql`: pgTAP structural checks.
- `scripts/publish.ts`: trusted content publisher.
- `tests/*.test.ts`: vitest integration suite using supabase-js.

## Run

```bash
cd backend
npx supabase start                  # first run pulls Docker images
npx supabase db reset               # apply migrations + seed.sql
pnpm publish:content                # REQUIRED after every reset: puzzle keys, shelf, boss roster, starter pool
pnpm test                           # builds @ronriku/core, publishes a small window, runs 36 integration tests
npx supabase test db                # pgTAP (8 checks)
npx supabase stop                   # data is kept; `stop --no-backup` wipes it
```

When a new edge function is added, restart the stack with `stop` and then `start`.

### Local URLs

| service | URL |
|---|---|
| API / REST / Auth | http://127.0.0.1:54321 (`/rest/v1`, `/auth/v1`) |
| Edge functions | http://127.0.0.1:54321/functions/v1/wallet-link |
| Postgres | postgresql://postgres:postgres@127.0.0.1:54322/postgres |
| Studio | http://127.0.0.1:54323 |
| Mailpit | http://127.0.0.1:54324 |

Get the keys with `npx supabase status -o env`. These are the standard local dev keys and are not secrets. Put runtime values in the git-ignored `backend/.env.local` (see `.env.example`). Scripts and tests fall back to `supabase status` automatically. Service-role keys belong on servers only.

To save RAM, `config.toml` disables realtime, vector storage and analytics. It enables anonymous sign-ins (rate limit raised to 1000/h for local tests) and manual identity linking.

## Design: how the server stays authoritative without running the generators

SQL cannot run the C#/TS generators, so `scripts/publish.ts` (service role, `@ronriku/core`) writes the derived data into tables that clients cannot read. The script is idempotent. Run it daily in production (cron), and after every `db reset` locally.

| table | written by publisher | used by |
|---|---|---|
| `puzzle_keys` (private) | answer key for each stage: Daily days `today-2 … today+30`, all 5×12 adventure levels (boss = 3 stages), every `boss_events` row. Pattern stores the correct option, Spatial the start orientation, par and solved orientations, Logic the unique solution path (par = cells − 1). | `complete_level`, `submit_daily`, `finish_boss` replay the submitted answers (`_verify_stage`) |
| `shop_shelf` (public read) | `shopShelf(day)` = C# `ShopCatalogue.ForDay`: 6 figures/day with RF1 encoding, rarity and price | `buy_figure_shards` |
| `boss_events` | weekly roster from C# `BossEvent.ForWeek` (this and next week), upserted by `code` = `boss-w{week}-{tier}` | bosses |
| `figure_pool` (private) | 4×4 Common figures from random seeds | sign-up starter figure |

As a result, `figures.encoding` is always filled at insert time, and no RPC trusts a client "solved" flag.

Figure seeds are stored as signed `bigint`, the reinterpretation of the generator's `ulong`. Rebuild a figure with `generateFigure(BigInt(row.seed), row.tier)`.

## RPC contract (supabase-js: `supabase.rpc(name, { p_… })`)

Errors are raised as stable snake_case messages in `error.message`.

| RPC | args | returns / notes |
|---|---|---|
| `ensure_profile()` | — | Full own `profiles` row (incl. `shards`, `wallet_address`). The profile is created by a trigger on sign-up (150 shards + starter figure as avatar); this call is idempotent. |
| `update_profile` | `p_handle?`, `p_display_name?`, `p_avatar_figure_id?` | Own profile. Errors: `invalid_handle` (`^[A-Za-z0-9_]{3,20}$`), `handle_taken`, `figure_not_owned`. |
| `complete_level` | `p_world` 0–4, `p_level` 0–11, `p_stars` 1–3 or null, `p_elapsed_ms`, `p_proof` (see payloads) | `{world, level, stars, run_stars, best_ms, earned, first_clear}`. The server rule for stars is canonical: 1 solved; 2 clean, meaning no **Spatial** stage over par, with moves = greatest(client, answer length); 3 also within the summed target ms. Stars are capped at `p_stars`. First clear pays `(boss?300:20)+10·(stars−1)`; replays pay 10 per new star. Errors: `level_locked`, `not_solved`, `invalid_proof`, `invalid_elapsed`, `puzzle_not_published`. |
| `submit_daily` | `p_day` (must be today UTC), `p_outcomes` (see payloads) | `{counted, solved, points, rating_before, rating_after, streak, shards_earned, outcomes}`. Points and rating follow C# `TrialScoring` / `ReasoningRating`, computed server-side. Over-par penalty is Spatial 60/move and Logic 15/step (par = cells − 1). Reward is `100+min(100,10·streak)`, paid **only if ≥ 1 trial is solved**. A second submission returns `counted:false`. `kind` is compared case-insensitively. |
| `buy_figure_shards` | `p_item` = today's shelf `item_id` (`fig-{seed}-{size}`) | The new `figures` row. Errors: `insufficient_shards`, `already_owned` (one copy per design), `sol_only_item` (Legendary), `unknown_item`. |
| `enter_boss` | `p_boss_id`, `p_pay_with` = `'shards'` | `boss_attempts` row; its `created_at` is the server start time. Reuses the open attempt, so there is no double charge. Checks puzzle keys **before** charging (`puzzle_not_published`). SOL entry goes through the server route → `enter_boss_sol`. |
| `enter_boss_sol` (service role) | `p_user`, `p_boss_id`, `p_signature`, `p_lamports` | Requires an active event, `p_lamports ≥ entry_lamports` (`underpaid`), an existing profile and published keys. Idempotent: returns the open attempt if one exists. A reused signature raises `signature_used`. |
| `finish_boss` | `p_attempt_id`, `p_result` (see payloads) | `{result: won/lost, earned, first_win, elapsed_ms, stages}`. A win needs every stage solved, client elapsed ≤ limit, and server elapsed (now − attempt start) ≤ limit + 30 s. The stored and ranked time is `greatest(client, server − 5 s)`. **`reward_shards` is paid only on the player's first win per boss**; later wins are recorded but pay 0. |
| `list_figure` | `p_figure_id`, `p_price_shards` or `p_price_lamports` | `listings` row. Requires progress (`progress_required`). The avatar cannot be listed. Lamport listings require a minted figure. |
| `cancel_listing` | `p_listing_id` | Seller only. |
| `buy_listing` | `p_listing_id` | Shard listings only. Atomic debit, credit and ownership transfer. The buyer needs progress (`progress_required`); the seller can receive at most 2000 shards from sales per UTC day (`seller_daily_cap`). |
| `send_friend_request` | `p_handle` (case-insensitive) | `{status, user_id}`. A mutual request, including two requests sent at the same moment, auto-accepts. |
| `respond_friend_request` | `p_requester_id`, `p_action` = accept/decline/block | `{status}` |
| `remove_friend` | `p_other` | — |
| `list_friends()` | — | Rows `{user_id, handle, display_name, avatar_figure_id, rating, status, direction}` |
| `leaderboard` | `p_scope` = global/daily/friends/boss, `p_limit` ≤ 200, `p_ref?` (day number / boss uuid) | Rows `{rank, user_id, handle, display_name, avatar_figure_id, score, elapsed_ms}`. Ties share a rank (`rank()`). A malformed `p_ref` raises `invalid_ref`. Callable by anon, except `friends`. |
| `daily_day(p_at?)` | — | UTC day number (day 1 = 2026-09-01) |

### Client payloads (exact shapes the Unity client sends)

```jsonc
// submit_daily p_outcomes — Daily order; kind = C# enum name (compared case-insensitively)
[{"kind":"Pattern","elapsed_ms":12345,"resets":0,"moves":1,"answer":2},
 {"kind":"Spatial","elapsed_ms":23456,"resets":1,"moves":5,"answer":[0,1,3]},
 {"kind":"Logic","elapsed_ms":34567,"resets":0,"moves":27,"answer":[14,13,...]}]
// complete_level p_proof — arrays parallel to answers (1 stage; 3 for level 11); moves/resets optional
{"answers":[a1], "moves":[m1], "resets":[r1]}
// finish_boss p_result
{"answers":[a,b,c], "moves":[m,m,m], "resets":[r,r,r], "elapsed_ms":123456}
```

Answer formats: Pattern = option index. Spatial = move list (0 TurnLeft, 1 TurnRight, 2 TipBack, 3 TipForward, max 64). Logic = full cell path (`x + size·y`). A null or missing answer means unsolved.

`moves` and `resets` are client-reported, but the server applies floors:
- Spatial moves = greatest(client, answer length).
- Logic moves = greatest(client, path length − 1).
- Pattern moves are ignored.
- Resets are clamped to 0..50.

So misreporting can only make a score worse than honest play. Elapsed times are plausibility-checked only, except in bosses, where server time bounds them.

### Anti-abuse rules

- **Boss rewards:** paid on the first win per boss only, which stops enter/finish farming.
- **Daily shards:** require ≥ 1 solved trial, so empty submissions from new anonymous accounts earn nothing.
- **Marketplace (`list_figure`, `buy_listing`):** requires "progress" (≥ 1 cleared level or a Daily with ≥ 1 solved trial). A seller can receive at most 2000 shards from sales per UTC day.
- **Starter figure:** taken from `figure_pool`. If the pool is empty at sign-up, the profile is still created and `ensure_profile` grants the figure later.
- **Serialisation:** every economy RPC locks the player's profile row first, so concurrent calls by one player run one at a time (e.g. a first clear pays once).

### Reads (RLS)

- **Public (anon):** `shop_items`, `shop_shelf`, `boss_events`, `figures`, `listings`, and these profile columns: `id, handle, display_name, avatar_figure_id, rating, streak, best_streak, last_daily_day, completed_dailies, created_at`. Because of column grants, select these columns explicitly; `select('*')` on `profiles` is denied.
- **Owner only:** `level_progress`, `daily_results`, `boss_attempts`, `transactions` (the append-only ledger). `friendships` is visible to both parties.
- **Service role only:** `puzzle_keys`, `figure_pool`, `wallet_nonces`, plus `enter_boss_sol` and the `_helpers`. Default privileges in `public` are revoked, so new tables and functions start closed to anon/authenticated. pgTAP checks that clients can execute only an explicit allowlist of RPCs.
- **Client writes:** none. Every write goes through the RPCs above.

## wallet-link edge function

1. `POST /functions/v1/wallet-link {action:"nonce"}` with the user JWT returns `{nonce, expires_at, message}`.
2. Sign the UTF-8 bytes of `message` verbatim. It is SIWS-style: `{domain} wants you to link your Solana wallet to Odlet.` followed by User, Nonce and Expires lines. The domain comes from `WALLET_LINK_DOMAIN` (default `odlet.xyz`). The format is in `functions/_shared/wallet-message.ts`.
3. `POST {action:"verify", address:<base58 pubkey>, signature:<base58 64 bytes>}` returns `{wallet_address}`. The ed25519 check uses tweetnacl. Nonces live 5 minutes and are single-use: they are consumed by an atomic `DELETE … RETURNING`, and a request that deletes 0 rows fails with 404. Errors: 409 `wallet_in_use`, 422 `bad_signature`, 404 `no_nonce`, 410 `nonce_expired`.

## Not done yet

- `sol-purchase` (verify a devnet transfer, then mint a Core NFT) and `figure-metadata/[id]`. The DB side is ready: `enter_boss_sol`, `figures.mint_address`, `transactions.signature` unique, and `renderFigurePng` in `@ronriku/core/render`.
- Daily and level elapsed times are only plausibility-checked (≥ 0.8 s per solved Daily trial, 1 s–1 h per level). Boss times are bounded by server time.
