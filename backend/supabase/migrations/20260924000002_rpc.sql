-- RONRIKU RPCs. All economy/progress writes happen here, as `security definer` functions with
-- `set search_path = public`, input validation and idempotency. Internal helpers start with `_`
-- and are not executable by clients.
--
-- Contract for clients (supabase-js: supabase.rpc(name, { p_x: ... })) is documented in
-- backend/README.md. Error messages are stable snake_case codes (e.g. 'insufficient_shards').
--
-- Concurrency: every RPC that changes a player's economy/progress first locks that player's
-- profile row (`for update`), which serialises one player's calls and removes check-then-insert races.

-- ---------------------------------------------------------------------------------------------
-- Constants and helpers
-- ---------------------------------------------------------------------------------------------

create or replace function public._uid() returns uuid
language plpgsql stable set search_path = public as $$
declare v uuid := auth.uid();
begin
  if v is null then raise exception 'not_authenticated' using errcode = '28000'; end if;
  return v;
end $$;

-- Locks and returns the caller's profile row.
create or replace function public._lock_profile(p_user uuid) returns public.profiles
language plpgsql set search_path = public as $$
declare v public.profiles;
begin
  select * into v from public.profiles where id = p_user for update;
  if v.id is null then raise exception 'profile_missing'; end if;
  return v;
end $$;

-- Daily day number: day 1 = 2026-09-01, boundary 00:00 UTC (DailyCalendar).
create or replace function public.daily_day(p_at timestamptz default now()) returns int
language sql stable set search_path = public as $$
  select ((p_at at time zone 'utc')::date - date '2026-09-01') + 1
$$;

create or replace function public._challenge_id(p_day int) returns text
language sql immutable set search_path = public as $$
  select 'daily-' || to_char(date '2026-09-01' + (p_day - 1), 'YYYYMMDD') || '-v2'
$$;

-- A client-reported count (moves/resets): integer JSON number clamped to [lo, hi], else lo.
create or replace function public._client_int(p jsonb, p_lo int, p_hi int) returns int
language sql immutable set search_path = public as $$
  select case when jsonb_typeof(p) = 'number' and p::text ~ '^\d{1,9}$'
              then least(greatest(p::text::int, p_lo), p_hi) else p_lo end
$$;

create or replace function public._ledger(p_user uuid, p_kind text, p_delta int, p_ref text default null,
  p_idem text default null, p_lamports bigint default null, p_signature text default null) returns void
language sql set search_path = public as $$
  insert into public.transactions (user_id, kind, shards_delta, ref_id, idempotency_key, lamports, signature)
  values (p_user, p_kind, p_delta, p_ref, p_idem, p_lamports, p_signature)
$$;

-- Adds (or with a negative delta, debits) shards. Raises insufficient_shards instead of going < 0.
create or replace function public._add_shards(p_user uuid, p_delta int, p_kind text, p_ref text default null,
  p_idem text default null) returns int
language plpgsql set search_path = public as $$
declare v_balance int;
begin
  update public.profiles set shards = shards + p_delta
   where id = p_user and shards + p_delta >= 0
  returning shards into v_balance;
  if v_balance is null then
    if not exists (select 1 from public.profiles where id = p_user) then raise exception 'profile_missing'; end if;
    raise exception 'insufficient_shards';
  end if;
  perform public._ledger(p_user, p_kind, p_delta, p_ref, p_idem);
  return v_balance;
end $$;

-- "Non-trivial progress" (anti-Sybil gate for the marketplace): at least one cleared level or one
-- Daily with a solved trial.
create or replace function public._has_progress(p_user uuid) returns boolean
language sql stable set search_path = public as $$
  select exists (select 1 from public.level_progress where user_id = p_user)
      or exists (select 1 from public.daily_results where user_id = p_user and solved >= 1)
$$;

-- CubeOrientations transition table (24 orientations × 4 moves), exported from @ronriku/core
-- (identical to the C# BFS order). Checked by backend/tests.
create or replace function public._spatial_apply(p_orientation int, p_move int) returns int
language sql immutable set search_path = public as $$
  select ('{1,2,3,4,5,0,6,7,0,5,8,9,10,11,12,0,13,14,0,12,2,1,15,16,17,18,19,1,20,21,1,19,21,20,22,2,18,17,2,22,16,3,17,21,3,16,20,18,22,19,4,3,15,4,18,20,4,15,21,17,14,13,23,5,11,10,5,23,9,6,14,10,6,9,11,13,12,23,7,6,8,7,13,11,7,8,10,14,23,12,9,8,19,22,16,15}'::int[])[p_orientation * 4 + p_move + 1]
$$;

-- Replays one submitted answer against the published key.
--   Pattern: answer = option index (number).       moves = 0, par = 0
--   Spatial: answer = moves array (0..3, ≤ 64).     moves = answer length, par from key
--   Logic:   answer = cell path (= unique solution). moves = path length − 1, par = cells − 1
create or replace function public._verify_stage(p_source text, p_stage int, p_answer jsonb,
  out solved boolean, out moves int, out par int, out target_ms int, out kind text)
language plpgsql stable set search_path = public as $$
declare
  k public.puzzle_keys;
  o int;
  m jsonb;
  path int[];
begin
  select * into k from public.puzzle_keys where source = p_source and stage = p_stage;
  if not found then raise exception 'puzzle_not_published'; end if;
  solved := false; moves := 0; par := k.par; target_ms := k.target_ms; kind := k.kind;
  if p_answer is null or p_answer = 'null'::jsonb then return; end if;

  if k.kind = 'Pattern' then
    solved := jsonb_typeof(p_answer) = 'number' and p_answer::text ~ '^\d{1,9}$' and p_answer::text::int = k.correct_option;
  elsif k.kind = 'Spatial' then
    if jsonb_typeof(p_answer) <> 'array' or jsonb_array_length(p_answer) > 64 then return; end if;
    o := k.start_orientation;
    for m in select value from jsonb_array_elements(p_answer) loop
      if jsonb_typeof(m) <> 'number' or m::text !~ '^[0-3]$' then return; end if;
      o := public._spatial_apply(o, m::text::int);
    end loop;
    moves := jsonb_array_length(p_answer);
    solved := o = any (k.solved_orientations);
  else
    if jsonb_typeof(p_answer) <> 'array' or jsonb_array_length(p_answer) <> coalesce(array_length(k.solution, 1), 0) then return; end if;
    if exists (select 1 from jsonb_array_elements(p_answer) e where jsonb_typeof(e.value) <> 'number' or e.value::text !~ '^\d{1,3}$') then return; end if;
    select array_agg(e.value::text::int order by e.ord) into path from jsonb_array_elements(p_answer) with ordinality e(value, ord);
    moves := array_length(path, 1) - 1;
    solved := path = k.solution;
  end if;
end $$;

-- Raises puzzle_not_published unless every stage of a source has a key (checked before charging).
create or replace function public._require_keys(p_source text, p_stages int) returns void
language plpgsql stable set search_path = public as $$
begin
  if (select count(*) from public.puzzle_keys where source = p_source and stage < p_stages) < p_stages then
    raise exception 'puzzle_not_published';
  end if;
end $$;

-- Over-par penalty per kind (SpatialPuzzleScorer 60, LogicPuzzleScreen 15, Pattern none).
create or replace function public._over_par_penalty(p_kind text) returns int
language sql immutable set search_path = public as $$
  select case p_kind when 'Spatial' then 60 when 'Logic' then 15 else 0 end
$$;

-- TrialScoring.Points at Standard difficulty (base 700 + 300·2).
create or replace function public._trial_points(p_solved boolean, p_elapsed int, p_target int, p_moves int,
  p_par int, p_resets int, p_penalty int) returns int
language sql immutable set search_path = public as $$
  select case when not p_solved then 0 else greatest(100,
    1300 + greatest(0, p_target - greatest(0, p_elapsed)) / 100
    - greatest(0, p_moves - p_par) * p_penalty - greatest(0, p_resets) * 120) end
$$;

-- ReasoningRating.Performance.
create or replace function public._performance(p_solved boolean, p_elapsed int, p_target int, p_moves int,
  p_par int, p_resets int) returns double precision
language sql immutable set search_path = public as $$
  select case when not p_solved then 0.0 else
    least(1.0, greatest(0.0,
      0.5
      + 0.25 * least(1.0, greatest(0.0, 1.0 - p_elapsed / (2.0 * greatest(1, p_target))))
      + 0.25 * (case when p_par <= 0 then 1.0 else p_par::double precision / greatest(p_moves, p_par) end)
      - least(0.2, 0.1 * greatest(0, p_resets))))
  end
$$;

-- Grants the 4×4 starter figure from the pool if the player has none yet. A no-op when the pool is
-- empty; ensure_profile retries it lazily, so sign-up never fails on an empty pool.
create or replace function public._grant_starter(p_user uuid) returns void
language plpgsql set search_path = public as $$
declare v_pool public.figure_pool; v_figure uuid;
begin
  if exists (select 1 from public.figures where owner_id = p_user and source = 'starter') then return; end if;
  delete from public.figure_pool
   where id = (select id from public.figure_pool where tier = 4 and rarity = 'Common' order by id limit 1 for update skip locked)
  returning * into v_pool;
  if v_pool.id is null then return; end if;
  insert into public.figures (seed, tier, encoding, rarity, name, owner_id, source)
  values (v_pool.seed, v_pool.tier, v_pool.encoding, v_pool.rarity, v_pool.name, p_user, 'starter')
  returning id into v_figure;
  update public.profiles set avatar_figure_id = v_figure where id = p_user and avatar_figure_id is null;
end $$;

-- Creates the profile for a new auth user: generated handle, 150 starter shards, starter figure.
create or replace function public._create_profile(p_user uuid) returns void
language plpgsql security definer set search_path = public as $$
begin
  insert into public.profiles (id, handle, display_name, shards)
  values (p_user, 'p_' || substr(replace(p_user::text, '-', ''), 1, 12), 'PLAYER ' || upper(substr(p_user::text, 1, 4)), 0)
  on conflict (id) do nothing;
  if not found then return; end if;
  perform public._add_shards(p_user, 150, 'signup_bonus', null, 'signup');
  perform public._grant_starter(p_user);
end $$;

create or replace function public._on_auth_user_created() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  perform public._create_profile(new.id);
  return new;
end $$;

create trigger on_auth_user_created after insert on auth.users
  for each row execute function public._on_auth_user_created();

-- ---------------------------------------------------------------------------------------------
-- Profile
-- ---------------------------------------------------------------------------------------------

-- Returns the caller's full profile (including shards and wallet), creating it if needed and
-- granting a missing starter figure (e.g. when the pool was empty at sign-up).
create or replace function public.ensure_profile() returns public.profiles
language plpgsql security definer set search_path = public as $$
declare v_uid uuid := public._uid(); v public.profiles;
begin
  perform public._create_profile(v_uid);
  perform public._lock_profile(v_uid);
  perform public._grant_starter(v_uid);
  select * into v from public.profiles where id = v_uid;
  return v;
end $$;

-- Updates non-economy fields. Null arguments are left unchanged. The avatar must be owned and unlisted.
create or replace function public.update_profile(p_handle text default null, p_display_name text default null,
  p_avatar_figure_id uuid default null) returns public.profiles
language plpgsql security definer set search_path = public as $$
declare v_uid uuid := public._uid(); v public.profiles;
begin
  if p_handle is not null and p_handle !~ '^[A-Za-z0-9_]{3,20}$' then raise exception 'invalid_handle'; end if;
  if p_display_name is not null and char_length(btrim(p_display_name)) not between 1 and 24 then raise exception 'invalid_display_name'; end if;
  perform public._lock_profile(v_uid);
  if p_avatar_figure_id is not null and not exists (
      select 1 from public.figures f where f.id = p_avatar_figure_id and f.owner_id = v_uid
        and not exists (select 1 from public.listings l where l.figure_id = f.id and l.status = 'active')) then
    raise exception 'figure_not_owned';
  end if;
  begin
    update public.profiles set
      handle = coalesce(p_handle, handle),
      display_name = coalesce(btrim(p_display_name), display_name),
      avatar_figure_id = coalesce(p_avatar_figure_id, avatar_figure_id)
    where id = v_uid returning * into v;
  exception when unique_violation then raise exception 'handle_taken';
  end;
  return v;
end $$;

-- ---------------------------------------------------------------------------------------------
-- Adventure
-- ---------------------------------------------------------------------------------------------

-- p_proof = { "answers": [a...], "moves": [m...]?, "resets": [r...]? } — arrays parallel to the
-- stages (1 stage; 3 for the world boss, level 11). Stars are recomputed on the server (canonical):
-- 1 = solved, 2 = clean (no Spatial stage over par; moves = greatest(client, answer length)),
-- 3 = clean and elapsed ≤ summed stage target ms; capped by p_stars. First clear pays
-- (boss ? 300 : 20) + 10·(stars − 1); replays pay 10 per newly earned star (idempotent).
create or replace function public.complete_level(p_world int, p_level int, p_stars int, p_elapsed_ms int,
  p_proof jsonb) returns jsonb
language plpgsql security definer set search_path = public as $$
declare
  v_uid uuid := public._uid();
  v_source text;
  v_stages int;
  v_answers jsonb := p_proof -> 'answers';
  v_moves jsonb := p_proof -> 'moves';
  v_clean boolean := true;
  v_target int := 0;
  v_stars int;
  v_prev public.level_progress;
  v_earned int;
  v_boss boolean := p_level = 11;
  r record;
begin
  if p_world is null or p_world not between 0 and 4 or p_level is null or p_level not between 0 and 11 then raise exception 'invalid_level'; end if;
  if p_elapsed_ms is null or p_elapsed_ms not between 1000 and 3600000 then raise exception 'invalid_elapsed'; end if;
  if p_stars is not null and p_stars not between 1 and 3 then raise exception 'invalid_stars'; end if;
  v_stages := case when v_boss then 3 else 1 end;
  if v_answers is null or jsonb_typeof(v_answers) <> 'array' or jsonb_array_length(v_answers) <> v_stages then raise exception 'invalid_proof'; end if;
  if v_moves is not null and jsonb_typeof(v_moves) <> 'array' then raise exception 'invalid_proof'; end if;

  perform public._lock_profile(v_uid);

  -- Unlock rule (AdventureProgress.IsUnlocked).
  if not (p_level = 0 and p_world = 0) and not exists (
      select 1 from public.level_progress
       where user_id = v_uid
         and ((p_level = 0 and world = p_world - 1 and level = 11) or (p_level > 0 and world = p_world and level = p_level - 1))) then
    raise exception 'level_locked';
  end if;

  v_source := format('level:%s:%s', p_world, p_level);
  for i in 0 .. v_stages - 1 loop
    select * into r from public._verify_stage(v_source, i, v_answers -> i);
    if not r.solved then raise exception 'not_solved'; end if;
    v_clean := v_clean and (r.kind <> 'Spatial' or greatest(r.moves, public._client_int(v_moves -> i, 0, 100000)) <= r.par);
    v_target := v_target + r.target_ms;
  end loop;
  v_stars := case when not v_clean then 1 when p_elapsed_ms <= v_target then 3 else 2 end;
  v_stars := least(v_stars, coalesce(p_stars, 3));

  select * into v_prev from public.level_progress where user_id = v_uid and world = p_world and level = p_level;
  if v_prev.user_id is null then
    insert into public.level_progress (user_id, world, level, stars, best_ms) values (v_uid, p_world, p_level, v_stars, p_elapsed_ms);
    v_earned := (case when v_boss then 300 else 20 end) + (v_stars - 1) * 10;
  else
    v_earned := greatest(0, v_stars - v_prev.stars) * 10;
    update public.level_progress set stars = greatest(stars, v_stars), best_ms = least(best_ms, p_elapsed_ms), completed_at = now()
     where user_id = v_uid and world = p_world and level = p_level;
  end if;
  if v_earned > 0 then perform public._add_shards(v_uid, v_earned, 'level_clear', v_source); end if;

  return jsonb_build_object('world', p_world, 'level', p_level, 'stars', greatest(v_stars, coalesce(v_prev.stars, 0)),
    'run_stars', v_stars, 'best_ms', least(p_elapsed_ms, coalesce(v_prev.best_ms, p_elapsed_ms)), 'earned', v_earned,
    'first_clear', v_prev.user_id is null);
end $$;

-- ---------------------------------------------------------------------------------------------
-- Daily
-- ---------------------------------------------------------------------------------------------

-- p_outcomes = [{ "kind": "Pattern", "elapsed_ms": 12345, "resets": 0, "moves": 1, "answer": 2 },
--               { "kind": "Spatial", ..., "moves": 5, "answer": [0, 1, 3] },
--               { "kind": "Logic", ..., "moves": 27, "answer": [14, 13, ...] }]
-- in Daily order; kind compared case-insensitively. A null/missing answer means unsolved. moves and
-- resets are client-reported but floored: moves = greatest(client, answer-derived), resets clamped
-- 0..50, so misreporting can only lower a score. Only today's Daily (UTC) is accepted; the first
-- submission counts, later ones return the stored result with counted = false. The shard reward
-- (100 + 10·streak, max 200) is paid only when at least one trial is solved.
create or replace function public.submit_daily(p_day int, p_outcomes jsonb) returns jsonb
language plpgsql security definer set search_path = public as $$
declare
  v_uid uuid := public._uid();
  v_kinds text[] := array['pattern', 'spatial', 'logic'];
  v_profile public.profiles;
  v_existing public.daily_results;
  v_item jsonb;
  v_elapsed int;
  v_resets int;
  v_moves int;
  v_total_ms int := 0;
  v_solved int := 0;
  v_points int := 0;
  v_perf_sum double precision := 0;
  v_expected double precision;
  v_k int;
  v_rating int;
  v_streak int;
  v_reward int;
  v_detail jsonb := '[]'::jsonb;
  v_pts int;
  r record;
begin
  if p_day is null or p_day <> public.daily_day() then raise exception 'wrong_day'; end if;
  if p_outcomes is null or jsonb_typeof(p_outcomes) <> 'array' or jsonb_array_length(p_outcomes) <> 3 then raise exception 'invalid_outcomes'; end if;

  v_profile := public._lock_profile(v_uid);

  select * into v_existing from public.daily_results where user_id = v_uid and day = p_day;
  if v_existing.user_id is not null then
    return jsonb_build_object('counted', false, 'day', p_day, 'challenge_id', v_existing.challenge_id,
      'solved', v_existing.solved, 'points', v_existing.points, 'rating_before', v_existing.rating_before,
      'rating_after', v_existing.rating_after, 'streak', v_profile.streak, 'shards_earned', 0, 'outcomes', v_existing.outcomes);
  end if;

  for i in 0 .. 2 loop
    v_item := p_outcomes -> i;
    if jsonb_typeof(v_item) <> 'object' or lower(v_item ->> 'kind') is distinct from v_kinds[i + 1] then raise exception 'invalid_outcomes'; end if;
    if jsonb_typeof(v_item -> 'elapsed_ms') <> 'number' or (v_item ->> 'elapsed_ms') !~ '^\d{1,7}$' then raise exception 'invalid_outcomes'; end if;
    v_elapsed := (v_item ->> 'elapsed_ms')::int;
    if v_elapsed > 3600000 then raise exception 'invalid_outcomes'; end if;
    v_resets := public._client_int(v_item -> 'resets', 0, 50);

    select * into r from public._verify_stage('daily:' || p_day, i, v_item -> 'answer');
    if r.solved and v_elapsed < 800 then raise exception 'implausible_time'; end if;
    v_moves := case when r.kind = 'Pattern' then 0 else greatest(r.moves, public._client_int(v_item -> 'moves', 0, 100000)) end;
    v_pts := public._trial_points(r.solved, v_elapsed, r.target_ms, v_moves, r.par, v_resets, public._over_par_penalty(r.kind));
    v_total_ms := v_total_ms + v_elapsed;
    v_points := v_points + v_pts;
    if r.solved then v_solved := v_solved + 1; end if;
    v_expected := 1.0 / (1.0 + power(10.0, (1300 - v_profile.rating) / 400.0));
    v_perf_sum := v_perf_sum + public._performance(r.solved, v_elapsed, r.target_ms, v_moves, r.par, v_resets) - v_expected;
    v_detail := v_detail || jsonb_build_object('kind', r.kind, 'solved', r.solved, 'elapsed_ms', v_elapsed,
      'moves', v_moves, 'par', r.par, 'resets', v_resets, 'points', v_pts);
  end loop;

  -- ReasoningRating.UpdateOverall: Δ = round(K · mean(p − E)), K = 40 for the first 10 Dailies.
  v_k := case when v_profile.completed_dailies < 10 then 40 else 24 end;
  v_rating := least(3000, greatest(100, v_profile.rating + round((v_k * v_perf_sum / 3)::numeric)::int));
  v_streak := case when v_profile.completed_dailies > 0 and v_profile.last_daily_day = p_day - 1 then v_profile.streak + 1 else 1 end;
  v_reward := case when v_solved >= 1 then 100 + least(100, 10 * v_streak) else 0 end;

  insert into public.daily_results (user_id, day, challenge_id, elapsed_ms, solved, points, rating_before, rating_after, outcomes)
  values (v_uid, p_day, public._challenge_id(p_day), v_total_ms, v_solved, v_points, v_profile.rating, v_rating, v_detail);

  update public.profiles set rating = v_rating, streak = v_streak, best_streak = greatest(best_streak, v_streak),
    last_daily_day = p_day, completed_dailies = completed_dailies + 1
   where id = v_uid;
  if v_reward > 0 then perform public._add_shards(v_uid, v_reward, 'daily', public._challenge_id(p_day), 'daily:' || p_day); end if;

  return jsonb_build_object('counted', true, 'day', p_day, 'challenge_id', public._challenge_id(p_day),
    'solved', v_solved, 'points', v_points, 'rating_before', v_profile.rating, 'rating_after', v_rating,
    'streak', v_streak, 'shards_earned', v_reward, 'outcomes', v_detail);
end $$;

-- ---------------------------------------------------------------------------------------------
-- Shop
-- ---------------------------------------------------------------------------------------------

-- Buys a figure from today's shelf (C# ShopCatalogue.Buy): p_item is the shelf item id
-- 'fig-{seed}-{size}'. Each player can own one copy of an item design; buying it again raises
-- 'already_owned', which also makes retries safe (a single debit). Legendary items are SOL-only.
create or replace function public.buy_figure_shards(p_item text) returns public.figures
language plpgsql security definer set search_path = public as $$
declare
  v_uid uuid := public._uid();
  v_shelf public.shop_shelf;
  v_figure public.figures;
begin
  select * into v_shelf from public.shop_shelf where day = public.daily_day() and item_id = p_item;
  if v_shelf.item_id is null then raise exception 'unknown_item'; end if;
  if v_shelf.price_shards is null then raise exception 'sol_only_item'; end if;

  perform public._lock_profile(v_uid);
  if exists (select 1 from public.figures where owner_id = v_uid and seed = v_shelf.seed and tier = v_shelf.tier and not monster) then
    raise exception 'already_owned';
  end if;

  insert into public.figures (seed, tier, encoding, rarity, name, owner_id, source)
  values (v_shelf.seed, v_shelf.tier, v_shelf.encoding, v_shelf.rarity, v_shelf.name, v_uid, 'shop')
  returning * into v_figure;
  perform public._add_shards(v_uid, -v_shelf.price_shards, 'figure_purchase', v_figure.id::text);
  return v_figure;
end $$;

-- ---------------------------------------------------------------------------------------------
-- Bosses
-- ---------------------------------------------------------------------------------------------

-- Opens (or returns the already open) attempt. Clients may only pay with shards; SOL entries are
-- recorded by the server route through enter_boss_sol (service role). The attempt's created_at is
-- the server start time used by finish_boss.
create or replace function public.enter_boss(p_boss_id uuid, p_pay_with text default 'shards') returns public.boss_attempts
language plpgsql security definer set search_path = public as $$
declare
  v_uid uuid := public._uid();
  v_boss public.boss_events;
  v_attempt public.boss_attempts;
begin
  if p_pay_with is distinct from 'shards' then raise exception 'sol_entry_requires_server'; end if;
  select * into v_boss from public.boss_events where id = p_boss_id;
  if v_boss.id is null then raise exception 'unknown_boss'; end if;
  if now() not between v_boss.starts_at and v_boss.ends_at then raise exception 'boss_not_active'; end if;
  perform public._require_keys('boss:' || v_boss.id, jsonb_array_length(v_boss.stages));

  perform public._lock_profile(v_uid);
  select * into v_attempt from public.boss_attempts where boss_id = p_boss_id and user_id = v_uid and result = 'open';
  if v_attempt.id is not null then return v_attempt; end if;

  insert into public.boss_attempts (boss_id, user_id, paid_with) values (p_boss_id, v_uid, 'shards') returning * into v_attempt;
  if v_boss.entry_shards > 0 then
    perform public._add_shards(v_uid, -v_boss.entry_shards, 'boss_entry', v_attempt.id::text);
  end if;
  return v_attempt;
end $$;

-- Service role only (sol-purchase route, after verifying the devnet transfer). The attempt and the
-- ledger row carrying the payment signature are written in this one transaction, so a paid signature
-- is either fully consumed (attempt + ledger) or not at all. Never silently reuses an open attempt:
-- raises attempt_open so the route can record the payment as refund_due instead of losing it.
-- The route pre-checks the same conditions before the player pays.
create or replace function public.enter_boss_sol(p_user uuid, p_boss_id uuid, p_signature text, p_lamports bigint)
returns public.boss_attempts
language plpgsql security definer set search_path = public as $$
declare v_boss public.boss_events; v_attempt public.boss_attempts;
begin
  select * into v_boss from public.boss_events where id = p_boss_id;
  if v_boss.id is null then raise exception 'unknown_boss'; end if;
  if now() not between v_boss.starts_at and v_boss.ends_at then raise exception 'boss_not_active'; end if;
  if p_lamports is null or p_lamports < v_boss.entry_lamports then raise exception 'underpaid'; end if;
  if p_signature is null or char_length(p_signature) not between 32 and 128 then raise exception 'invalid_signature'; end if;
  perform public._require_keys('boss:' || v_boss.id, jsonb_array_length(v_boss.stages));
  perform public._lock_profile(p_user);

  if exists (select 1 from public.transactions where signature = p_signature) then raise exception 'signature_used'; end if;
  select * into v_attempt from public.boss_attempts where boss_id = p_boss_id and user_id = p_user and result = 'open';
  if v_attempt.id is not null then raise exception 'attempt_open'; end if;
  insert into public.boss_attempts (boss_id, user_id, paid_with) values (p_boss_id, p_user, 'sol') returning * into v_attempt;
  begin
    perform public._ledger(p_user, 'boss_entry', 0, v_attempt.id::text, null, p_lamports, p_signature);
  exception when unique_violation then raise exception 'signature_used';
  end;
  return v_attempt;
end $$;

-- p_result = { "answers": [a, b, c], "moves": [m, m, m]?, "resets": [r, r, r]?, "elapsed_ms": 123456 }.
-- Win = every stage solved, client elapsed ≤ time limit and server elapsed (now − attempt start)
-- ≤ time limit + 30 s. The stored/ranked time is greatest(client elapsed, server elapsed − 5 s).
-- reward_shards is paid only for the player's FIRST win on a boss; later wins are practice
-- (recorded for the leaderboard, 0 shards). Finishing a finished attempt returns its stored result.
create or replace function public.finish_boss(p_attempt_id uuid, p_result jsonb) returns jsonb
language plpgsql security definer set search_path = public as $$
declare
  v_uid uuid := public._uid();
  v_attempt public.boss_attempts;
  v_boss public.boss_events;
  v_answers jsonb := p_result -> 'answers';
  v_elapsed int;
  v_server_ms bigint;
  v_ranked int;
  v_won boolean := true;
  v_first boolean;
  v_earned int := 0;
  v_stages int;
  v_detail jsonb := '[]'::jsonb;
  r record;
begin
  perform public._lock_profile(v_uid);
  select * into v_attempt from public.boss_attempts where id = p_attempt_id and user_id = v_uid for update;
  if v_attempt.id is null then raise exception 'unknown_attempt'; end if;
  select * into v_boss from public.boss_events where id = v_attempt.boss_id;
  if v_attempt.result <> 'open' then
    return jsonb_build_object('result', v_attempt.result, 'earned', 0, 'elapsed_ms', v_attempt.elapsed_ms, 'already_finished', true);
  end if;

  if jsonb_typeof(p_result -> 'elapsed_ms') <> 'number' or (p_result ->> 'elapsed_ms') !~ '^\d{1,7}$' then raise exception 'invalid_result'; end if;
  v_elapsed := (p_result ->> 'elapsed_ms')::int;
  v_stages := jsonb_array_length(v_boss.stages);
  if v_elapsed not between 1000 and 3600000 or v_answers is null
     or jsonb_typeof(v_answers) <> 'array' or jsonb_array_length(v_answers) <> v_stages then
    raise exception 'invalid_result';
  end if;

  for i in 0 .. v_stages - 1 loop
    select * into r from public._verify_stage('boss:' || v_boss.id, i, v_answers -> i);
    v_won := v_won and r.solved;
    v_detail := v_detail || jsonb_build_object('kind', r.kind, 'solved', r.solved);
  end loop;
  v_server_ms := (extract(epoch from (now() - v_attempt.created_at)) * 1000)::bigint;
  v_ranked := least(3600000, greatest(v_elapsed::bigint, v_server_ms - 5000))::int;
  v_won := v_won and v_elapsed <= v_boss.time_limit_ms and v_server_ms <= v_boss.time_limit_ms + 30000
           and now() <= v_boss.ends_at + make_interval(secs => v_boss.time_limit_ms / 1000.0);

  v_first := v_won and not exists (
    select 1 from public.boss_attempts where boss_id = v_boss.id and user_id = v_uid and result = 'won');
  update public.boss_attempts set result = case when v_won then 'won' else 'lost' end, elapsed_ms = v_ranked,
    detail = v_detail || jsonb_build_array(jsonb_build_object('client_elapsed_ms', v_elapsed, 'server_elapsed_ms', v_server_ms)),
    finished_at = now()
   where id = v_attempt.id;
  if v_first then
    v_earned := v_boss.reward_shards;
    perform public._add_shards(v_uid, v_earned, 'boss_win', v_attempt.id::text, 'boss:' || v_boss.id);
  end if;
  return jsonb_build_object('result', case when v_won then 'won' else 'lost' end, 'earned', v_earned,
    'first_win', v_first, 'elapsed_ms', v_ranked, 'stages', v_detail);
end $$;

-- ---------------------------------------------------------------------------------------------
-- Marketplace (off-chain shard listings; SOL listings are settled by the server route)
-- Anti-Sybil: listing and buying require non-trivial progress (_has_progress), and a seller can
-- receive at most 2000 shards from sales per UTC day (MARKET_DAILY_INFLOW_CAP).
-- ---------------------------------------------------------------------------------------------

create or replace function public.list_figure(p_figure_id uuid, p_price_shards int default null,
  p_price_lamports bigint default null) returns public.listings
language plpgsql security definer set search_path = public as $$
declare v_uid uuid := public._uid(); v_figure public.figures; v_listing public.listings;
begin
  if (p_price_shards is null) = (p_price_lamports is null) then raise exception 'exactly_one_price'; end if;
  if p_price_shards is not null and p_price_shards not between 1 and 1000000 then raise exception 'invalid_price'; end if;
  if p_price_lamports is not null and p_price_lamports <= 0 then raise exception 'invalid_price'; end if;

  perform public._lock_profile(v_uid);
  if not public._has_progress(v_uid) then raise exception 'progress_required'; end if;
  select * into v_figure from public.figures where id = p_figure_id for update;
  if v_figure.id is null or v_figure.owner_id is distinct from v_uid then raise exception 'figure_not_owned'; end if;
  if p_price_lamports is not null and v_figure.mint_address is null then raise exception 'mint_required_for_sol_listing'; end if;
  if p_price_shards is not null and v_figure.mint_address is not null then raise exception 'minted_figures_list_for_sol'; end if;
  if exists (select 1 from public.profiles where id = v_uid and avatar_figure_id = p_figure_id) then raise exception 'figure_is_avatar'; end if;

  begin
    insert into public.listings (figure_id, seller_id, price_shards, price_lamports)
    values (p_figure_id, v_uid, p_price_shards, p_price_lamports) returning * into v_listing;
  exception when unique_violation then raise exception 'already_listed';
  end;
  return v_listing;
end $$;

create or replace function public.cancel_listing(p_listing_id uuid) returns public.listings
language plpgsql security definer set search_path = public as $$
declare v_uid uuid := public._uid(); v_listing public.listings;
begin
  update public.listings set status = 'cancelled', closed_at = now()
   where id = p_listing_id and seller_id = v_uid and status = 'active'
  returning * into v_listing;
  if v_listing.id is null then raise exception 'listing_not_active'; end if;
  return v_listing;
end $$;

-- Atomic: debits the buyer, credits the seller and transfers ownership in one transaction.
create or replace function public.buy_listing(p_listing_id uuid) returns jsonb
language plpgsql security definer set search_path = public as $$
declare
  v_uid uuid := public._uid();
  v_listing public.listings;
  v_cap constant int := 2000; -- MARKET_DAILY_INFLOW_CAP
  v_today_inflow int;
begin
  select * into v_listing from public.listings where id = p_listing_id for update;
  if v_listing.id is null or v_listing.status <> 'active' then raise exception 'listing_not_active'; end if;
  if v_listing.seller_id = v_uid then raise exception 'own_listing'; end if;
  if v_listing.price_shards is null then raise exception 'sol_listing_requires_server'; end if;

  -- Lock both profiles in a fixed order to avoid deadlocks between crossing purchases.
  perform 1 from public.profiles where id in (v_uid, v_listing.seller_id) order by id for update;
  if not public._has_progress(v_uid) then raise exception 'progress_required'; end if;
  select coalesce(sum(shards_delta), 0) into v_today_inflow from public.transactions
   where user_id = v_listing.seller_id and kind = 'listing_sale'
     and created_at >= (date_trunc('day', now() at time zone 'utc') at time zone 'utc');
  if v_today_inflow + v_listing.price_shards > v_cap then raise exception 'seller_daily_cap'; end if;

  perform public._add_shards(v_uid, -v_listing.price_shards, 'listing_purchase', v_listing.id::text);
  perform public._add_shards(v_listing.seller_id, v_listing.price_shards, 'listing_sale', v_listing.id::text);
  update public.figures set owner_id = v_uid where id = v_listing.figure_id and owner_id = v_listing.seller_id;
  if not found then raise exception 'figure_moved'; end if;
  update public.profiles set avatar_figure_id = null where id = v_listing.seller_id and avatar_figure_id = v_listing.figure_id;
  update public.listings set status = 'sold', buyer_id = v_uid, closed_at = now() where id = v_listing.id;
  return jsonb_build_object('listing_id', v_listing.id, 'figure_id', v_listing.figure_id, 'price_shards', v_listing.price_shards);
end $$;

-- ---------------------------------------------------------------------------------------------
-- Friends
-- ---------------------------------------------------------------------------------------------

-- Sends a request by handle. If the other player already asked us (including a request crossing
-- ours concurrently), the friendship is accepted. Returns { status, user_id }. Unknown handles and
-- players who blocked us both yield 'not_found'.
create or replace function public.send_friend_request(p_handle text) returns jsonb
language plpgsql security definer set search_path = public as $$
declare v_uid uuid := public._uid(); v_other uuid; v_row public.friendships;
begin
  select id into v_other from public.profiles where handle operator(extensions.=) p_handle::extensions.citext;  -- citext ops live in extensions
  if v_other is null then raise exception 'not_found'; end if;
  if v_other = v_uid then raise exception 'cannot_friend_self'; end if;

  select * into v_row from public.friendships
   where (requester_id = v_uid and addressee_id = v_other) or (requester_id = v_other and addressee_id = v_uid) for update;
  if v_row.requester_id is null then
    begin
      insert into public.friendships (requester_id, addressee_id) values (v_uid, v_other);
      return jsonb_build_object('status', 'pending', 'user_id', v_other);
    exception when unique_violation then
      -- Crossing request committed in between: re-read and fall through.
      select * into v_row from public.friendships
       where (requester_id = v_uid and addressee_id = v_other) or (requester_id = v_other and addressee_id = v_uid) for update;
    end;
  end if;
  if v_row.status = 'blocked' then
    if v_row.requester_id = v_other then raise exception 'not_found'; end if;
    return jsonb_build_object('status', 'blocked', 'user_id', v_other);
  end if;
  if v_row.status = 'pending' and v_row.requester_id = v_other then
    update public.friendships set status = 'accepted' where requester_id = v_other and addressee_id = v_uid;
    return jsonb_build_object('status', 'accepted', 'user_id', v_other);
  end if;
  return jsonb_build_object('status', v_row.status, 'user_id', v_other);
end $$;

-- p_action: 'accept' | 'decline' | 'block'. Only the addressee of a pending request may respond.
create or replace function public.respond_friend_request(p_requester_id uuid, p_action text) returns jsonb
language plpgsql security definer set search_path = public as $$
declare v_uid uuid := public._uid();
begin
  if p_action is null or p_action not in ('accept', 'decline', 'block') then raise exception 'invalid_action'; end if;
  perform 1 from public.friendships where requester_id = p_requester_id and addressee_id = v_uid and status = 'pending' for update;
  if not found then raise exception 'no_pending_request'; end if;
  if p_action = 'decline' then
    delete from public.friendships where requester_id = p_requester_id and addressee_id = v_uid;
    return jsonb_build_object('status', 'declined');
  end if;
  -- A block is stored with the blocker as requester so send_friend_request can tell who blocked whom.
  if p_action = 'block' then
    delete from public.friendships where requester_id = p_requester_id and addressee_id = v_uid;
    insert into public.friendships (requester_id, addressee_id, status) values (v_uid, p_requester_id, 'blocked');
    return jsonb_build_object('status', 'blocked');
  end if;
  update public.friendships set status = 'accepted' where requester_id = p_requester_id and addressee_id = v_uid;
  return jsonb_build_object('status', 'accepted');
end $$;

create or replace function public.remove_friend(p_other uuid) returns void
language plpgsql security definer set search_path = public as $$
declare v_uid uuid := public._uid();
begin
  delete from public.friendships
   where status <> 'blocked'
     and ((requester_id = v_uid and addressee_id = p_other) or (requester_id = p_other and addressee_id = v_uid));
end $$;

-- Caller's friends and requests with public profile fields. direction: 'outgoing' | 'incoming'.
create or replace function public.list_friends()
returns table (user_id uuid, handle text, display_name text, avatar_figure_id uuid, rating int, status text, direction text)
language sql stable security definer set search_path = public as $$
  select p.id, p.handle::text, p.display_name, p.avatar_figure_id, p.rating, f.status,
         case when f.requester_id = auth.uid() then 'outgoing' else 'incoming' end
    from public.friendships f
    join public.profiles p on p.id = case when f.requester_id = auth.uid() then f.addressee_id else f.requester_id end
   where auth.uid() in (f.requester_id, f.addressee_id)
     and not (f.status = 'blocked' and f.addressee_id = auth.uid())
   order by f.status, p.handle
$$;

-- ---------------------------------------------------------------------------------------------
-- Leaderboards
-- ---------------------------------------------------------------------------------------------

-- p_scope: 'global' (rating), 'daily' (points on day p_ref, default today), 'friends' (rating, caller
-- + accepted friends), 'boss' (fastest ranked win per player for boss p_ref, default the latest
-- active boss). Ties share a rank (SQL rank()). A malformed p_ref raises 'invalid_ref'.
create or replace function public.leaderboard(p_scope text, p_limit int default 50, p_ref text default null)
returns table (rank int, user_id uuid, handle text, display_name text, avatar_figure_id uuid, score int, elapsed_ms int)
language plpgsql stable security definer set search_path = public as $$
declare v_limit int := least(greatest(coalesce(p_limit, 50), 1), 200); v_day int; v_boss uuid;
begin
  begin
    if p_scope = 'daily' then v_day := coalesce(nullif(p_ref, '')::int, public.daily_day()); end if;
    if p_scope = 'boss' then v_boss := nullif(p_ref, '')::uuid; end if;
  exception when invalid_text_representation or numeric_value_out_of_range then raise exception 'invalid_ref';
  end;

  if p_scope = 'global' then
    return query
      select (rank() over (order by p.rating desc))::int, p.id, p.handle::text, p.display_name,
             p.avatar_figure_id, p.rating, null::int
        from public.profiles p order by p.rating desc, p.created_at limit v_limit;
  elsif p_scope = 'daily' then
    return query
      select (rank() over (order by d.points desc, d.elapsed_ms))::int, p.id, p.handle::text,
             p.display_name, p.avatar_figure_id, d.points, d.elapsed_ms
        from public.daily_results d join public.profiles p on p.id = d.user_id
       where d.day = v_day order by d.points desc, d.elapsed_ms, d.created_at limit v_limit;
  elsif p_scope = 'friends' then
    if auth.uid() is null then raise exception 'not_authenticated'; end if;
    return query
      select (rank() over (order by p.rating desc))::int, p.id, p.handle::text, p.display_name,
             p.avatar_figure_id, p.rating, null::int
        from public.profiles p
       where p.id = auth.uid() or p.id in (
         select case when f.requester_id = auth.uid() then f.addressee_id else f.requester_id end
           from public.friendships f where f.status = 'accepted' and auth.uid() in (f.requester_id, f.addressee_id))
       order by p.rating desc, p.created_at limit v_limit;
  elsif p_scope = 'boss' then
    v_boss := coalesce(v_boss,
      (select b.id from public.boss_events b where now() between b.starts_at and b.ends_at order by b.starts_at desc limit 1));
    return query
      with best as (
        select distinct on (a.user_id) a.user_id, a.elapsed_ms, a.finished_at
          from public.boss_attempts a where a.boss_id = v_boss and a.result = 'won'
         order by a.user_id, a.elapsed_ms, a.finished_at)
      select (rank() over (order by b.elapsed_ms))::int, p.id, p.handle::text, p.display_name,
             p.avatar_figure_id, null::int, b.elapsed_ms
        from best b join public.profiles p on p.id = b.user_id
       order by b.elapsed_ms, b.finished_at limit v_limit;
  else
    raise exception 'invalid_scope';
  end if;
end $$;

-- ---------------------------------------------------------------------------------------------
-- Execute grants: nothing by default (see default privileges in the schema migration), then the
-- client API. Keep this list in sync with the allowlist in supabase/tests/security.test.sql.
-- ---------------------------------------------------------------------------------------------

revoke all on all functions in schema public from public, anon, authenticated;

grant execute on function public.daily_day(timestamptz) to anon, authenticated;
grant execute on function public.leaderboard(text, int, text) to anon, authenticated;
grant execute on function
  public.ensure_profile(),
  public.update_profile(text, text, uuid),
  public.complete_level(int, int, int, int, jsonb),
  public.submit_daily(int, jsonb),
  public.buy_figure_shards(text),
  public.enter_boss(uuid, text),
  public.finish_boss(uuid, jsonb),
  public.list_figure(uuid, int, bigint),
  public.cancel_listing(uuid),
  public.buy_listing(uuid),
  public.send_friend_request(text),
  public.respond_friend_request(uuid, text),
  public.remove_friend(uuid),
  public.list_friends()
to authenticated;
-- enter_boss_sol and the _helpers stay service-role only.
