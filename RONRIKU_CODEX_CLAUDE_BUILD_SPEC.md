# RONRIKU — Codex / Claude Autonomous Build Specification

> Paste this document into Codex or Claude Code at the root of the repository, or ask the agent to read it and execute it. This is an implementation contract, not a brainstorming brief.

## 0. Agent mandate

You are the lead engineer, technical game designer, and delivery owner for **RONRIKU**, a production-quality portrait mobile reasoning game being built for:

1. the Solana Mobile / Seeker ecosystem; and
2. the Colosseum Crypto World's Fair Hackathon and subsequent accelerator application.

Your job is to **build, run, test, and polish the product**, not merely scaffold folders, produce mockups, or write a plan.

Operate autonomously within this specification. Make sensible implementation decisions without waiting for approval when those decisions are reversible and preserve the product intent. If a dependency, credential, platform tool, or external service is unavailable, isolate it behind an interface, implement a functional local substitute, document the blocker, and continue building every unaffected part.

Do not attempt the entire specification at once. Complete the prioritized vertical slice first, prove it on an Android build, and then expand phase by phase.

At the start:

1. inspect the repository and all existing files;
2. inspect `docs/reference/home-v1.png` and `docs/reference/home-v2.png` if present;
3. identify the installed Unity version and available Android tooling;
4. identify uncommitted user changes and preserve them;
5. read all repository-level agent instructions;
6. create or update `docs/IMPLEMENTATION_PLAN.md`, `docs/ARCHITECTURE.md`, `docs/STATE.md`, and `docs/RISKS.md`;
7. begin Phase 0 and then Phase 1 immediately.

Never claim that something works unless you have run the relevant verification. Clearly distinguish verified behavior, locally simulated behavior, and work blocked by unavailable external infrastructure.

---

## 1. Product definition

### Product name

**RONRIKU** is the product and company name. Use it consistently across the app, codebase, documentation, Android package configuration, backend, onchain program metadata, and submission materials.

**DAILY** is the permanent name of the main daily game mode, even if the product is later renamed.

### One-sentence pitch

> RONRIKU is a competitive network for human reasoning: players prove skill through short daily visual challenges, build a persistent reasoning rating, challenge friends, compete in sponsored events, and eventually create challenges of their own.

### Product thesis

Static puzzle libraries were built for a world before generative AI. AI makes answers cheap and static puzzle content easy to reproduce. RONRIKU turns reasoning into an evolving competitive network built around dynamic challenges, persistent skill identity, social competition, empirical difficulty, creator supply, and globally accessible rewards.

The mobile game is the wedge. The long-term product is:

```text
players + skill identity + competitions + creators + prize settlement
```

### What users should feel

RONRIKU should feel like opening a small, intelligent artifact every day: calm, precise, slightly mysterious, competitive, and instantly playable. It must not feel like a crypto dashboard, a casino, an educational worksheet, or a generic Unity prototype.

### Non-negotiable principle

The game must be compelling when all wallet and reward features are ignored. Solana improves prize funding, settlement, creator payments, and verifiability; it does not replace game design.

---

## 2. Colosseum objective

Build RONRIKU as the beginning of an enduring startup, not a disposable hackathon demo.

The submission must demonstrate:

- a polished and understandable consumer product;
- a differentiated insight about reasoning games in the AI era;
- a working daily retention loop;
- a credible distribution loop through share cards and friend challenges;
- one clear, useful Solana primitive;
- evidence of creator-driven content supply;
- a path to sponsorship and revenue;
- real product telemetry and early-user evidence;
- disciplined prioritization and strong execution velocity.

Use **October 5** as the internal feature-complete deadline unless the owner supplies a newer date. Reserve the remaining time before submission for Android device QA, user testing, bug fixing, metrics, pitch assets, and video capture.

### North-star metric

**Weekly completed reasoning sessions**

Supporting metrics:

- D1 and D7 retention;
- Daily start and completion rates;
- average puzzles per session;
- median completion time;
- streak length;
- share-card open and successful-share rate;
- friend-challenge creation and completion rate;
- wallet-connect rate after reward intent;
- Boss participation and completion;
- creator puzzles published and played;
- daily and weekly active users.

Do not optimize the product around transaction volume.

---

## 3. Product loops

### Short loop: 30–180 seconds

```text
puzzle → action → immediate feedback → solve → score
```

### Daily loop: 3–7 minutes

```text
three Daily Trials → result → percentile → rating change → streak → share/challenge
```

### Long-term loop

```text
improve skills → raise rating → enter harder events → defeat Bosses
→ challenge friends → publish puzzles → build reputation
```

The first build must make the short and daily loops excellent. It should expose, but not overbuild, the long-term loop.

---

## 4. Hackathon MVP: exact scope

### P0 — must be functional and polished

- first-run experience with no required account;
- Home / Daily screen matching the supplied visual references;
- a complete three-trial Daily session;
- one Pattern mechanic;
- one flagship Spatial mechanic;
- one Logic mechanic;
- deterministic procedural variants with independently validated answers;
- results screen with time, percentile, rating change, skill changes, and streak;
- persistent local profile;
- share card that never reveals the solution;
- friend challenge deep-link contract and working local/end-to-end path;
- global/daily leaderboard backed by a swappable repository;
- one polished sponsored World Boss;
- Android wallet connection using Mobile Wallet Adapter;
- one Devnet Challenge Pool flow: sponsor-funded pool metadata and eligible claim;
- server-authoritative attempt contracts and backend validation path;
- anonymous play before authentication or wallet connection;
- analytics event pipeline with a privacy-safe implementation;
- working Android build suitable for a Solana Seeker-class device;
- automated domain/generator tests and documented setup.

### P1 — important proof of network potential

- minimal Spatial creator editor: place cubes, choose target, validate, test, publish;
- creator attribution and public puzzle detail;
- empirical difficulty/plays/solve-rate display;
- one static or remotely configured Human vs AI benchmark presentation using honest measured data;
- production-quality submission demo seed/data configuration;
- lightweight internal/admin procedure for creating Daily and Boss configurations.

### Explicitly out of scope for the hackathon MVP

- a custom fungible token;
- NFTs or tradable player items;
- DAO governance;
- paid-entry competitions;
- automatic redistribution of loser funds;
- real-time multiplayer;
- a full creator marketplace or payout economy;
- dozens of minigames;
- fully onchain gameplay or rating calculation;
- large 3D worlds;
- AI-generated-everything pipelines;
- claims of being cheat-proof;
- medical, educational, employment, or IQ claims.

Do not expand into these systems unless every P0 item is verified and the owner explicitly reprioritizes them.

---

## 5. Technology baseline

### Client

- Unity 6 LTS or the exact Unity 6 version already established by the repository;
- C#;
- Universal Render Pipeline;
- Android primary target;
- portrait orientation;
- Solana Seeker and ordinary modern Android phones;
- 60 FPS target on representative hardware;
- Unity Input System;
- TextMeshPro or a properly licensed/imported pixel typeface with TMP assets;
- orthographic 3D camera for Spatial puzzles;
- async/await where it improves clarity, with Unity lifecycle safety;
- native Android share and deep-link support behind interfaces.

Do not upgrade or downgrade an existing Unity project casually. If the repository is empty and the requested Unity editor is unavailable, create the source structure and domain tests that can be verified without fabricating Unity-generated project files; document the exact editor action still required.

### Backend

Preferred initial target:

- Supabase;
- PostgreSQL;
- Edge Functions or an equivalent small server runtime;
- server-side service credentials only;
- database migrations committed to the repository;
- local development configuration and seed data.

Keep backend access behind typed interfaces. The client must not depend directly on Supabase types in its domain or presentation layers.

### Solana

- Solana Devnet during development;
- Mobile Wallet Adapter on Android;
- official/currently maintained Unity-compatible integrations only;
- an Anchor program or equally well-supported Rust program for the Challenge Pool if a program is required;
- no player private keys in the app or backend;
- no privileged key, seed phrase, service-role key, or RPC secret committed to source.

Before selecting or pinning a Solana package, verify its current maintenance status, Unity compatibility, Android requirements, license, and official documentation. Record the decision and exact version in `docs/ARCHITECTURE.md`.

### Repository shape

Use a monorepo unless an existing structure clearly dictates otherwise:

```text
/
  client/                    # Unity project, or repository root if already established
  backend/
    functions/
    migrations/
    tests/
  programs/
    challenge-pool/
  docs/
    reference/
    IMPLEMENTATION_PLAN.md
    ARCHITECTURE.md
    STATE.md
    RISKS.md
    SECURITY.md
    DEMO_SCRIPT.md
  scripts/
  README.md
```

Do not move an established Unity project merely to match this suggestion if doing so creates risk.

---

## 6. Visual contract

### Authoritative references

The following files, when present, are the authoritative Home-screen references:

- `docs/reference/home-v1.png`
- `docs/reference/home-v2.png`

`home-v2.png` is the stronger reference for composition. `home-v1.png` additionally shows the intended bottom navigation and lower profile treatment.

Do not reinterpret these references as a conventional modern app UI. Reproduce their visual language while adapting it to safe areas, different Android aspect ratios, accessibility settings, and actual gameplay requirements.

If the files are missing, note this in `docs/RISKS.md` and continue using the contract below. Do not block domain work.

### Visual identity

```text
retro pixel interface
+ minimal low-poly 3D
+ abstract intelligence laboratory
+ visual reasoning experiment
```

Approximate palette:

| Role | Color |
|---|---|
| Graphite | `#28292F` |
| Near black | `#17181C` |
| Black | `#090A0C` |
| Blue grey | `#536971` |
| Deep blue | `#135B73` |
| Teal | `#11C5B3` |
| Yellow | `#E8DA37` |
| Off white | `#E7E8E5` |

The 3D puzzle object is the hero. Navigation and metadata must be subordinate.

### Home composition

From top to bottom:

1. compact avatar, username, level, and Reasoning Rating;
2. generous negative space;
3. large pixel `DAILY` title;
4. centered isometric board preview with blue/teal tiles and yellow cubes;
5. compact Daily number/reset/participation metadata;
6. hard-edged primary `BEGIN` action;
7. minimal five-item bottom navigation.

Avoid:

- glossy materials;
- fantasy UI;
- soft rounded SaaS cards everywhere;
- generic gradients;
- stock icon packs with mismatched visual weights;
- oversized navigation;
- excessive text;
- noisy particles;
- bounce-heavy animation;
- wallet branding on the primary gameplay surface.

Prefer:

- hard rectangles and square corners;
- intentional pixel typography;
- low-poly primitives and clean silhouettes;
- large quiet areas;
- precise spacing on a small grid;
- teal/yellow contrast;
- short, sharp motion;
- restrained directional light and subtle shadows;
- a fixed or tightly controlled orthographic/isometric view.

Suggested camera baseline:

- orthographic projection;
- roughly 45° Y rotation;
- 30–40° downward angle;
- composition tuned per aspect ratio rather than by changing the visual language.

### Responsive and accessibility behavior

- respect Android safe areas and display cutouts;
- define minimum touch targets even when controls look visually compact;
- do not rely on color alone for puzzle state;
- support reduced motion through a presentation setting;
- maintain readable contrast;
- test at least narrow, typical, and tall portrait aspect ratios;
- prevent layout overlap at supported text scales;
- do not allow the bottom navigation to compete with the puzzle.

### Motion

Target timings:

| Action | Duration |
|---|---:|
| Screen enter | 150–250 ms |
| Tile selection | 80–140 ms |
| Cube snap | 100–180 ms |
| Correct reveal | 250–450 ms |
| Results count-up | 500–900 ms |

The product should feel controlled and intelligent. Use easing deliberately and avoid continuous decorative animation that distracts from reasoning.

---

## 7. Navigation and screens

Use five primary tabs:

1. **DAILY**
2. **BOSSES**
3. **CREATE**
4. **RANK**
5. **PROFILE**

Required screens or equivalent routed views:

- Bootstrap;
- First Run;
- Home / Daily;
- Daily Game;
- Daily Results;
- Bosses;
- Boss Game;
- Create;
- Published Puzzle;
- Rank;
- Profile;
- Wallet / Rewards sheet or modal;
- Friend Challenge entry/result.

Prefer reusable routed views and puzzle hosts. Do not create a separate Unity scene for each puzzle instance.

### First-run flow

```text
logo
→ THREE TESTS. ONE MIND. EVERY DAY.
→ BEGIN
→ first playable trial
```

No immediate account wall. No wallet prompt before the player has understood the game. Request wallet connection only at an intentional reward, identity, or claim moment.

---

## 8. Daily session

Each Daily contains exactly three trials for the MVP:

1. Pattern;
2. Spatial;
3. Logic.

The session must have a stable challenge ID and version, deterministic content, server-issued attempt data when online, a local-development equivalent when offline, and a clear transition between trials.

### Daily result

Show:

```text
DAILY COMPLETE

02:14
TOP 3.8%

RATING
1821 → 1847
+26

STREAK 12
```

Also show compact dimension changes for Pattern, Spatial, Logic, Planning, Memory, and Speed where justified by the completed trials.

The result screen must offer:

- share;
- challenge a friend;
- view ranking;
- return home.

Never expose puzzle solutions in the share payload.

---

## 9. Deterministic puzzle framework

Puzzle content must not be coupled directly to scenes or view objects.

Create clear concepts equivalent to:

```csharp
IPuzzleDefinition
IPuzzleGenerator<TData>
IPuzzleController
IPuzzleValidator<TData, TAnswer>
IPuzzleRenderer<TData>
IPuzzleScorer
```

Use plain C# for domain logic wherever Unity APIs are unnecessary.

Every generated puzzle must include serializable metadata:

```text
id
version
type
seed
variantSeed
difficulty
rulesVersion
goal
timeLimit
skillDimensions
contentHash
```

Generation must accept deterministic inputs, for example:

```csharp
PuzzleData Generate(long seed, PuzzleDifficulty difficulty, VariantSpec variant);
```

Requirements:

- same inputs produce logically identical content across supported platforms;
- solutions are validated independently of presentation state;
- generated schemas can be served as versioned JSON;
- a rules version prevents silent generator changes from invalidating old attempts;
- random-number behavior is explicit and testable;
- no generator depends on frame time, object order, or Unity scene state;
- decoys must be plausible but uniquely incorrect;
- all important generator invariants have property-style multi-seed tests.

### Per-player variation

Important competitions may issue variants with the same underlying reasoning rule but transformed presentation:

- rotation;
- reflection;
- palette mapping;
- object permutation;
- coordinate remapping;
- answer-order permutation.

The variant must preserve logical difficulty as closely as possible and must not alter the correct-answer uniqueness. Store the logical puzzle seed separately from the presentation variant seed.

---

## 10. Puzzle mechanics

### Pattern trial

Implement one polished visual transformation puzzle using a structure such as:

```text
INPUT → OUTPUT
INPUT → OUTPUT
INPUT → ?
```

Supported rule vocabulary may include:

- rotation;
- reflection;
- translation;
- color substitution;
- object count;
- boolean composition;
- symmetry;
- repetition;
- ordered transformation.

For the MVP, choose a constrained subset that can be generated reliably. Multiple-choice answers are acceptable. Generate one correct option and plausible decoys derived from common reasoning errors. Every puzzle must be proven to have exactly one accepted answer.

### Spatial trial — flagship mechanic

Build one excellent mechanic, not a collection of incomplete ones.

Recommended MVP mechanic:

> Rotate a small isometric cube structure and choose or construct the orientation that matches a target projection.

Visual requirements:

- teal primary board tiles;
- deep-blue secondary tiles;
- yellow interactive cubes;
- flat materials;
- simple geometry;
- orthographic/isometric camera;
- clear silhouette;
- subtle contact shadows;
- no unnecessary environment.

Input requirements:

- tap and swipe on Android;
- discrete, deterministic 90° rotations;
- snap animation;
- input lock during authoritative state transition;
- undo/reset when appropriate;
- haptic hooks;
- mouse simulation in Unity Editor;
- no dependence on precise pixel dragging.

### Logic trial

Build one robust, rule-based grid or node mechanic.

Recommended MVP mechanic:

> Route a signal through a compact tile grid while satisfying ordered or limited-use constraints.

It must support deterministic generation, independent solver/validator logic, a guaranteed valid solution, a uniqueness rule when required, and concise visual instructions demonstrated through interaction rather than a wall of text.

### Boss

Create one authored/deterministically varied Boss that feels meaningfully harder and more dramatic while preserving the same design system.

The Boss should combine at most two familiar mechanics. Do not introduce several unexplained systems at once.

Display:

- sponsor identity as configuration, not hardcoded product UI;
- reward amount and asset;
- start/end or remaining time;
- global attempts;
- solve rate;
- difficulty;
- leaderboard;
- reward verification status;
- claim eligibility after results are finalized.

---

## 11. Reasoning Rating and skill profile

Reasoning Rating is the core persistent identity, not decorative XP.

Profile dimensions:

- Pattern;
- Spatial;
- Logic;
- Planning;
- Memory;
- Speed.

Never call the score IQ. Never make medical, clinical, academic, or employment-suitability claims.

### MVP rating model

Implement a transparent, deterministic formula based on:

- puzzle difficulty;
- successful completion;
- solve time relative to a documented target or cohort baseline;
- hints, invalid moves, or resets when relevant;
- confidence/attempt count to limit early volatility.

Prefer an Elo-like or bounded update model that users can understand over an opaque pseudo-scientific number. Rating changes must be reproducible on the server. Document:

- formula;
- constants;
- initial rating;
- bounds;
- failure behavior;
- how dimension scores aggregate;
- limitations of the prototype.

The server is authoritative online. Local mode may calculate the same formula but must label results as local/noncompetitive.

### Streak rules

Define date boundaries in UTC at the server and document them. Handle first completion, same-day repeat, consecutive day, missed day, clock changes, offline completion, and eventual sync without allowing a client clock to authoritatively grant a competitive streak.

---

## 12. Social and distribution

### Share card

Generate a visually distinctive result card using the RONRIKU visual system:

```text
RONRIKU 024

■■□□■
□■■■□
■■□□■

SOLVED  02:31
TOP 4.7%
RATING 1847  +21
STREAK 12
```

The pattern must be decorative or derived from non-solution session state. It must not reveal answers.

Support native Android sharing, a fallback image export, and analytics for share opened/completed/cancelled where platform APIs allow reliable distinction.

### Friend challenge

A player can issue a friend challenge from a result or published puzzle.

Link payloads must use an opaque challenge token or signed server identifier, not a raw trusted score. The recipient receives the same logical challenge or equivalent transformed variant. Show:

```text
CHALLENGE NAVAL
SPATIAL // 7.4

NAVAL   31.42s
YOU     ??.??s
```

Support app deep links and a browser/install fallback contract. If universal/app links cannot be deployed yet, implement and test the routing layer with development links and document the remaining deployment configuration.

---

## 13. Creator proof

The hackathon Creator is intentionally narrow.

### Spatial creator flow

```text
CREATE
→ choose small board size
→ place/remove cubes
→ set starting state
→ set target state/projection
→ validate
→ test-play
→ publish
```

Publishing must fail when the puzzle is invalid, trivial under defined rules, outside limits, or lacks a solution. Store a schema version and content hash.

Published puzzle detail shows:

- title or generated identifier;
- creator;
- difficulty estimate;
- plays;
- solve rate;
- median time when enough data exists;
- play action;
- challenge/share action.

Do not implement creator payouts in the MVP. Define future payout interfaces and explain that empirical difficulty and quality—not player failure alone—would contribute to creator rewards.

---

## 14. Human vs AI benchmark

This is a demo/event mode, not the core product promise.

For one selected challenge, support presentation of a versioned benchmark:

```text
HUMANS       41%
MODEL A      72%
MODEL B      68%
```

Only show honestly measured results. Store model name/version, prompt/protocol version, run count, date, puzzle version, and scoring method. If no benchmark has been run, show no invented numbers; use an internal placeholder clearly marked as demo data only outside production builds.

---

## 15. Server-authoritative competition

Never trust the mobile client for competitive outcomes.

The server owns:

- challenge identity and rules version;
- logical seed and per-player variant assignment;
- attempt start and expiry;
- completion receipt time;
- answer validation;
- score and rating change;
- leaderboard insertion;
- reward eligibility;
- result finalization;
- claim allocation.

Define an `AttemptSession` or equivalent containing:

```text
sessionId
challengeId
challengeVersion
variantId
issuedAt
expiresAt
rulesVersion
nonce
serverSignatureOrOpaqueToken
```

The client submits actions/answer data needed for validation; it must not submit `completed=true` as proof.

For important events, capture minimal privacy-safe telemetry useful for abuse detection, such as interaction count and timing deltas. Do not collect invasive sensor data. Rate-limit endpoints and make completion idempotent.

Document precisely what remains client-trusted in the prototype. Do not describe deterrence as perfect security.

---

## 16. Solana primitive: Challenge Pool

Keep gameplay and rating offchain. Use Solana for transparent reward funding and settlement.

### Conceptual account/model

```text
ChallengePool
  authority
  challengeIdHash
  rulesHash
  startTime
  endTime
  rewardMint
  fundedAmount
  sponsor
  resultsRoot
  claimDeadline
  status
```

Expected lifecycle:

```text
create → fund → active → finalize results root → claim → close/expire
```

For MVP, support a sponsor/treasury-funded Devnet pool and eligible winner claim. No paid entry.

### Result settlement

After the competition ends:

1. backend validates attempts and calculates allocations;
2. backend publishes or authorizes a results root/commitment;
3. eligible players receive proofs or signed claim data;
4. players claim through their wallet;
5. the program prevents replay/double claim;
6. unclaimed funds follow an explicit expiry policy.

### Program requirements

- deterministic PDA/account derivation;
- checked token transfers;
- idempotent or replay-safe claim path;
- explicit authority model;
- immutable challenge/rules commitment after activation;
- end/claim time checks;
- duplicate-claim prevention;
- event emission/logging useful for indexing;
- tests for unauthorized finalization, invalid proof, wrong mint, wrong recipient, duplicate claim, time boundary, arithmetic safety, and close/expiry behavior;
- threat model in `docs/SECURITY.md`.

Use the simplest auditable design that produces a credible Devnet demonstration. Do not introduce upgradeability or governance complexity unless strictly required by the selected framework.

### Client service boundary

Define an interface equivalent to:

```csharp
public interface ISolanaService
{
    Task<WalletConnection> ConnectWalletAsync(CancellationToken ct);
    Task DisconnectWalletAsync(CancellationToken ct);
    WalletState GetState();
    Task<SignedMessageResult> SignMessageAsync(byte[] message, CancellationToken ct);
    Task<TransactionResult> SignAndSendTransactionAsync(TransactionRequest request, CancellationToken ct);
    Task<ClaimResult> ClaimRewardAsync(RewardClaim claim, CancellationToken ct);
}
```

Implement:

- `MockSolanaService` for Editor/local development;
- `MobileWalletAdapterSolanaService` for Android;
- graceful unsupported/unavailable/cancelled/error states;
- no wallet requirement for ordinary Daily play.

---

## 17. API contract

Use versioned typed DTOs distinct from domain models. A suggested MVP surface:

```text
GET  /v1/daily
POST /v1/attempts/start
POST /v1/attempts/{id}/complete
GET  /v1/leaderboards/daily
GET  /v1/leaderboards/global
GET  /v1/bosses/current
POST /v1/wallet/link/challenge
POST /v1/wallet/link/verify
GET  /v1/rewards
POST /v1/rewards/{id}/prepare-claim
POST /v1/friend-challenges
GET  /v1/friend-challenges/{token}
POST /v1/creator/puzzles
GET  /v1/puzzles/{id}
```

Requirements:

- explicit request/response schemas;
- meaningful error codes;
- idempotency keys for attempt completion, publishing, and claim preparation;
- schema/version compatibility strategy;
- server timestamps;
- bounded payload sizes;
- rate limiting;
- validation at the boundary;
- no privileged secret in the Unity client;
- repository/service interfaces so presentation never calls HTTP directly.

Anonymous identity may begin as a server-issued installation/player ID. Wallet linking must use a nonce and signed-message verification; a wallet address alone is not authentication.

---

## 18. Local development mode

The entire core game must remain usable without backend or wallet access.

Local mode must:

- generate today's Daily from a deterministic local seed;
- create and persist a local profile;
- simulate a leaderboard with clearly labeled data;
- use the mock wallet;
- simulate a sponsored reward and eligible claim state;
- create and resolve local friend-challenge links/tokens;
- save locally created puzzles;
- expose a small developer menu for fixed dates/seeds and state reset;
- make mock/local state visibly distinguishable from live competitive state where confusion would be harmful.

Do not scatter environment checks across UI code. Centralize runtime/environment configuration.

---

## 19. Architecture rules

Suggested Unity structure:

```text
Assets/
  Ronriku/
    Art/
    Audio/
    Fonts/
    Materials/
    Prefabs/
    Scenes/
    Scripts/
      Application/
      Domain/
        Puzzles/
        Player/
        Competition/
        Creator/
        Rewards/
      Infrastructure/
        Api/
        Analytics/
        DeepLinks/
        Persistence/
        Sharing/
        Solana/
      Presentation/
        Screens/
        Components/
        Animation/
        Accessibility/
      Composition/
    Tests/
      EditMode/
      PlayMode/
```

Rules:

- no giant MonoBehaviours;
- no business logic in view components;
- plain C# domain classes when Unity is unnecessary;
- explicit dependencies and a lightweight composition root;
- no heavyweight dependency-injection framework without a demonstrated need;
- no global mutable service locator;
- cancellation and destroyed-object handling for async Unity flows;
- repositories/interfaces at infrastructure boundaries;
- DTO-to-domain mapping kept out of UI;
- serializable state versioning and migration from the first persisted release;
- configuration through typed assets/files, not magic constants spread through code;
- errors surfaced to the user in product language and logged with actionable context.

---

## 20. Audio and haptics

Provide centralized hooks for:

- tile hover/focus;
- selection;
- cube rotation/snap;
- valid solution;
- invalid move;
- trial transition;
- Daily completion;
- rating increase;
- Boss reveal/completion;
- reward claim.

If final audio is unavailable, use isolated temporary assets or silent implementations. Never let missing audio block interaction. Respect user audio/haptic settings and device capabilities.

---

## 21. Analytics

Define `IAnalyticsService` with no-op/local and production implementations.

Minimum events:

```text
app_opened
first_run_started
first_puzzle_started
first_puzzle_solved
daily_viewed
daily_started
puzzle_started
puzzle_failed
puzzle_solved
puzzle_abandoned
daily_completed
results_viewed
share_opened
share_completed
friend_challenge_created
friend_challenge_opened
friend_challenge_completed
boss_viewed
boss_started
boss_completed
creator_opened
puzzle_created
puzzle_published
wallet_connect_started
wallet_connected
wallet_connect_failed
reward_claim_started
reward_claimed
```

Include versioned, minimal properties such as challenge ID/version, puzzle type, difficulty bucket, session duration, and environment. Never send solutions, private keys, signed raw transactions, or unnecessary personal data. Document the event dictionary.

---

## 22. Performance targets

Primary target: 60 FPS on a representative Seeker-class Android device.

Requirements:

- simple shaders and materials;
- minimal transparent overdraw;
- low draw-call puzzle scenes;
- object pooling only where measurements justify it;
- no avoidable per-frame allocations;
- event-driven behavior instead of unnecessary `Update()` loops;
- no expensive post-processing required for the core look;
- bounded texture sizes;
- compressed audio and assets appropriate for mobile;
- memory, load-time, and build-size observations recorded during polish;
- app resume, pause, interruption, and orientation-lock behavior tested.

Profile before complex optimization. Preserve visual clarity.

---

## 23. Testing and verification

### Domain tests

Automate tests for:

- deterministic generation;
- solution validation;
- answer uniqueness;
- variant equivalence;
- rating calculation;
- skill-dimension updates;
- Daily seed/version generation;
- streak boundaries;
- result serialization;
- local-save migrations;
- attempt state machine;
- idempotent completion;
- reward claim state;
- creator validation;
- content hashes.

Run generator tests across many seeds and difficulty levels. No generated puzzle may:

- have zero valid solutions;
- have multiple accepted answers when exactly one is expected;
- contain invalid coordinates;
- create unreachable states;
- produce an answer option duplicated under equivalent transforms;
- exceed declared complexity limits.

### Client tests

- EditMode tests for domain and mapping logic;
- PlayMode tests for the complete Daily route;
- interaction tests for Spatial touch gestures;
- layout screenshots or visual checks at representative aspect ratios;
- pause/resume and interrupted-session behavior;
- deep-link routing;
- mock wallet success, cancel, unsupported, and error paths;
- offline-to-online transitions where supported.

### Backend tests

- endpoint schema validation;
- authentication/wallet nonce verification;
- attempt start/complete state transitions;
- duplicate/idempotent requests;
- invalid/expired sessions;
- server-side solution validation;
- leaderboard ordering and tie behavior;
- authorization and row-level security;
- rate-limit behavior;
- reward allocation generation.

### Onchain tests

Test every security-relevant Challenge Pool transition, including happy path and adversarial cases listed in Section 16.

### Build verification

For every implementation phase:

1. compile;
2. fix all compile errors;
3. run relevant tests;
4. verify serialized Unity references/scenes;
5. run the smallest relevant product flow;
6. update documentation and state;
7. record exactly what was and was not verified.

An Android APK/AAB existing on disk is not sufficient verification; install and run it on an emulator or physical device when available.

---

## 24. Security and integrity

- never commit credentials, private keys, seed phrases, service-role keys, keystores, or private RPC URLs;
- provide `.env.example` files with names only and safe placeholders;
- make production secrets server-side;
- validate every untrusted payload server-side;
- use parameterized database access;
- apply least-privilege row-level security;
- treat deep links and creator content as untrusted;
- bound procedural inputs to prevent resource exhaustion;
- sanitize display names and text;
- make claims replay-safe;
- document program authorities and operational key handling;
- run secret scanning if tooling is available;
- record prototype trust assumptions in `docs/SECURITY.md`.

Do not market the prototype as cheat-proof, trustless, or fully decentralized. State its actual guarantees.

---

## 25. Delivery phases

### Phase 0 — discovery and executable plan

Deliver:

- repository/toolchain inventory;
- visual-reference inspection;
- architecture and dependency decisions;
- P0/P1 backlog with risk order;
- build/test commands;
- initial living documentation;
- explicit blockers and available mocks.

Exit criteria:

- the current project can be opened/compiled or the exact missing prerequisite is proven;
- no user files or changes were lost;
- the next implementation step is unambiguous.

### Phase 1 — visual vertical slice

Deliver:

- portrait application shell;
- responsive Home screen;
- profile header;
- `DAILY` title;
- isometric teal/blue board with yellow cubes;
- `BEGIN` action;
- bottom navigation;
- theme, typography, safe-area, motion, and haptic foundations;
- one tap-through into a puzzle host;
- Android build.

Exit criteria:

- composition closely matches the references at multiple aspect ratios;
- no stock-looking Unity UI remains in the primary view;
- primary action is understood within five seconds;
- Android build installs/launches where tooling permits.

### Phase 2 — flagship Spatial puzzle

Deliver the deterministic Spatial domain, renderer, touch controls, solver/validator, score, feedback, and multi-seed tests.

Exit criteria: a player can launch from Home, understand the mechanic without a manual, complete it on Android, receive validated feedback, and replay deterministic variants.

### Phase 3 — Pattern and Logic

Deliver one polished generator/renderer/controller/validator for each, including uniqueness tests and concise onboarding.

Exit criteria: each mechanic is reliably solvable, visually coherent, and passes multi-seed invariants.

### Phase 4 — complete Daily retention loop

Deliver three-trial orchestration, results, rating, skill dimensions, XP/level if retained, streak, persistence, share card, and analytics.

Exit criteria: the complete Daily flow works from fresh install through result and restart, with deterministic calculations and no blocking account/wallet step.

### Phase 5 — server-authoritative backend

Deliver migrations, functions/API, typed Unity client, anonymous identity, attempt sessions, server validation, leaderboards, local/live repositories, and backend tests.

Exit criteria: an online Daily uses a server-issued session and server-computed result; local mode remains functional.

### Phase 6 — social challenge loop

Deliver friend challenge creation, opaque link token, deep-link routing, transformed equivalent variant, versus result, and tracking.

Exit criteria: one device/session can create a challenge and another can open and complete it through the documented development or deployed link path.

### Phase 7 — World Boss

Deliver one polished event, event metadata, server validation, Boss leaderboard, and sponsored reward presentation.

Exit criteria: the Boss is clearly distinct, playable, measurable, and uses no hardcoded fake production claims.

### Phase 8 — wallet and Challenge Pool

Deliver mock and Mobile Wallet Adapter services, wallet linking, program/tests, Devnet deployment instructions/state, pool funding/finalization/claim path, and explorer references in internal demo configuration.

Exit criteria: Android can connect a compatible wallet and complete a verified Devnet claim, or the exact externally blocked step is documented while all program/client tests pass.

### Phase 9 — creator proof

Deliver the constrained Spatial editor, validation, test-play, publish, detail page, and creator metrics.

Exit criteria: a user can make a valid puzzle, test it, publish it, and open/play the stored version; invalid content cannot publish.

### Phase 10 — submission polish and evidence

Deliver:

- device/profile optimization;
- accessibility and failure-state pass;
- production-safe configuration;
- demo dataset and one Human vs AI benchmark if honestly available;
- `docs/DEMO_SCRIPT.md` for a sub-three-minute video;
- screenshots and store/submission assets where requested;
- README setup, architecture, test, backend, program, and Android build instructions;
- release build and final verification matrix;
- metrics dashboard/export procedure for real testing.

Exit criteria: a new evaluator can install/open the app, understand it immediately, complete the core loop, see the real Solana integration, and understand the startup thesis in under three minutes.

---

## 26. Priority and time-pressure rules

If schedule risk appears, preserve scope in this order:

1. visual quality and first five seconds;
2. complete Daily session;
3. reliable deterministic puzzles;
4. rating, result, and streak;
5. real server validation;
6. real Devnet Challenge Pool claim;
7. share and friend challenge;
8. one World Boss;
9. minimal Creator proof;
10. Human vs AI benchmark.

Do not cut invisible foundations that protect correctness, security, or data integrity. Instead reduce breadth, content count, decorative screens, and optional transitions.

If a P0 feature must be deferred, record:

- why;
- evidence of the blocker;
- smallest safe fallback;
- user-visible consequence;
- exact completion step;
- whether the demo remains truthful.

---

## 27. Definition of done

The hackathon MVP is done only when the repository contains a working product that:

1. launches in portrait on Android;
2. presents a polished Home screen faithful to the reference direction;
3. lets an anonymous player complete Pattern, Spatial, and Logic trials;
4. calculates and persists a transparent result, rating, skill changes, and streak;
5. generates a safe share card;
6. creates and resolves a friend challenge;
7. displays working daily/global rankings;
8. offers one complete Boss event;
9. connects a compatible Android wallet through Mobile Wallet Adapter;
10. demonstrates a sponsor-funded Devnet Challenge Pool and replay-safe claim;
11. validates competitive attempts on the server;
12. supports full local development without backend/wallet access;
13. lets a creator build, validate, test, publish, and replay one Spatial puzzle type;
14. records useful privacy-safe product analytics;
15. passes relevant automated tests;
16. installs and runs as an Android build;
17. contains accurate setup, architecture, security, build, demo, and known-risk documentation;
18. contains no secrets or knowingly false production claims.

---

## 28. Agent working protocol

For each phase:

1. state the phase goal in `docs/STATE.md`;
2. inspect before editing;
3. implement the smallest complete vertical behavior;
4. compile early;
5. run focused tests;
6. fix discovered regressions before expanding;
7. visually inspect user-facing work;
8. test failure and cancellation states;
9. update docs and verification evidence;
10. report results and continue to the next unblocked phase.

Do not:

- stop after writing a plan;
- leave the primary path as TODOs or empty buttons;
- substitute static screenshots for working UI;
- claim a mock is live infrastructure;
- silently remove hard features;
- add major dependencies without checking maintenance, license, size, and platform support;
- rewrite user-owned work unnecessarily;
- make broad destructive repository changes;
- wait for approval on obvious reversible details;
- polish secondary screens while the Daily loop is incomplete.

At the end of every phase, report:

```text
PHASE
Status: complete / partial / blocked

Implemented
- ...

Files changed
- ...

Verification
- command/test/device: result

Known risks
- ...

Next action
- ...
```

Keep `docs/STATE.md` concise enough that a new Codex/Claude session can resume without rereading the entire history. Record durable architectural decisions in `docs/ARCHITECTURE.md`, not only in chat.

---

## 29. Submission demo target

Design the product so the final demo can be one continuous interaction:

**0:00–0:15 — thesis**

> Static puzzle apps were built for a world before generative AI. RONRIKU is a competitive network for human reasoning.

**0:15–0:35 — Home**

Open the polished app. Show `DAILY`. Tap `BEGIN`.

**0:35–1:05 — three trials**

Solve Pattern, Spatial, and Logic, giving Spatial the strongest visual moment.

**1:05–1:25 — identity and retention**

Show completion time, top percentile, rating increase, skill profile, streak, and share/friend challenge.

**1:25–1:50 — sponsored Boss and Solana**

Show the Boss reward pool, wallet connection only when needed, transparent Devnet pool, and eligible claim.

**1:50–2:15 — Creator**

Place cubes, validate, test, publish, and show creator/plays/difficulty.

**2:15–2:40 — real evidence**

Show honest current player, completion, retention, puzzle, creator, or transaction metrics. Never invent traction.

**2:40–3:00 — company**

> We are starting with a daily mobile game. We are building the competitive network for human reasoning.

Every implementation decision should make this demonstration clearer, more truthful, or more impressive.

---

## 30. Start now

Begin with repository inspection and Phase 0. Then implement Phase 1 without waiting for another prompt.

The first milestone is not “architecture complete.” It is a **beautiful Android Home screen leading into one genuinely playable, deterministic Spatial trial**.
