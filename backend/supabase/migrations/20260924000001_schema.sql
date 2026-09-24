-- RONRIKU schema (docs/PLAN_V2.md §7). Every table has RLS. Clients never write economy/progress
-- tables directly: all such writes go through `security definer` RPCs (next migration).
--
-- Grant model: Supabase grants ALL on new public tables to anon/authenticated by default. We revoke
-- that and grant back only the SELECT columns/tables that are meant to be public or owner-readable.

create extension if not exists citext with schema extensions;

-- Objects created later by migrations (role postgres) get no privileges for clients unless granted
-- explicitly. Supabase's defaults would otherwise grant ALL/EXECUTE to anon and authenticated.
alter default privileges in schema public revoke all on tables from public, anon, authenticated;
alter default privileges in schema public revoke all on sequences from public, anon, authenticated;
alter default privileges in schema public revoke execute on functions from public, anon, authenticated;
-- PUBLIC's built-in EXECUTE default is global and cannot be revoked per schema.
alter default privileges revoke execute on functions from public;

-- ---------------------------------------------------------------------------------------------
-- Catalogue / game data
-- ---------------------------------------------------------------------------------------------

create table public.shop_items (
  id              text primary key,
  kind            text not null check (kind in ('figure', 'boss_entry')),
  tier            int check (tier between 3 and 5),
  rarity          text check (rarity in ('Common', 'Rare', 'Epic', 'Legendary')),
  price_shards    int check (price_shards > 0),
  price_lamports  bigint check (price_lamports > 0),
  active          boolean not null default true,
  sort            int not null default 0,
  check (price_shards is not null or price_lamports is not null),
  check (kind <> 'figure' or rarity is not null)
);
comment on table public.shop_items is
  'Price catalogue (PLAN 6): figure prices by rarity (Legendary SOL-only) and boss entry. The figures on sale each day are in shop_shelf.';

-- Daily shelf (Shop.cs ShopCatalogue.ForDay): 6 deterministic figures per UTC day, identical for
-- everyone. Written by the trusted publisher with @ronriku/core; buy_figure_shards sells from it.
create table public.shop_shelf (
  day            int not null,
  slot           int not null check (slot between 0 and 5),
  item_id        text not null,             -- 'fig-{ulong seed}-{size}' (C# ShopItem.Id)
  seed           bigint not null,           -- signed reinterpretation of the ulong seed
  tier           int not null check (tier between 3 and 5),
  rarity         text not null check (rarity in ('Common', 'Rare', 'Epic', 'Legendary')),
  name           text not null,
  encoding       text not null,
  price_shards   int,                       -- null = SOL only
  price_lamports bigint,
  primary key (day, slot),
  unique (day, item_id)
);

create table public.boss_events (
  id              uuid primary key default gen_random_uuid(),
  -- C# BossEvent.Id ('boss-w{week}-{tier}') for roster events; null for hand-made events.
  code            text unique,
  tier            int not null default 0 check (tier between 0 and 2),
  name            text not null,
  palette         text not null default 'boss',
  seed            bigint not null,
  -- [{ "kind": "Pattern"|"Spatial"|"Logic", "seed": "<signed 64-bit decimal>" }, ...]
  stages          jsonb not null check (jsonb_typeof(stages) = 'array' and jsonb_array_length(stages) between 1 and 5),
  entry_shards    int not null default 150 check (entry_shards >= 0),
  entry_lamports  bigint not null default 10000000 check (entry_lamports >= 0),
  reward_shards   int not null default 450 check (reward_shards between 300 and 1000),
  time_limit_ms   int not null default 300000 check (time_limit_ms > 0),
  starts_at       timestamptz not null,
  ends_at         timestamptz not null,
  created_at      timestamptz not null default now(),
  check (ends_at > starts_at)
);

-- ---------------------------------------------------------------------------------------------
-- Players
-- ---------------------------------------------------------------------------------------------

create table public.figures (
  id            uuid primary key default gen_random_uuid(),
  -- Signed 64-bit reinterpretation of the generator's ulong seed: generateFigure(BigInt(seed), tier).
  seed          bigint not null,
  tier          int not null check (tier between 3 and 5),
  -- RF1 encoding (PLAN §5). Filled by the trusted publisher that generated the figure.
  encoding      text not null check (encoding like 'RF1.%'),
  rarity        text not null check (rarity in ('Common', 'Rare', 'Epic', 'Legendary')),
  name          text not null,
  monster       boolean not null default false,
  owner_id      uuid references auth.users (id) on delete set null,
  mint_address  text unique,
  source        text not null default 'shop' check (source in ('shop', 'starter', 'sol', 'reward', 'admin')),
  created_at    timestamptz not null default now()
);
create index figures_owner_idx on public.figures (owner_id);

create table public.profiles (
  id                uuid primary key references auth.users (id) on delete cascade,
  handle            extensions.citext not null unique check (handle ~ '^[A-Za-z0-9_]{3,20}$'),
  display_name      text not null check (char_length(display_name) between 1 and 24),
  avatar_figure_id  uuid references public.figures (id) on delete set null,
  rating            int not null default 1200 check (rating between 100 and 3000),
  shards            int not null default 0 check (shards >= 0),
  streak            int not null default 0 check (streak >= 0),
  best_streak       int not null default 0 check (best_streak >= 0),
  last_daily_day    int not null default 0,
  completed_dailies int not null default 0 check (completed_dailies >= 0),
  wallet_address    text unique check (wallet_address ~ '^[1-9A-HJ-NP-Za-km-z]{32,44}$'),
  created_at        timestamptz not null default now()
);
create index profiles_rating_idx on public.profiles (rating desc, created_at);

create table public.level_progress (
  user_id       uuid not null references auth.users (id) on delete cascade,
  world         int not null check (world between 0 and 4),
  level         int not null check (level between 0 and 11),
  stars         int not null check (stars between 1 and 3),
  best_ms       int not null check (best_ms > 0),
  completed_at  timestamptz not null default now(),
  primary key (user_id, world, level)
);

create table public.daily_results (
  user_id        uuid not null references auth.users (id) on delete cascade,
  day            int not null,
  challenge_id   text not null,
  elapsed_ms     int not null check (elapsed_ms >= 0),
  solved         int not null check (solved between 0 and 3),
  points         int not null check (points >= 0),
  rating_before  int not null,
  rating_after   int not null,
  outcomes       jsonb not null,
  created_at     timestamptz not null default now(),
  primary key (user_id, day)
);
create index daily_results_board_idx on public.daily_results (day, points desc, elapsed_ms);

create table public.boss_attempts (
  id          uuid primary key default gen_random_uuid(),
  boss_id     uuid not null references public.boss_events (id) on delete cascade,
  user_id     uuid not null references auth.users (id) on delete cascade,
  paid_with   text not null check (paid_with in ('shards', 'sol')),
  result      text not null default 'open' check (result in ('open', 'won', 'lost')),
  elapsed_ms  int,
  detail      jsonb,
  created_at  timestamptz not null default now(),
  finished_at timestamptz
);
create index boss_attempts_board_idx on public.boss_attempts (boss_id, result, elapsed_ms);
-- At most one unfinished attempt per player and boss (enter_boss is idempotent on it).
create unique index boss_attempts_one_open on public.boss_attempts (boss_id, user_id) where result = 'open';

create table public.listings (
  id              uuid primary key default gen_random_uuid(),
  figure_id       uuid not null references public.figures (id) on delete cascade,
  seller_id       uuid not null references auth.users (id) on delete cascade,
  buyer_id        uuid references auth.users (id) on delete set null,
  price_shards    int check (price_shards between 1 and 1000000),
  price_lamports  bigint check (price_lamports > 0),
  status          text not null default 'active' check (status in ('active', 'sold', 'cancelled')),
  created_at      timestamptz not null default now(),
  closed_at       timestamptz,
  check ((price_shards is null) <> (price_lamports is null))
);
create unique index listings_one_active on public.listings (figure_id) where status = 'active';
create index listings_status_idx on public.listings (status, created_at desc);

create table public.friendships (
  requester_id  uuid not null references auth.users (id) on delete cascade,
  addressee_id  uuid not null references auth.users (id) on delete cascade,
  status        text not null default 'pending' check (status in ('pending', 'accepted', 'blocked')),
  created_at    timestamptz not null default now(),
  primary key (requester_id, addressee_id),
  check (requester_id <> addressee_id)
);
-- One row per unordered pair.
create unique index friendships_pair on public.friendships (least(requester_id, addressee_id), greatest(requester_id, addressee_id));
create index friendships_addressee_idx on public.friendships (addressee_id);

-- Append-only shard/lamport ledger.
create table public.transactions (
  id              uuid primary key default gen_random_uuid(),
  user_id         uuid not null references auth.users (id) on delete cascade,
  kind            text not null,
  shards_delta    int not null default 0,
  lamports        bigint,
  signature       text unique,
  ref_id          text,
  idempotency_key text,
  created_at      timestamptz not null default now()
);
create index transactions_user_idx on public.transactions (user_id, created_at desc);
create unique index transactions_idem on public.transactions (user_id, kind, idempotency_key) where idempotency_key is not null;

create table public.wallet_nonces (
  user_id     uuid primary key references auth.users (id) on delete cascade,
  nonce       text not null,
  expires_at  timestamptz not null
);

-- ---------------------------------------------------------------------------------------------
-- Server-only data written by the trusted publisher (backend/scripts/publish.ts, service role)
-- ---------------------------------------------------------------------------------------------

-- Answer keys for every playable puzzle stage so RPCs can verify submitted answers without the
-- generators. source = 'daily:<day>' | 'level:<world>:<index>' | 'boss:<boss uuid>'.
create table public.puzzle_keys (
  source               text not null,
  stage                int not null check (stage >= 0),
  kind                 text not null check (kind in ('Pattern', 'Spatial', 'Logic')),
  seed                 bigint not null,
  content_hash         text not null,
  par                  int not null default 0,
  target_ms            int not null,
  correct_option       int,        -- Pattern
  start_orientation    int,        -- Spatial
  solved_orientations  int[],      -- Spatial: orientations whose shadow equals the target
  solution             int[],      -- Logic: the unique solution path
  created_at           timestamptz not null default now(),
  primary key (source, stage)
);

-- Pre-generated random-seed figures used for the starter grant at sign-up (the server cannot run
-- the generator, so the publisher keeps this pool topped up).
create table public.figure_pool (
  id        bigserial primary key,
  seed      bigint not null,
  tier      int not null check (tier between 3 and 5),
  rarity    text not null check (rarity in ('Common', 'Rare', 'Epic', 'Legendary')),
  name      text not null,
  encoding  text not null,
  unique (seed, tier)
);
create index figure_pool_pick_idx on public.figure_pool (tier, rarity, id);

-- ---------------------------------------------------------------------------------------------
-- RLS + grants
-- ---------------------------------------------------------------------------------------------

alter table public.shop_items      enable row level security;
alter table public.shop_shelf      enable row level security;
alter table public.boss_events     enable row level security;
alter table public.figures         enable row level security;
alter table public.profiles        enable row level security;
alter table public.level_progress  enable row level security;
alter table public.daily_results   enable row level security;
alter table public.boss_attempts   enable row level security;
alter table public.listings        enable row level security;
alter table public.friendships     enable row level security;
alter table public.transactions    enable row level security;
alter table public.wallet_nonces   enable row level security;
alter table public.puzzle_keys     enable row level security;
alter table public.figure_pool     enable row level security;

revoke all on all tables in schema public from anon, authenticated;
revoke all on all sequences in schema public from anon, authenticated;

-- Public catalogue and game data.
grant select on public.shop_items, public.shop_shelf, public.boss_events, public.figures, public.listings to anon, authenticated;
create policy shop_items_read on public.shop_items for select using (active);
create policy shop_shelf_read on public.shop_shelf for select using (true);
create policy boss_events_read on public.boss_events for select using (true);
create policy figures_read on public.figures for select using (true);
create policy listings_read on public.listings for select using (true);

-- Profiles: public columns for everyone; economy columns (shards, wallet) only via ensure_profile().
grant select (id, handle, display_name, avatar_figure_id, rating, streak, best_streak, last_daily_day,
              completed_dailies, created_at) on public.profiles to anon, authenticated;
create policy profiles_read on public.profiles for select using (true);

-- Owner-only reads.
grant select on public.level_progress, public.daily_results, public.boss_attempts, public.transactions to authenticated;
create policy level_progress_own on public.level_progress for select to authenticated using (user_id = (select auth.uid()));
create policy daily_results_own on public.daily_results for select to authenticated using (user_id = (select auth.uid()));
create policy boss_attempts_own on public.boss_attempts for select to authenticated using (user_id = (select auth.uid()));
create policy transactions_own on public.transactions for select to authenticated using (user_id = (select auth.uid()));

-- Friendships: visible to both parties.
grant select on public.friendships to authenticated;
create policy friendships_parties on public.friendships for select to authenticated
  using ((select auth.uid()) in (requester_id, addressee_id));

-- wallet_nonces, puzzle_keys, figure_pool: no grants and no policies → service role only.
