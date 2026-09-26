# ODLET — The Roast

> Renamed: RONRIKU is now **ODLET** (odlet.xyz), 26 September 2026. Product name updated below; the plan and findings are unchanged.

Method: `roast-my-product` framework (10 dimensions, value prop weighted 2×, common crypto sins, web3 UX red flags). Evidence: README, PLAN_V2, PLAN_V3, STATE, `web/src/app/page.tsx`, the Unity screens, and every screenshot in `docs/evidence/`. The target is `01-pitch-v1.md` and the build as it stands on dApp Store launch morning (2026-09-26).

---

## Verdict

**You built five good games and a crypto shop, then pitched all of them at once to nobody in particular. On launch day your own website still describes the game you threw away.**

---

## Scorecard

| # | Dimension | Score | Justification |
|---|---|---|---|
| 1 | Value Proposition (×2) | **4/10 → 8/20** | Pitch v1 lists 4 arcade modes, a Daily, raids, NFTs, a marketplace, friends and a soundtrack. There is no single sentence a stranger remembers. "Train your brain. Fight monsters. Own your hero." is three products. |
| 2 | Crypto Necessity | **3/10** | Figures are cosmetic avatars. Shards (the real economy) are Postgres rows. The only on-chain asset is a **devnet** NFT with no market value, and the in-app Seeker wallet (MWA in Unity) isn't shipped yet (STATE open item 3). Swap the chain for a DB table and nothing about play changes. |
| 3 | Target User Clarity | **3/10** | Balatro fans? Wordle/daily-puzzle people? Duolingo-style "brain training" adults? Seeker degens farming dApp Store installs? NFT collectors? Pitch v1 speaks to all five and converts none. |
| 4 | First-Time UX | **5/10** | Good: no wallet gate, offline-first, 0.3 s cold start, level 1 is one tap away. Bad: the first thing you see is **"PLAYER 0302 · LV1 · LOCAL"** and 11 padlocks. The Pattern trial needed a v3 fix because testers didn't get it. The charm pick screen is half empty. The one real playtest was a single adult. |
| 5 | Core Loop | **6/10** | The loops exist (Daily streak plus 60 levels plus weekly raid), but nothing *pulls* anyone back: no notification, no share, no friend challenge in the flow. The Daily results screen is a dead end with a CONTINUE button. Wordle grew on its share grid and ODLET has no share button. |
| 6 | Competitive Moat | **3/10** | Procedural voxel figures and deterministic seeds are neat engineering, not a moat. Balatro (Playstack) owns deckbuilding-scoring, Crypt of the NecroDancer owns rhythm roguelite, NYT Games / Puzzmo own the daily brain habit, Elevate/Peak own brain-training. You are a sampler of all four, which is a moat for none. |
| 7 | Technical Execution | **7/10** | The strongest part. 352 core fixture tests, server replay of answers, RLS everywhere, a one-tx buyer-pays mint, a 16 MB APK, render-on-demand voxels. But **v3 has not been played on a real phone** (STATE: "v3 not yet hands-on on device"), arcade proofs are not replay-verified, WebGL music start is unverified, and there has been no live devnet mint yet. |
| 8 | Naming & Messaging | **4/10** | "RONRIKU" (the old name; since renamed to ODLET) is distinctive but unexplained and hard to spell after one hearing. Worse, the messaging contradicts itself. The web hero says **"No reflexes, no luck — just a calm, sharp mind"** above a game with timed monster swings, a 140 BPM rhythm dungeon and card draws. The Seeker section says **"Solana dApp Store listing is in preparation"** and claims a "Seed Vault wallet" the app doesn't have yet. |
| 9 | Monetization Path | **2/10** | All SOL is **devnet SOL**. Revenue today is exactly zero, by design. No IAP, no pass, no ads, no mainnet. "Legendary = SOL only" gates your best content behind a test currency. Pitch v1's business model is "SOL purchases mint NFTs", which is a feature, not a business. |
| 10 | Market Timing | **5/10** | The audience is real: Seeker has shipped 150K+ units and the store takes zero platform fees. But the store is no longer empty. It went from about 700 apps (Mar 2026) to 1,561 (Jun 2026), with 96 new apps in one week, so "launch and get featured" is not a plan. Balatro (5M+ copies) made "chips × mult" a mainstream vocabulary, and daily puzzles are at peak habit. You are not using the Seeker-specific hooks (Genesis Token, MWA, Seed Vault) that would lift you out of 1,500 listings. |
| | **Weighted total** | **46 / 110** | "Fundamental problems": the build is far ahead of the positioning and the business. |

---

## The Worst Issues

### 1. You are pitching a buffet. Nobody orders a buffet.
**What's wrong**: Pitch v1 is a feature inventory: 4 modes, 7 micro-challenges, 3 trials, raids, figures, NFTs, marketplace, friends, soundtrack, website. Every screenshot shows a different game.
**Why it matters**: A dApp Store tile gets about 1.5 seconds and a Short gets about 1 second. "What is this?" has to be answered by one image and one line. The PLAN_V3 playtest already told you the product needs *one* feeling (fast, juicy, hero-driven), and the pitch hasn't caught up.
**What good looks like**: One hook: **"Balatro-style combos, but every card is a puzzle. One run a day on your Seeker."** The chips × mult box is the visual signature. Everything else is supporting cast.

### 2. The website is selling v2 on launch day
**What's wrong**: `web/src/app/page.tsx` still leads with the v2 pitch: "Daily pixel reasoning", "Three puzzles a day", **"No reflexes, no luck"**, "Median solve: under a minute", "Three chained puzzles against the clock" and "Solana dApp Store listing is in preparation". It also claims a "Seed Vault wallet" that the Unity app does not have yet.
**Why it matters**: Players arriving from the store listing or an ad see a different, slower game than the one they installed. A false wallet claim in front of dApp Store reviewers and Solana-native users is a credibility hit you can't take back.
**What good looks like**: The hero shows the battle screen with a chips × mult hit, the copy matches v3, and a real dApp Store badge links to the listing.

### 3. The crypto layer is decoration, and devnet decoration at that
**What's wrong**: NFTs are avatar skins bought with test SOL. The "tradeable" market for SOL listings is still "planned". The Unity app can't sign with the Seeker wallet. The footer literally says "Solana devnet only · no real funds".
**Why it matters**: Crypto-native Seeker users will read "devnet NFT" as "not real". Non-crypto players don't care about NFTs at all. Right now the chain costs trust and dev time and returns nothing.
**What good looks like**: Crypto does one job that a database can't do, on mainnet. Examples: **Genesis Token holders get a free Founders figure** (a Seeker-exclusive perk you can verify on-chain), or **weekly raid prize pools / leaderboard results settled on-chain**. Until mainnet, stop calling devnet items "owned" and "tradeable" in marketing.

### 4. No share, no pull, no measurement
**What's wrong**: The Daily results screen (`DailyResultsScreen.cs`) has no share action. Analytics (`docs/ANALYTICS_EVENTS.md`) is 4 local events that "send nothing over the network". There are no push/local notifications for the streak.
**Why it matters**: The Daily is your retention engine and your free marketing. Wordle's entire growth was the copy-paste grid. Without events you won't know D1/D7 retention on launch week, which is exactly the number any investor or dApp Store feature team will ask for.
**What good looks like**: A one-tap **SHARE** that posts "ODLET Daily #023 🟪🟦🟧 1:42 · rating 1226 · streak 3 + link". Retention measured from the `daily_results` / `level_progress` tables you already have (a SQL view, not a new SDK). A local notification at daily reset.

### 5. Placeholder texture everywhere a first-timer looks
**What's wrong**: "PLAYER 0302" as a name. "LV1 · LOCAL" as a status line. "LOCAL MODE // NO GLOBAL PERCENTILE YET" and online "PERCENTILE PENDING" on the results screen. Five skill deltas that are all **+26 / +26 / +26 / +26 / +25**, which reads as fake. The charm pick screen leaves the bottom 45 % empty.
**Why it matters**: These are the screenshots users and reviewers will post. Dev-state labels make a polished-looking game read as a beta.
**What good looks like**: The player gets a generated name (you already generate figure names like KEYOVI and NEHASU). "LOCAL" appears only when offline and reads "OFFLINE". Unfinished stats are hidden, not apologised for.

### 6. Unverified on hardware, on launch morning
**What's wrong**: STATE: "v2/v3 not yet hands-on on device". The Beat Crawl timing window, music on the phone speaker and frame rate on boards have never been felt on a phone.
**Why it matters**: A rhythm mode with latency is worse than no rhythm mode. The first reviews land in hours 0–24.
**What good looks like**: Do a 20-minute device pass before the listing goes live. If Crawl timing feels off, widen the window in `RonrikuTuning.asset` (no code change).

---

## Common Sins Detected

- **Ornamental blockchain**: cosmetic devnet NFTs. Nothing breaks if the chain is removed.
- **Bridge to nowhere (partial)**: excellent infrastructure (server replay, one-tx mint, fixtures) with no distribution plan in the pitch beyond "tell your friends".
- **No retention loop, weak form**: the loops exist, but there are no triggers (share, notification, friend challenge).
- **Complexity worship, in the pitch rather than the product**: Pattern → Shadow → Link, chips × mult, charms, shards, raids and tiers, all in one page.
- **Grant-dependent risk**: zero revenue lines, so today the product only continues on the team's own time or on grants.

## UX Red Flags

- **Mobile wallet flow missing in the native app**: MWA exists on the web only (STATE item 3). On a Seeker, this is the one flow that should be native.
- **No onboarding for non-crypto users around SOL**: the "SOL · ONLINE" button toasts "LEGENDARY FIGURES ARE DEVNET NFTS" and nothing explains what that means.
- **Stale/placeholder state**: the "LOCAL", "PERCENTILE PENDING" and "PLAYER 0302" labels.
- **Good**: no wallet gate. Value (play) comes before any wallet prompt. Keep it that way.

---

## Positioning vs. the field

| vs. | They own | Odlet's honest angle |
|---|---|---|
| **Balatro** | The chips × mult dopamine and deep runs | Bite-size (60–120 s) fights where *puzzles* are the cards. Free, on the phone, with a daily seed. Don't claim depth, claim speed. |
| **Wordle / NYT Games / Puzzmo** | The shared daily ritual | Same ritual, plus a rating and a streak you can compare with friends. Only works if you ship the share card. |
| **Elevate / Peak / Lumosity (Duolingo-style brain games)** | Credible "train your brain", subscriptions | Don't fight on science. Fight on *fun*: monsters, music, combos. The "reasoning rating" is a hook, not a claim. |
| **Other Seeker / dApp Store apps** | Wallet-native utility, token-farming quests | A polished original *game* in a crowded 1,500+ app store, with no wallet needed to start. Lead with Seeker-only perks (Genesis figure), not with devnet NFTs. |

---

## Top 10 fixes, ranked by impact

`PRE-LAUNCH` = a concrete product change the dev team can make in a few hours before the listing goes live. `POST` = after launch.

| Rank | Fix | Type | Concrete action | Effort |
|---|---|---|---|---|
| 1 | **Make the website match v3 and stop false claims** | PRE-LAUNCH (web) | `web/src/app/page.tsx`: hero kicker, subtitle and line 48 ("No reflexes, no luck…") rewritten to the v3 hook. Line 153: drop "Seed Vault wallet" and "listing is in preparation", add the dApp Store link. Line 123 boss copy: "battle, then a rune-hand showdown" for world bosses. Swap the first hero visual for the arcade battle screenshot. | 1 h |
| 2 | **Daily SHARE button** (Wordle grid) | PRE-LAUNCH (Unity) | `DailyResultsScreen.cs`: add a SHARE button next to CONTINUE. Build text `ODLET Daily #023 🟪🟦🟧 01:42 · 1226 (+26) · 🔥3 <site-url>` (one coloured square per trial solved, ⬛ for missed). On Android, send an `ACTION_SEND` intent via `AndroidJavaObject`; on WebGL/editor, copy with `GUIUtility.systemCopyBuffer` and toast "COPIED". | 2–3 h |
| 3 | **Kill dev-state labels** | PRE-LAUNCH (Unity) | `PlayerProfile.cs:132`: default name = a generated figure-style name (reuse the figure name generator) instead of "PLAYER " + id. Header "LV1 · LOCAL" shows "OFFLINE" only when offline. `DailyResultsScreen.cs:44`: remove the "PERCENTILE PENDING / NO GLOBAL PERCENTILE YET" line. `HomeScreen.cs:77`: drop "LOCAL MODE". | 1 h |
| 4 | **Hide the identical skill deltas** | PRE-LAUNCH (Unity) | `DailyResultsScreen.cs:78–95`: render the skills row only if the deltas differ (or remove it for launch). Five +26s look fabricated. | 15 min |
| 5 | **Device pass + Crawl timing** | PRE-LAUNCH (QA) | 20 min on the Pixel 6a/Seeker: W1-1 battle, W1-3 ice, W1-4 rune, W1-6 crawl. If beats feel late, widen the crawl window in `RonrikuTuning.asset` (no code). Record the ad footage in the same session (see `04-video-ads.md`). | 30 min |
| 6 | **Label devnet honestly in-app** | PRE-LAUNCH (Unity) | Everywhere SOL appears in the app (`ShopScreen.cs:68, 94`, boss entry), say "TEST SOL (DEVNET)" and add one line: "Collectibles are on Solana devnet during early access." This protects store review and trust. | 30 min |
| 7 | **Fill the charm-pick screen** | PRE-LAUNCH (Unity, optional) | Put the monster voxel and "HP 558 · 4 HANDS" under the three charms so the choice has context and the screen isn't 45 % empty. | 1 h |
| 8 | **Retention measurement from existing tables** | POST (day 1) | A SQL view over `daily_results` + `level_progress`: D1/D7 by first-seen day, levels reached, where players quit (world/level). No SDK needed. Without this the traction slide is empty. | 2 h |
| 9 | **Seeker Genesis perk + MWA in the app** | POST (week 1–2) | MWA in Unity (open item 3), then verify a Genesis Token and grant a free "Founders" figure. This is the one crypto feature Seeker owners can't get elsewhere, and it gives the dApp Store editorial team a reason to feature you. | days |
| 10 | **A real revenue line** | POST (month 1) | Mainnet figure mints at small prices plus a Season Pass (cosmetic figures, extra raid entries, a "Daily archive" of past dailies), sold in SOL/USDC via MWA. The Solana dApp Store takes no cut, so it's a genuine margin advantage over Google Play. | weeks |

## Fix These Now (the 3 that matter most)

1. **Highest impact: the SHARE button.** It is the only free distribution channel a daily game has. (Fix #2)
2. **Easiest win: the website copy plus dev-state labels.** An hour of string changes removes the contradictions and the "beta" smell from every screenshot. (Fixes #1, #3, #4)
3. **Existential: a reason for crypto that isn't decoration, and money that isn't devnet.** Genesis perk → mainnet → season pass. If you can't answer "why Solana" with something a DB can't do, the Seeker audience will treat you as a port. (Fixes #9, #10)

---

Sources (market facts, checked 2026-09-26): [Solana Compass: dApp Store hits 1,561 apps, zero platform fees](https://solanacompass.com/news/solana-mobile-dapp-store-surpasses-1561-apps-as-catalog-more-than-doubles-in-three-months) · [Cointribune: 150,000 Seekers delivered](https://www.cointribune.com/en/solanas-new-crypto-smartphone-goes-global/) · [Game Developer: Balatro sells 5 million copies](https://www.gamedeveloper.com/business/balatro-sells-5-million-copies-after-end-of-year-spike)
