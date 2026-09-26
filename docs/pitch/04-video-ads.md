# RONRIKU — Video Ad Plan

Three concepts built on the hook from `03-ideal-pitch.md`: **Solve to strike.** A right answer *hits*: chips × mult, a combo, a shake. That moment is the first frame of every ad.

Production: the Remotion project in `marketing/video/` (compositions `Tiktok15`, `Trailer30`, `Bumper6`, all 1080×1920 at 30 fps). Each shot plays `public/clips/<id>.mp4` when it exists. Until the clip is recorded it falls back to the matching screenshot from `docs/evidence/` with a slow Ken Burns push, so every ad already renders with zero footage.

Music is the game's own procedural soundtrack, exported as WAVs by Unity **RONRIKU → Export Music WAVs** into `docs/evidence/audio/` (git-ignored). The prepare script copies it in. If the WAVs are missing, the ads render silent.

Frame rules (video-craft preflight): one focal point per shot; at most 8 words of on-screen text; text inside the vertical safe zone (150 px top, 170 px bottom, plus about 120 px on the right for TikTok's buttons); headlines ≥ 72 px in Silkscreen; end card ≥ 3 s with one action. Effect families: **kinetic type + screen shake**, matching the game's own juice. Nothing else.

---

## Footage to record (shot list for the engineer)

Record on the phone (Seeker or Pixel 6a). Portrait, 1080×2400, **60 fps if the device allows, else 30**, bit rate ≥ 12 Mbps, with the notification shade clean and Do Not Disturb on.

```bash
adb shell settings put global sysui_demo_allowed 1          # clean status bar (optional)
adb shell screenrecord --size 1080x2400 --bit-rate 12000000 --time-limit 12 /sdcard/c01.mp4
adb pull /sdcard/c01.mp4 marketing/video/public/clips/battle-combo.mp4
```

`screenrecord` does not capture app audio, which is fine because the ads lay the soundtrack in post. The alternative is Unity Editor Game view at 1080×2400 with the Unity Recorder (MP4, 30 fps). Set **Reduced motion OFF** and **Music ON**, and raise shake/pop-up size slightly in `RonrikuTuning.asset` if it reads small on video.

Save each clip as `marketing/video/public/clips/<id>.mp4`. Trim to the action. Each clip should be **at least** the "min length" below; the ads use the first N seconds (set `startFrom` in `src/shots.ts` to skip a lead-in).

| id | Where | What must happen on screen | Min length | Fallback still |
|---|---|---|---|---|
| `battle-combo` | W2 · 5 Battle | 3–4 right answers in a row, fast. The combo counter climbs to ×3 or more, the chips × mult box pops, damage numbers fly, the monster staggers. **Hero shot of the campaign.** | 6 s | arcade-2-battle-hit |
| `battle-ko` | Any Battle, last hit | The final right answer, monster HP to 0, the KO/victory banner, stars count up | 4 s | arcade-1-battle |
| `wrong-answer` | Any Battle | One wrong tap: the monster swings, a heart is lost, the screen shakes | 2 s | arcade-1-battle |
| `charm-pick` | W1 · 4 Rune Hand | The PICK A CHARM screen, a finger taps EMBER HEART | 2 s | arcade-3-charm-pick |
| `rune-play` | W1 · 4 Rune Hand | Select a two pair / straight, the preview shows chips × mult, tap PLAY, runes score one by one, the total counts up, the monster takes the hit | 5 s | arcade-5-rune-scoring |
| `ice-dash` | W1 · 3 Ice Dash | 4–6 swipes solving at par: gems collected, the door opens, ★★★ | 5 s | arcade-6-ice-dash |
| `beat-crawl` | W3 · 10 Beat Crawl | Moves exactly on the beat, bump-kill a slime, the combo rises, the beat bar pulses | 5 s | arcade-7-beat-crawl |
| `map-walk` | PLAY map, W1 | The hero walks from node 1 to node 2, then switch the world tab LAB → EMBER | 4 s | arcade-0-map |
| `boss` | W1 · 12 Boss | The phase-1 battle hit that ends phase 1, the transition into the phase-2 rune hand | 5 s | seeker-20x9-0-bosses |
| `daily` | DAILY | Tap BEGIN, a Pattern example animates, tap the right answer, cut to results: the rating counts up 1200 → 1226 and the STREAK label shows | 6 s | seeker-20x9-1-home |
| `shop` | SHOP | Slow scroll of the voxel figure shelf; tap a figure | 4 s | seeker-20x9-0-shop |
| `web-play` *(optional)* | Browser, `/play` at 1440 wide | The same battle in the browser, for the "play anywhere" beat | 3 s | web/play-1440 |

Tip: do the recordings in the same session as the pre-launch device pass (roast fix #5). A seeded level replays identically, so a botched take is a 30-second retry.

---

## Concept A: "Solve to strike" (15 s vertical, TikTok / Shorts / Reels)

**Goal**: stop the scroll in under 1 s, show 4 modes, and end on "free on Seeker". **Framework**: Hook → Proof montage → CTA. **Music**: `music_herorun_i2` (the 140 BPM HeroRun track, top intensity layer). Every cut lands on a bar: 4 beats = 1.714 s ≈ 51 frames. **Composition**: `Tiktok15` (450 frames).

| Time | Shot (clip) | On-screen text | Audio / motion |
|---|---|---|---|
| 0.0–1.7 | `battle-combo`, **full-bleed**, starting mid-combo on a big hit | **YOUR BRAIN IS THE WEAPON** (slams in on frame 3) | Track starts on the downbeat, screen shake on the hit, `stinger_combo` |
| 1.7–3.4 | `battle-combo` (continues, later hit) | **ANSWER FAST → COMBO ×6** | Punch-zoom 1.0 → 1.08 on each cut |
| 3.4–5.1 | `rune-play` | **CARDS? NO. PUZZLES.** | Hard cut on the beat |
| 5.1–6.9 | `ice-dash` | **SLIDE.** | — |
| 6.9–8.6 | `beat-crawl` | **MOVE ON THE BEAT.** | The beat bar in the clip is synced to the music |
| 8.6–9.9 | `battle-ko` | **K.O.** | `stinger_victory` |
| 9.9–11.6 | `daily` | **ONE DAILY. ONE RATING.** | Rating count-up |
| 11.6–15.0 | End card: logo, voxel hero, CTA | **RONRIKU** / **SOLVE TO STRIKE** / **FREE ON SEEKER · dApp Store** | Music resolves, 0.5 s fade |

Post copy: "your brain is the weapon. free on the Solana dApp Store (Seeker) + in your browser #puzzle #roguelite #solana"

## Concept B: "Five worlds. One mind." (30 s, dApp Store / Seeker trailer)

**Goal**: the store listing video. It shows the breadth, looks premium and is honest about crypto. The phone is framed (device mockup) on a dark ambient background, like the in-game dithered blobs. **Framework**: Problem-light → Demo → Proof → CTA. **Music**: `music_world1_i1` for 0–15 s, `music_boss_i2` for 15–23 s, `music_menu_i0` under the end card. **Composition**: `Trailer30` (900 frames).

| Time | Shot (clip) | On-screen text | Audio / motion |
|---|---|---|---|
| 0–3 | `map-walk` in the phone frame; world palettes cycle behind | **5 WORLDS. 60 GUARDIANS.** | The phone rises in with a spring |
| 3–6 | `battle-combo` | **EVERY ANSWER IS AN ATTACK** | `stinger_combo` on the hit |
| 6–7.5 | `charm-pick` | **PICK A CHARM** | — |
| 7.5–10.5 | `rune-play` | **CHIPS × MULT** | Count-up |
| 10.5–13 | `ice-dash` | **SLIDE TO PAR** | — |
| 13–15.5 | `beat-crawl` | **FIGHT ON THE BEAT** | — |
| 15.5–19.5 | `boss` | **THEN THE BOSS.** | Switch to the boss track, red ambient |
| 19.5–23 | `daily` | **ONE DAILY. A REAL RATING.** | Blue ambient |
| 23–26 | `shop` | **COLLECT VOXEL HEROES** / small: *collectibles on Solana (devnet in early access)* | — |
| 26–30 | End card | **RONRIKU** / **SOLVE TO STRIKE** / **FREE ON THE SOLANA dApp STORE** | Menu track, fade out |

The honesty line on the shop shot is deliberate (see the roast). Drop "(devnet in early access)" once mints are on mainnet.

## Concept C: "Hit." (6 s bumper), as built

The first draft overlaid a "20 × 6" box on a hit that the game scores 20 × 1. It read as fake, so it was cut. The bumper now uses the real numbers from the footage.

| Time | Shot | On-screen text | Audio |
|---|---|---|---|
| 0.0–1.4 | `battle`: the tap-3-that-make-16 answer lands for -20 | **SOLVE.** | `stinger_combo` on the hit, quiet HeroRun bed |
| 1.4–3.0 | `battle`: combo 4, 25 × 4 = **-100**, K.O. and VICTORY | **STRIKE.** | `stinger_victory` |
| 3.0–6.0 | End card | **RONRIKU · SOLVE TO STRIKE** / **ON SEEKER** | Bed fades out |

### Original bumper draft (superseded)

**Goal**: an unskippable pre-roll / X autoplay that makes one impression: puzzle = hit. **Composition**: `Bumper6` (180 frames), vertical. For a 16:9 bumper, duplicate the composition at 1920×1080 with the phone framed centre.

| Time | Shot | On-screen text | Audio |
|---|---|---|---|
| 0.0–2.0 | `battle-combo`, full-bleed, the biggest hit | **20 × 6** (the chips × mult box slams in) | `stinger_combo` |
| 2.0–3.5 | `battle-ko` | **K.O.** | `stinger_victory` |
| 3.5–6.0 | End card | **RONRIKU · SOLVE TO STRIKE** / **ON SEEKER** | Tail of the stinger |

---

## As built (2026-09-26)

- **Footage**: the engineer's recordings `public/clips/{map,battle,cards,dash,crawl,daily}.mp4` (1080×2400, 30 fps). Shot ids map to them via `raw:` in `src/shots.ts`, with in-clip timestamps measured frame by frame: the battle -20 hit at 5.55 s, the -40 at 11 s, the KO at 14.73 s; cards ON FIRE -348 at 7.3 s; dash solved at 2.25 s. Not recorded yet: `boss` and `shop`, which use Ken Burns stills. There is no charm-pick screen in `cards.mp4`, so the trailer's "PICK A CHARM" became **PLAY A HAND** over the two-pair hit.
- **Music**: 15 s = `music_herorun_i2` (140 BPM, cuts on bars). Trailer = `music_world1_i2` (124 BPM NuDisco hype layer, cuts on its bars) → `music_boss_i2` (raids) → `music_daily_i2` (Daily → end card). Bumper = stingers over a quiet HeroRun bed.
- **Copy changes vs the tables above**: "CARDS? NO. PUZZLES." → **CHIPS X MULT** (Silkscreen has no "×" glyph, so it's a plain X). Trailer "THEN THE BOSS." → **WEEKLY BOSS RAIDS**, because the only boss footage is the raids list.
- **Output**: `marketing/video/out/tiktok15.mp4` (15.0 s), `trailer30.mp4` (30.0 s), `bumper6.mp4` (6.0 s). All 1080×1920, H.264 + AAC.

## Render

```bash
cd marketing/video
npm install              # remotion + @remotion/cli + react only (no template extras)
npm run prepare:assets   # copies stills/music/fonts from the repo, scans public/clips
npm run studio           # preview
npm run render:tiktok    # → out/tiktok15.mp4  (also render:trailer, render:bumper)
```

On a machine with 2–3 GB of free RAM, keep `--concurrency=1` (the render scripts already set it).
