-- Local seed data. Deterministic content that needs the generators (daily shelf, puzzle keys,
-- weekly boss roster, starter figure pool) is written afterwards by `pnpm --filter @ronriku/backend publish`.

-- Price catalogue (PLAN §6). 1 SOL = 1_000_000_000 lamports.
insert into public.shop_items (id, kind, tier, rarity, price_shards, price_lamports, sort) values
  ('figure-common',    'figure',     null, 'Common',     300,  null,        1),
  ('figure-rare',      'figure',     null, 'Rare',       800,  null,        2),
  ('figure-epic',      'figure',     null, 'Epic',       2000, null,        3),
  ('figure-legendary', 'figure',     null, 'Legendary',  null, 100000000,   4),
  ('boss-entry',       'boss_entry', null, null,         150,  10000000,    5)
on conflict (id) do nothing;

-- One always-active launch boss. Seed 20260924; stages = Mix(seed, 201..203) (BossEvent.Stages),
-- monster = generateFigure(20260924, 5, monster: true) = "NIYORO".
insert into public.boss_events (id, code, tier, name, palette, seed, stages, entry_shards, entry_lamports, reward_shards,
  time_limit_ms, starts_at, ends_at) values
  ('00000000-0000-4000-8000-00000000b055', null, 0, 'NIYORO', 'boss', 20260924,
   '[{"kind":"Pattern","seed":"-2145675535596569384"},{"kind":"Spatial","seed":"5217370603676041262"},{"kind":"Logic","seed":"-7998607936530580287"}]',
   150, 10000000, 450, 300000, now() - interval '1 day', now() + interval '365 days')
on conflict (id) do nothing;
