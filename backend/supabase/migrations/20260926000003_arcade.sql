-- v3 arcade adventure levels (docs/PLAN_V3.md). Map levels now play as Battle / Rune Hand / Ice Dash /
-- Beat Crawl / Boss (client Domain/Arcade/LevelModes.cs). The classic trials (and complete_level's
-- server-side replay) remain for Daily and boss raids.
--
-- Trust model for arcade results: the client sends mode, stars, elapsed time and a compact input
-- replay ("proof"). The server checks mode against the fixed layout, unlock order, plausibility
-- floors on time and proof shape, then stores the proof with the record. Rewards are first-clear +
-- new-star only, so a forged result can at most unlock the map early (bounded: 60 levels). Full
-- replay verification needs the arcade rules in packages/core; until then proofs are kept for audit.

alter table public.level_progress add column if not exists mode text;
alter table public.level_progress add column if not exists proof text check (proof is null or length(proof) <= 8000);

create or replace function public._arcade_mode(p_level int) returns text
language sql immutable as $$
  select (array['battle','battle','dash','cards','battle','crawl','cards','dash','battle','crawl','cards','boss'])[p_level + 1]
$$;

create or replace function public.complete_arcade_level(p_world int, p_level int, p_mode text, p_stars int,
  p_elapsed_ms int, p_proof text) returns jsonb
language plpgsql security definer set search_path = public as $$
declare
  v_uid uuid := public._uid();
  v_mode text := public._arcade_mode(p_level);
  v_min_ms int;
  v_prefix text;
  v_prev public.level_progress;
  v_earned int;
  v_boss boolean := p_level = 11;
  v_source text;
begin
  if p_world is null or p_world not between 0 and 4 or p_level is null or p_level not between 0 and 11 then raise exception 'invalid_level'; end if;
  if lower(coalesce(p_mode, '')) <> v_mode then raise exception 'wrong_mode'; end if;
  if p_stars is null or p_stars not between 1 and 3 then raise exception 'invalid_stars'; end if;
  if p_elapsed_ms is null or p_elapsed_ms > 3600000 then raise exception 'invalid_elapsed'; end if;
  if p_proof is null or length(p_proof) not between 4 and 8000 then raise exception 'invalid_proof'; end if;

  -- Plausibility floors: nobody beats a monster, a room or a hand this fast.
  v_min_ms := case v_mode when 'battle' then 6000 when 'cards' then 8000 when 'dash' then 2500 when 'crawl' then 6000 else 20000 end;
  if p_elapsed_ms < v_min_ms then raise exception 'implausible_time'; end if;
  v_prefix := case v_mode when 'battle' then 'B1:' when 'cards' then 'C1:' when 'dash' then 'D1:' when 'crawl' then 'R1:' else 'B1:' end;
  if left(p_proof, 3) <> v_prefix then raise exception 'invalid_proof'; end if;
  if v_boss and position('||C1:' in p_proof) = 0 then raise exception 'invalid_proof'; end if;

  perform public._lock_profile(v_uid);

  if not (p_level = 0 and p_world = 0) and not exists (
      select 1 from public.level_progress
       where user_id = v_uid
         and ((p_level = 0 and world = p_world - 1 and level = 11) or (p_level > 0 and world = p_world and level = p_level - 1))) then
    raise exception 'level_locked';
  end if;

  v_source := format('level:%s:%s', p_world, p_level);
  select * into v_prev from public.level_progress where user_id = v_uid and world = p_world and level = p_level;
  if v_prev.user_id is null then
    insert into public.level_progress (user_id, world, level, stars, best_ms, mode, proof)
      values (v_uid, p_world, p_level, p_stars, greatest(1, p_elapsed_ms), v_mode, p_proof);
    v_earned := (case when v_boss then 300 else 20 end) + (p_stars - 1) * 10;
  else
    v_earned := greatest(0, p_stars - v_prev.stars) * 10;
    update public.level_progress
       set stars = greatest(stars, p_stars), best_ms = least(best_ms, greatest(1, p_elapsed_ms)), completed_at = now(),
           mode = v_mode, proof = case when p_stars >= v_prev.stars then p_proof else proof end
     where user_id = v_uid and world = p_world and level = p_level;
  end if;
  if v_earned > 0 then perform public._add_shards(v_uid, v_earned, 'level_clear', v_source); end if;

  return jsonb_build_object('world', p_world, 'level', p_level, 'mode', v_mode,
    'stars', greatest(p_stars, coalesce(v_prev.stars, 0)), 'run_stars', p_stars,
    'best_ms', least(greatest(1, p_elapsed_ms), coalesce(v_prev.best_ms, p_elapsed_ms)),
    'earned', v_earned, 'first_clear', v_prev.user_id is null);
end $$;

revoke all on function public.complete_arcade_level(int, int, text, int, int, text) from public, anon;
revoke all on function public._arcade_mode(int) from public, anon, authenticated;
grant execute on function public.complete_arcade_level(int, int, text, int, int, text) to authenticated;
