# RONRIKU v3 — Arcade adventure

Why: playtesting (a first-time adult player) showed the adventure was three slow abstract puzzles on
repeat, the Pattern trial was not self-explanatory, and players got bored after world 1. v3 turns the
map into a run of fast, juicy, hero-driven games with music, keeping the classic trials for the Daily
and boss raids.

## Level layout (every world, `Domain/Arcade/LevelModes.cs`)

| # | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| mode | Battle | Battle | Ice Dash | Rune Hand | Battle | Beat Crawl | Rune Hand | Ice Dash | Battle | Beat Crawl | Rune Hand | **Boss** |

Tier 0 (gentler) for world 1 levels 1–6, tier 1 everywhere else — difficulty stays in the middle band
instead of ramping per world. Everything derives from `LevelDef.Seed`, so any level can be replayed
exactly from `(world, index)`; records store only stars, best time and a compact input proof.

## Modes

**Battle** — hero vs monster. A stream of 3–10 s micro-challenges; each right answer is an attack worth
`(10 + speed bonus ≤ 10) chips × (1 + combo, max ×6) mult`. The monster swings on a timer (hits push it
back); wrong answers and swings cost hearts. Stars = hearts kept. Micro-challenges (one-line prompts,
tap answers): **Next** (sequence of tokens), **Odd** (one mirrored shape), **Sum** (tap tiles to hit a
number), **Memory** (lights flash), **Mirror** (complete the symmetric picture), **Arrows** (follow the
arrows), **Scales** (weights). World 1 teaches four, each later world adds one.

**Rune Hand** (Balatro-like) — 36 runes (1–9 × Fire/Wave/Leaf/Bolt), hand of 7, 4 hands, 3 discards.
Play 1–5 runes as a combo (pair … sign ladder), score `(combo chips + rune values) × mult`, beat the
monster's HP. Before the fight pick 1 of 3 **charms** (jokers) that bend scoring. Preview shows combo
and chips×mult before committing; HINT selects the best play. Balance target: a greedy bot with a
random charm wins ~68 % (flat across worlds) — `ArcadeTests.Cards_Balance…` and the offline sim.

**Ice Dash** (hero) — swipe; the hero slides until a wall. Grab all gems, then the door opens; spikes
reset. BFS-generated, par = optimal moves; ★★★ at par.

**Beat Crawl** (hero, rhythm) — monsters act on the music beat (every 2nd beat of the 140 BPM HeroRun
track); one hero move per beat, on-beat moves build combo, bump to attack, clear the room, take the
stairs. Monsters telegraph their next step (slime hops, bat flies straight, skeleton chases).

**Boss** — phase 1 battle (big HP), phase 2 rune hand with two charms. Stars = weaker phase.

## Juice & music

`Presentation/Arcade/ArcadeKit.cs`: shake, punch, pop-up numbers, flashes, banners, count-ups, hero/
monster lunges, chips×mult boxes. `Presentation/Audio`: procedural chiptune (9 tracks, 3 intensity
layers, stingers, sample-accurate beat clock). Combos raise intensity; big hits fire stingers.

## Tuning without code

`Assets/Ronriku/Resources/RonrikuTuning.asset` (linked from the RONRIKU object in the Bootstrap scene):
shake, pop-up size, SFX volume, battle stagger / pauses, crawl beats-per-move and timing window.

## Server

`complete_arcade_level(world, level, mode, stars, elapsed_ms, proof)` (migration
`20260926000003_arcade.sql`): mode must match the layout, unlock order enforced, time floors per mode,
proof shape checked and stored, first clear + new stars pay (same economy as v2). Full replay
verification needs the arcade rules ported to `packages/core` — tracked in STATE open items.

## Pattern trial (Daily)

Examples now act out their rule (rotate / flip / slide / invert animation) until the input becomes the
output; after answering, the yellow grid performs the change into the right answer.
