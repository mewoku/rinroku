# Current State

Updated: 2026-09-23

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
- Not verified: physical device install/launch, touch swipe feel, haptic feel, FPS. No AVD or device is attached (`adb devices` empty, `~/.android/avd` empty).

Known gaps
- `variantSeed` is recorded in metadata and hash but does not yet transform presentation.
- No onboarding beyond the instruction line; first-time players may need a one-move demo.
- Board leaves a gap above short structures on tall screens.

## Next action

1. Daily loop skeleton (priority 2 in spec §26): UTC-date seed, 3-trial orchestrator, results screen, local profile, rating, streak; remove placeholder Home data.
2. Pattern and Logic mechanics into the orchestrator.
3. Home visual pass against `home-v2.png`.
4. Attach a Seeker or create an AVD; verify install, swipe feel, haptics, FPS.

## Commands

```powershell
.\scripts\unity-test.ps1 -Platform EditMode
.\scripts\unity-test.ps1 -Platform PlayMode
.\scripts\unity-test.ps1 -Platform PlayMode -Category Capture   # writes docs/evidence PNGs
.\scripts\unity-verify.ps1
.\scripts\unity-build-android.ps1
```

Logs and test results go to `artifacts/logs/` (git-ignored). Unity must not have the project open during batchmode runs.
