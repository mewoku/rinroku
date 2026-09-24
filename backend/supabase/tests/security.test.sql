-- Structural security invariants. Run: npx supabase test db
begin;
create extension if not exists pgtap with schema extensions;
select plan(8);

select is(
  (select count(*)::int from pg_tables where schemaname = 'public' and not rowsecurity),
  0, 'every public table has RLS enabled');

select is(
  (select count(*)::int from pg_proc p join pg_namespace n on n.oid = p.pronamespace
    where n.nspname = 'public' and p.prosecdef
      and not coalesce(p.proconfig @> array['search_path=public'], false)),
  0, 'every security definer function pins search_path = public');

select is(
  (select count(*)::int from information_schema.role_table_grants
    where table_schema = 'public' and grantee in ('anon', 'authenticated')
      and privilege_type in ('INSERT', 'UPDATE', 'DELETE', 'TRUNCATE')),
  0, 'clients have no direct write grants on public tables');

select is(
  (select count(*)::int from information_schema.role_table_grants
    where table_schema = 'public' and grantee in ('anon', 'authenticated')
      and table_name in ('puzzle_keys', 'figure_pool', 'wallet_nonces')),
  0, 'server-only tables are not granted to clients');

-- Generic: nothing in public is executable by clients except this explicit allowlist
-- (keep in sync with the grants at the end of 20260924000002_rpc.sql).
select is(
  (select array_agg(p.proname::text order by p.proname) from pg_proc p join pg_namespace n on n.oid = p.pronamespace
    where n.nspname = 'public'
      and (has_function_privilege('anon', p.oid, 'execute') or has_function_privilege('authenticated', p.oid, 'execute'))
      and p.proname not in ('daily_day', 'leaderboard', 'ensure_profile', 'update_profile', 'complete_level',
        'submit_daily', 'buy_figure_shards', 'enter_boss', 'finish_boss', 'list_figure', 'cancel_listing',
        'buy_listing', 'send_friend_request', 'respond_friend_request', 'remove_friend', 'list_friends')),
  null::text[], 'only allowlisted RPCs are executable by anon/authenticated');

select is(
  (select array_agg(p.proname::text order by p.proname) from pg_proc p join pg_namespace n on n.oid = p.pronamespace
    where n.nspname = 'public' and has_function_privilege('anon', p.oid, 'execute')
      and p.proname not in ('daily_day', 'leaderboard')),
  null::text[], 'anon may only call daily_day and leaderboard');

-- Default privileges: objects created by future migrations are closed to clients.
create function public._tap_probe() returns int language sql as 'select 1';
create table public._tap_probe_table (id int);
select ok(
  not has_function_privilege('anon', 'public._tap_probe()', 'execute')
  and not has_function_privilege('authenticated', 'public._tap_probe()', 'execute'),
  'new functions are not executable by clients by default');
select ok(
  not has_table_privilege('anon', 'public._tap_probe_table', 'select')
  and not has_table_privilege('authenticated', 'public._tap_probe_table', 'insert'),
  'new tables are not accessible to clients by default');

select * from finish();
rollback;
