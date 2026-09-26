# ODLET — The Ideal Pitch

> Renamed: RONRIKU is now **ODLET** (odlet.xyz), 26 September 2026. Product name updated below; the plan and findings are unchanged.

> A rewrite that answers `02-roast.md`. It has one hook, one audience wedge, honest crypto and honest numbers. Anything marked **TBD** or **target** is not a fact yet. Fill it in from launch-week data, never invent it.
>
> Deck version: the Slides artifact linked in the final report. Built from this document with the real screenshots in `docs/evidence/`.
>
> Framework: the `create-pitch-deck` skill with the **6-Part Arc** (Shift → Enemy → Tool → Proof → Future → Invitation) and Sequoia slide order. The skill's interview was answered from the repo docs and the owner's brief, not live. Before presenting, confirm the owner's answers to the three open questions at the bottom.

---

## 1. Hook (the bar test)

**ODLET: Solve to strike.**
Balatro-style combos where every card is a puzzle. A one-minute monster fight on your Seeker, and one Daily a day to prove your brain.

*Visual: the battle screen mid-hit, with the chips × mult box `20 × 1` and a `-20` pop on the monster.*

Say it in one breath: "You fight voxel monsters, but your attacks are tiny puzzles. Answer fast, chain combos, multiply damage. It's Balatro's dopamine with Wordle's daily habit, made for the Solana phone."

## 2. Problem

**Brain games are homework. Action games make you feel dumb-fast, not smart-fast.**

- "Brain training" apps (Elevate, Peak, Lumosity) feel like tests. Subscriptions ask you to pay for discipline.
- Daily puzzles (Wordle, NYT Games, Puzzmo) are loved, but they're a 3-minute ritual with no game around them.
- Balatro proved people will happily do arithmetic for hours if every number *explodes* (5M+ copies). No one has put that feeling on reasoning itself.
- On Seeker specifically, owners bought a crypto phone and mostly find wallets and utilities. There are few polished, original games you'd open daily.

*Our own evidence*: the v2 playtest. A first-time adult player got bored after world 1 because the puzzles were "slow and abstract". The fix wasn't easier puzzles. It was making every right answer *hit* something (PLAN_V3).

## 3. Why now

- **Seeker is in hands**: 150K+ devices shipped, and the dApp Store charges **zero platform fees** (vs 15–30 % on Google Play / App Store).
- **The store is crowding fast**: about 700 → 1,561 apps from March to June 2026. Being early *and* distinctive still matters, and a real game stands out among utilities.
- **Balatro made "chips × mult" a vocabulary.** Players already understand the scoring language we use.
- **Mobile Wallet Adapter + Metaplex Core** make a one-transaction, buyer-pays collectible mint practical on a phone.

## 4. Product

One loop, three depths:

| Depth | What you do | Time |
|---|---|---|
| **Fight** | Walk the map, tap a monster, and win a 60–120 s fight. Four modes: **Battle** (micro-puzzles = attacks), **Rune Hand** (Balatro-style card fight with charms), **Ice Dash** (slide puzzle), **Beat Crawl** (rhythm dungeon on the soundtrack's beat) | 1–2 min |
| **Daily** | Three trials (Pattern → Shadow → Link), a reasoning rating, a streak, and a shareable result | 3–5 min |
| **Raid** | A weekly boss with a leaderboard; entry costs shards you earned | 5 min / week |

- 5 worlds × 12 levels, each world with its own palette, music and boss.
- Procedural chiptune/funk: 9 tracks × 3 intensity layers. **Combos turn the music up.**
- Offline-first, 16 MB APK, ~0.3 s cold start. No wallet or account needed to start.
- Same game in the browser (WebGL) for friends without a Seeker.

*Screens: map, battle hit, rune scoring, ice dash, beat crawl, daily.*

## 5. Why Seeker / why Solana (honest version)

Crypto is **not** the game. The game works without a wallet, and that's deliberate. Solana does three jobs a database can't do as well:

1. **Seeker-native perks, verified on-chain.** Holders of the Seeker Genesis Token get a free Founders voxel figure. Only a Seeker owner can claim it, and anyone can verify that. *(Planned: week 1–2, needs MWA in the Unity app.)*
2. **Collectibles you actually hold.** Every voxel figure is procedurally generated from a seed and minted as a **Metaplex Core** asset in **one transaction the buyer signs**. The server never holds funds; its mint authority just co-signs. It's live on devnet today and goes to mainnet after the launch-week device pass.
3. **Zero-fee distribution plus native payments.** The dApp Store takes 0 %, and SOL/USDC via the Seeker wallet means we keep what players pay.

What we *won't* claim: that devnet items have value today, or that NFTs make the game fun. The footer and the in-app shop say "devnet / test SOL" until mainnet.

## 6. Proof (what's real on launch day)

We have no traction numbers yet because we launch today. What exists:

- **The whole product ships**: Android APK + WebGL site, 60 levels, 4 arcade modes, Daily, raids, shop, friends, marketplace (shards).
- **Fair by construction**: the server replays submitted answers instead of trusting "solved" flags. RLS on every table, all writes through RPCs.
- **Engineering depth**: 352 cross-language fixture tests (C# ↔ TypeScript domain), 38 backend integration tests, 113 web tests.
- **Balance by simulation**: Rune Hand is tuned so a greedy bot wins about 68 % of fights, flat across worlds.

## 7. Traction plan (first 30 days)

| Week | Lever | Metric we'll report (targets, not facts) |
|---|---|---|
| 0 (today) | dApp Store listing + web `/play` + 15 s vertical ad (see `04-video-ads.md`) | installs, % who finish W1-1 |
| 1 | **Daily share card** (Wordle grid) + "beat my Daily" link to the web version | shares per DAU (target ≥ 0.15) |
| 1–2 | **Genesis Founders figure** + an ask to Solana Mobile for editorial placement | Genesis claims, D1 retention (target 35 %) |
| 2–4 | **Weekly raid leaderboard** posted on X every Monday, plus a streamer "raid night" | weekly raid entries, D7 retention (target 15 %) |

All retention numbers come from the `daily_results` / `level_progress` tables we already store. They are reported as they come, good or bad.

## 8. Business model

Cosmetic-only, with **no pay-to-win**: a rating you can buy is worthless.

| Line | Price (proposed) | Status |
|---|---|---|
| **Voxel figures** (Rare / Epic / Legendary mints) | ~0.02–0.1 SOL | devnet live → mainnet |
| **Season Pass** (monthly: exclusive figures, +1 weekly raid entry, Daily archive) | ~$3.99 in SOL/USDC | month 1 |
| **Raid entry with SOL** (optional; shards always work) | ~0.01 SOL | devnet live → mainnet |
| Later: Google Play / web with standard IAP | — | month 3+ |

Unit economics: mint rent and fees are paid by the buyer's wallet and the store takes 0 %, so gross margin on Seeker sales is roughly 100 % minus the backend (one small VPS + Postgres). **TBD**: conversion rate. The planning assumption is 2–4 % payers, typical of F2P puzzle games, and gets replaced with real data in week 4.

## 9. Competition

| | Fun loop | Daily habit | Phone-native | Owns items |
|---|---|---|---|---|
| Balatro | ●●● | ○ | ●● (paid port) | ○ |
| Wordle / NYT / Puzzmo | ● | ●●● | ●● | ○ |
| Elevate / Peak | ● | ●● | ●●● | ○ |
| Typical dApp Store app | ○ | ● (quests) | ●● | ●● |
| **ODLET** | ●● | ●● | ●●● (Seeker) | ●● |

We don't out-Balatro Balatro. We're the **free, 1-minute, daily** version where the cards are puzzles, and the only one built for Seeker first.

## 10. Roadmap

- **Now (launch)**: dApp Store + web, 60 levels, Daily, raids, devnet collectibles.
- **Weeks 1–2**: share card, Genesis Founders figure, MWA in the app, retention dashboard.
- **Month 1**: mainnet mints, Season 1 pass, arcade proofs replay-verified server-side (anti-cheat parity with the Daily).
- **Months 2–3**: friend challenges ("beat my seed"), world 6, raid tournaments with on-chain prize settlement, Google Play build.

## 11. The ask

Proposed, for the owner to confirm (the pitch skill requires a specific ask):

1. **Solana Mobile**: editorial placement in the dApp Store games section for launch week, and a technical intro for the Genesis Token perk.
2. **Grant**: **$25K** (Solana Foundation / Solana Mobile builder programs) to fund 3 months of mainnet launch, the Season 1 content drop and device QA. **Milestone:** 5,000 installs and D7 ≥ 15 % by 2026-12-31, reported publicly.
3. **Community**: play the Daily for 7 days, share your grid, and tell us where you quit.

**ODLET: solve to strike.**

---

## Deck self-score (the skill's rubric for a grant / ecosystem audience)

| Dimension | Score | Note |
|---|---|---|
| Hook clarity | 🟢 8 | One line plus one image |
| Problem | 🟡 6 | Needs a real user quote from launch week |
| Why now | 🟢 8 | Hard numbers (150K Seekers, 0 % fees, catalogue growth) |
| Product / demo | 🟢 9 | Real screenshots, live web build |
| Crypto necessity | 🟡 5 | Honest, but the strongest piece (Genesis perk) isn't built yet |
| Traction | 🔴 2 | Launch day. Replace targets with actuals after week 1 |
| Business model | 🟡 5 | Designed, not validated |
| Ask | 🟡 6 | Specific, but the owner must confirm the amount and the programme |

## Q&A prep

- **"Why does this need a blockchain?"** The game doesn't, on purpose. Solana gives us Seeker-verified perks, collectibles players really hold, and zero-fee native payments, which are three things a Play Store game can't offer.
- **"Devnet NFTs are worthless."** Agreed, and we label them that way. Mainnet mints follow the launch-week device pass. The mint flow (one buyer-signed transaction, the server never holds funds) is already tested.
- **"Balatro will crush you."** Balatro is a paid, 40-minute-run PC-first game. We're free, 1-minute and daily. Different job, and we borrow the scoring language players already love.
- **"Why would anyone come back?"** The Daily streak, the weekly raid, and a share card that turns every player into an ad. We'll publish D1/D7 either way.
- **"Cheating on leaderboards?"** The Daily and raids are replay-verified server-side today. Arcade levels have time floors and stored proofs now, and full replay is next month.

## Open questions for the owner

1. Is the ask a grant, a pre-seed round or a partnership? The $25K grant above is a placeholder proposal.
2. Team slide: names, roles and one credibility line each (not in the repo).
3. The public domain and social handles for the CTA and share card.

Sources: [Solana Compass: dApp Store 1,561 apps, zero platform fees](https://solanacompass.com/news/solana-mobile-dapp-store-surpasses-1561-apps-as-catalog-more-than-doubles-in-three-months) · [Cointribune: 150,000 Seekers delivered](https://www.cointribune.com/en/solanas-new-crypto-smartphone-goes-global/) · [Game Developer: Balatro 5M copies](https://www.gamedeveloper.com/business/balatro-sells-5-million-copies-after-end-of-year-spike)
