# ODLET — store listing

Copy for the Solana dApp Store (Publisher Portal) and, later, Google Play. Media lives in `store/media/`
(regenerate with `python store/tools/make_art.py`).

## Title

`ODLET` (Play limit 30 chars). Website: https://odlet.xyz

Play subtitle-style name if wanted: `ODLET: Pixel Puzzle Arcade` (26 chars)

## Short description

- **dApp Store (max 30 chars):** `Pixel puzzle arcade adventure` (29)
- **Google Play (max 80 chars):** `Beat voxel monsters with quick puzzles, rune hands, ice dashes and beat crawls.` (79)

## Full description (≤ 4000 chars; this text is 2,047)

```
Beat monsters with your brain.

Odlet is a pixel-art puzzle arcade. Walk a map of five worlds, meet voxel monsters and knock them out with fast little puzzles, then come back every day for three fresh reasoning trials.

FIVE WORLDS, SIXTY LEVELS
Every world mixes four kinds of play, then ends in a two-phase boss fight.
• Battle: 3–10 second micro-puzzles (next in sequence, odd one out, sums, memory, mirror, arrows, scales). Right answers are attacks; chain them for combo multipliers before the monster swings.
• Rune Hand: a deck-building duel. Play combos from a hand of runes, pick a charm that bends the scoring, and beat the monster's HP with chips × mult.
• Ice Dash: swipe and slide until you hit a wall. Grab every gem, dodge the spikes, find the door. Three stars at par.
• Beat Crawl: a rhythm dungeon. Monsters move on the music's beat, and so do you. Stay on beat for combos.

THE DAILY
Three trials a day, the same for every player: Pattern (what comes out?), Shadow and Link. Keep your streak alive and climb the rating ladder.

BOSS RAIDS
Weekly bosses chain three trials against the clock. Pay with shards, win rewards.

COLLECT VOXEL HEROES
Earn shards by playing and spend them in the daily shop on hand-built voxel figures. Equip your favourite as your avatar.

MADE FOR THE PHONE
Portrait, one-handed, haptics, a procedural chiptune soundtrack that reacts to your combos, and a gravity-sensor parallax that makes the pixels float. Tuned for Solana Seeker.

PLAYS OFFLINE
The whole game works without a connection. When online, an anonymous account (no email, no password) keeps your progress, rating and friends, and the server checks results so leaderboards stay fair.

ABOUT SOLANA
The Android app has no wallet features and never asks for your keys. On the Odlet website (odlet.xyz) you can optionally link a wallet and buy figures with devnet SOL; those figures are minted as Metaplex Core NFTs on Solana devnet, a test network with no real money. Nothing in Odlet is an investment.

No ads. No tracking. No pay-to-win.
```

> Before submitting: if the backend is not live on HTTPS at release time, change the "PLAYS OFFLINE"
> paragraph to "Online accounts, leaderboards and friends are coming soon; the whole game works offline."
> Reviewers test what the text claims.

## Feature bullets (for the "What's in it" / promo fields)

- 60 arcade levels across 5 worlds, each ending in a two-phase boss
- 4 play styles: puzzle Battles, Rune Hand deck duels, Ice Dash, Beat Crawl rhythm
- Daily: three new reasoning trials a day, streaks and rating
- Weekly boss raids
- Collect and equip voxel figures from a daily shop
- Reactive chiptune soundtrack, haptics, sensor parallax
- Fully playable offline; anonymous online accounts, no email
- No ads, no tracking SDKs

## Saga / Seeker features field (dApp Store `saga_features`)

`Tuned for the Seeker's 20:9 display and haptics. No in-app wallet yet: optional devnet figure purchases and wallet linking happen on the Odlet website (odlet.xyz).`

## Category

- dApp Store: **Games**
- Google Play: **Games › Puzzle** (secondary tag: Arcade)

## Content rating notes (IARC questionnaire)

- Violence: cartoon/fantasy only — stylised voxel monsters are "defeated" by solving puzzles; no blood, no gore. Expect **PEGI 3 / ESRB Everyone** (at most PEGI 7 "mild fantasy violence").
- No sexual content, language, drugs, gambling with real money.
- **Simulated gambling:** none. Rune Hand is a card-combo puzzle scored by points; no wagering, no loot boxes bought with money. The shop sells fixed, visible items for in-game shards.
- User interaction: players choose a handle visible on leaderboards and can send friend requests by handle. **No chat, no user-generated text beyond the handle.** Answer "Users interact: yes (limited)" on Play.
- Shares location: no. Digital purchases: **none in the Android app**. (Devnet SOL purchases exist on the website only.)
- Crypto/NFT: the app contains no wallet, no token trading, no NFT purchase. See `RELEASE_CHECKLIST.md` for Play's blockchain policy.
- Target audience (Play "Target audience and content"): **13+** (not designed for children; avoids Families policy).

## Keywords / tags

pixel art, puzzle, brain game, logic, arcade, roguelike, deckbuilder, rhythm, daily puzzle, voxel, retro, chiptune, offline, Solana, Seeker

## What's new (v1.0 — first release)

```
First release: five worlds of arcade puzzle battles, Rune Hand duels, Ice Dash and Beat Crawl, a daily three-trial challenge, weekly bosses, voxel figures and a reactive chiptune soundtrack.
```

## Testing instructions for reviewers (dApp Store `testing_instructions`)

```
No login needed. Launch the app and tap PLAY: the adventure map opens at World 1 Level 1 (Battle). Levels unlock in order. DAILY runs today's three trials. Everything works offline; online accounts are anonymous and created automatically when the server is reachable. The app has no wallet connection and no purchases with real money.
```

## Media checklist

| Asset | File | Spec |
|---|---|---|
| App icon | `store/media/icon-512.png` | 512×512 PNG (dApp Store + Play) |
| Banner (dApp Store, required) | `store/media/banner-1200x600.png` | 1200×600 |
| Feature graphic (dApp Store, optional, editorial) | `store/media/feature-1200x1200.png` | 1200×1200 |
| Feature graphic (Google Play) | `store/media/feature-graphic-1024x500.png` | 1024×500 |
| Screenshots, Seeker 20:9 | `store/media/screenshots/1080x2400/01–05.png` | dApp Store: ≥1080 px each side, same orientation, ≥4 |
| Screenshots, 16:9 | `store/media/screenshots/1080x1920/01–05.png` | Google Play phone |

Screenshots: 01 Battle · 02 Rune Hand · 03 Ice Dash · 04 Daily Pattern · 05 Figure shop. They are
composed from real Editor captures in `docs/evidence/`. Before submitting, glance at them against the
current build (another engineer changed labels/share button after the captures) and re-run the
Capture PlayMode tests + `make_art.py` if anything visible changed.
