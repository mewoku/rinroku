# ODLET — Pitch v1

> Renamed: RONRIKU is now **ODLET** (odlet.xyz), 26 September 2026. Product name updated below; the plan and findings are unchanged.

> Written the way the team would naturally pitch it on launch morning: feature-first, lots of enthusiasm, not yet stress-tested. The roast (`02-roast.md`) takes this apart. The rewrite is `03-ideal-pitch.md`.

---

## ODLET: the pixel-art reasoning + action game for Solana Seeker

**Train your brain. Fight monsters. Own your hero.**

Odlet is a pixel-art game where your mind is the weapon. Walk a voxel hero across **5 worlds × 12 levels** and beat guardian monsters with fast micro-puzzles, Balatro-style rune card fights, ice-slide puzzles and a rhythm dungeon. Then come back every day for the **3-trial Daily brain challenge**, climb the **reasoning rating** leaderboard, raid **weekly bosses**, and collect **MagicaVoxel-style voxel figures** you really own as **Metaplex Core NFTs** on Solana.

Launching today on the **Solana dApp Store for Seeker**, and playable in any browser at the same time.

### What's inside

- **Adventure map**: 60 levels in 5 worlds (Lab, Prism, Ember, Grove, Frost), each with its own palette, music and world boss.
- **4 arcade modes**
  - **Battle**: 3–10 s micro-challenges (Next, Odd, Sum, Memory, Mirror, Arrows, Scales). Every right answer is an attack: *chips × mult*, combos up to ×6.
  - **Rune Hand**: a Balatro-like card fight. 36 runes, hands of 7, pairs to straights, **charms** that bend the scoring.
  - **Ice Dash**: swipe, slide until you hit a wall, grab the gems, reach the door. Par = the optimal solution.
  - **Beat Crawl**: a rhythm dungeon at 140 BPM. Move on the beat, bump monsters, build combo.
  - **Boss**: battle phase into a rune-hand phase with two charms.
- **Daily**: Pattern → Shadow → Link. Three trials, one mind. A chess-style **reasoning rating**, streaks and a global leaderboard.
- **Boss Raids**: weekly bosses, entry with shards or devnet SOL, a leaderboard per boss.
- **Voxel figures**: procedurally generated little voxel people in 3×3, 4×4 and 5×5 tiers, Common to Legendary. Buy them with shards or SOL, mint them as **Metaplex Core NFTs**, equip one as your avatar, trade them on the marketplace.
- **Procedural chiptune/funk soundtrack**: 9 tracks × 3 intensity layers. Combos push the music harder.
- **Friends**: add by handle, friend leaderboards, compare ratings.
- **Website**: the same game in WebGL, plus marketplace, inventory, profiles and leaderboards.

### Why Solana / Seeker

- **Real ownership.** Your figures are NFTs in your wallet, not rows in our database.
- **One-transaction mint.** The server prepares and partially signs a single transaction (payment, memo and Core mint). The buyer's wallet signs it and pays the fees and rent. The server mint authority never holds SOL.
- **Built for Seeker.** A 16 MB native Android build with haptics, gyroscope parallax, a 0.3 s cold start and a dp layout tuned for the 20:9 screen.
- **Fair play.** The server replays submitted answers instead of trusting "solved" flags. Every table has RLS and every write goes through an RPC.

### Tech

Unity 6 (Android + WebGL) · Supabase (Postgres, RLS, RPCs) · a TypeScript port of the C# domain (352 fixture tests) · Next.js 15 · Metaplex Core · Mobile Wallet Adapter on the web · self-hostable with Docker + Caddy.

### Economy

- Earn **shards** from level clears, Daily completion (streak bonus) and boss wins.
- Spend them on boss entry and figures (Common 300 / Rare 800 / Epic 2,000). Legendary figures are SOL only.
- SOL purchases mint NFTs directly to the buyer.

### Roadmap

- **Now**: dApp Store launch, web launch, devnet NFTs.
- **Next**: Seeker MWA inside the app, mainnet mint, NFT marketplace with TransferDelegate, more worlds.
- **Later**: seasons, guilds, tournaments with SOL prize pools, a figure editor, a creator economy.

### The ask

Download Odlet on the Solana dApp Store, play the Daily, and tell your friends. We're looking for community, partners and early supporters to help us grow the best brain game on Solana.

**ODLET: three tests, one mind.**
