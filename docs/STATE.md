# Current State

Updated: 2026-09-24

## Active phase

Phase 2 — flagship Spatial puzzle (Shadow Match). Next: Phase 4-first Daily loop (see Next action).

## Phase 0 — complete

Repository, toolchain (Unity `6000.6.0f1` at `D:\6000.6.0f1`), architecture, risks, and build scripts established. Git initialised 2026-09-23.

## Phase 1 — complete with caveats

Home shell, pixel `DAILY` title, isometric board, BEGIN, five-tab nav, safe area, theme, analytics and haptic boundaries, Android APK.

Caveats
- Home still shows hardcoded placeholder identity/metadata (`NAVAL`, `LEVEL 131`, `1821`, `3,842 ACTIVE`, `RESETS 07:18`, `DAILY 024`). Must be replaced by local profile + date-derived Daily before any demo.
- Home layout leaves large empty bands above `DAILY` and below `BEGIN` versus `docs/reference/home-v2.png`.
- BOSSES / CREATE / RANK / PROFILE tabs are disabled no-ops.

## Phase 2 — Shadow Match (in progress)

Mechanic
- A rigid 4–6 cube structure sits in a 3×3×3 box above a 3×3 floor. Teal floor tiles are the target shadow.
- Player moves: TURN left/right (90° about vertical) and TIP back/forward (90° about the floor axis). Swipe horizontally to turn, vertically to tip; buttons mirror both.
- Solved when the top-down shadow covers exactly the teal tiles. A BFS solver gives par; the score rewards reaching par.
- Rules version 2. Details in `docs/ARCHITECTURE.md`.

Implemented
- `CubeOrientations` (24 rotations + transition table), `SpatialPuzzleGenerator`, `SpatialPuzzleSolver`, move-replay `SpatialPuzzleValidator`, `SpatialPuzzleInvariants`, par-based `SpatialPuzzleScorer`.
- Renderer: quaternion-slerped rotation, structure drops to rest on floor, shadow overlay, floor slab.
- `ShadowGridElement` target/current mini-maps (filled vs outlined cells, not colour-only).
- Auto-detect solve after each settled move; CONTINUE returns to Home and emits `puzzle_solved`.
- Android haptics: short `VibrationEffect` one-shots with `Handheld.Vibrate` fallback.

Verification (2026-09-23)
- EditMode: 7/7 passed — group closure, in-box rotation, determinism (1,500 seed×difficulty cases), invariants (1,500 cases: connected, in-box, unsolved start, solver par == stored par within difficulty range, validator accepts solver path, ≤4 matching orientations), variety, validator rejection cases, scorer.
- PlayMode: 2/2 passed — Home → Spatial route; solver path clicked through real buttons reveals CONTINUE and returns Home.
- Capture: `docs/evidence/{narrow-16x9,seeker-20x9,tall-22x9}-{1-home,2-spatial-start,3-spatial-move,4-spatial-solved}.png` rendered offscreen in batchmode and inspected.
- Android build: passed 2026-09-23, `Builds/Android/RONRIKU.apk` 42,169,768 bytes, SHA-256 `cecf2a3da52a14bfc0f1ec1ff1cbb1693226c26d42ee7e784fab4a562c39eb02`. `RonrikuBuild.Verify` ran the same 1,500-case invariant sweep.
- Not verified: physical device install/launch, touch swipe feel, haptic feel, FPS. No AVD or device is attached (`adb devices` empty, `~/.android/avd` empty).

Known gaps
- `variantSeed` is recorded in metadata and hash but does not yet transform presentation.
- No onboarding beyond the instruction line; first-time players may need a one-move demo.
- Board leaves a gap above short structures on tall screens.

## Phase 4 (pulled forward) — Daily loop

Implemented
- `DailyCalendar` (UTC day number from 2026-09-01, reset countdown, challenge id, seed) and `DailyPlan` (3 trials; currently Spatial Easy/Standard/Hard until Pattern and Logic ship).
- `DailySession` trial orchestration; `TrialOutcome` (no solution data).
- `ReasoningRating`: bounded Elo-style update, formula and constants documented in `Rating.cs`; per-skill ratings (Spatial, Planning, Speed so far).
- `PlayerProfile` (schema v1, migration, history) and `DailyCompletion`: first completion per UTC day counts; replays and earlier days are practice; streak consecutive/gap/rewind rules.
- `JsonFileProfileRepository`: atomic write, corrupt-file quarantine.
- `RuntimeConfig`: central environment, profile directory and UTC override.
- Home uses the real profile (anonymous `PLAYER XXXX`), live reset countdown, streak, PLAY AGAIN after completion. All hardcoded placeholder data removed.
- Trials: header `TRIAL n / 3`, live timer, SKIP TRIAL after the time limit.
- Results: time, solved/points, LOCAL MODE instead of a fake percentile, rating count-up, streak, per-trial rows, skill deltas.

Verification (2026-09-24, in the open editor via Unity CLI Pipeline)
- EditMode 17/17; PlayMode 2/2 (full Daily from fresh profile on a fixed date → results → Home shows new rating, STREAK 1, PLAY AGAIN; survives scene reload).
- Capture: `docs/evidence/*-{1-home,2-trial-start,3-trial-solved,4-results,5-home-after}.png` at three aspect ratios.
- Calibration fix found by tests: trial difficulty ratings moved to 1100/1300/1500 so weak sessions lose rating.
- Device (Pixel 6a, 1080×2400, 2026-09-24): fresh install → Home (anonymous profile, DAILY 023, UTC countdown) → three trials (trial 1 via real swipe-down = TIP FORWARD, trials 2–3 via buttons) → results 1200 → 1223, STREAK 1 → force-stop + relaunch shows 1223, STREAK 1, PLAY AGAIN. `profile.json` on device matches. Evidence in `artifacts/device/` (not committed).
- Development APK is 71.5 MB: the Unity Pipeline package (editor automation, includes Roslyn) compiles into players only when `DEVELOPMENT_BUILD` is defined. Release builds exclude it.
- Harmless log on device: Unity probes `com.google.android.play.core.assetpacks.AssetPackManager` (Play Asset Delivery not used).

## Phase 3 — Pattern and Logic

Implemented
- Pattern: 4×4 grids, rules rotate/mirror (Easy), + transpose/invert/shift (Standard), ordered pairs (Hard). Every single and pair hypothesis consistent with the examples must give the same test output (answer forced); decoys are outputs of other plausible rules. One choice, correct option position spread.
- Logic ("LINK"): N×N (4/5/6) one-stroke path through every cell, numbers in order. Random Hamiltonian path via backbite moves; a pruned DFS solver adds clues until the solution is proven unique. Easy adds numbers only, Standard alternates walls and numbers, Hard prefers walls (~2 numbers + ~9 walls on 6×6).
- Validators replay submitted answers (option index / cell path). `TrialScoring` shared points formula; per-difficulty targets and time limits.
- `TrialScreenBase` shared chrome (timer, SKIP after limit, CONTINUE); `PatternPuzzleScreen`; `LogicPuzzleScreen` (drag to draw, drag back to retract, tap to cut, undo/reset; walls and out-of-order numbers blocked).
- Daily rules v2: Pattern → Spatial → Logic, all Standard.

Verification (2026-09-24)
- EditMode 23/23 (Pattern bijective rules, 900 pattern puzzles valid + forced + unique hashes, 280 logic puzzles unique-solution, validator rejection cases, 120 Dailies generate valid trials of each kind).
- PlayMode 2/2: full Pattern → Spatial → Logic Daily through the UI; profile persists.
- Capture: `docs/evidence/*-2-trial{1,2,3}-start.png`, `*-3-trial{1,2,3}-solved.png`.
- Android build succeeded and installed on the Pixel 6a; hands-on device play of the new trials not yet done (phone in use).
- Editor in-place APK updates leave ~39 MB of dead space between zip entries (93 MB file, 54 MB of entries); clean builds do not.

## Next action

1. Hands-on Pixel 6a pass of Pattern and Link (drag feel, cell hit size on 6×6).
2. First-run flow (THREE TESTS. ONE MIND. EVERY DAY.) and one-line onboarding per trial.
3. Home visual pass against `home-v2.png`.
4. Share card (spec §12) and friend-challenge link contract.
5. Backend (Supabase) and Mobile Wallet Adapter per spec phases 5 and 8.

## Commands

```powershell
.\scripts\unity-test.ps1 -Platform EditMode
.\scripts\unity-test.ps1 -Platform PlayMode
.\scripts\unity-test.ps1 -Platform PlayMode -Category Capture   # writes docs/evidence PNGs
.\scripts\unity-verify.ps1
.\scripts\unity-build-android.ps1
```

Logs and test results go to `artifacts/logs/` (git-ignored). Unity must not have the project open during batchmode runs.

With the editor open, use the Unity CLI instead (Pipeline package):

```bash
unity command run_tests --mode EditMode --timeout 300
unity command run_tests --mode PlayMode --filter Uncategorized --filter_type category --async_tests true   # then: unity command test_status
unity command build --target Android --outputPath <repo>/Builds/Android/RONRIKU.apk --options '["Development"]' --confirm true   # then: unity command build_status
```
