# Architecture

Updated: 2026-09-23

## Decisions

### Client and version

The repository was empty, so there was no established Unity version to preserve. The installed and runnable editor is Unity `6000.6.0f1` at `D:\6000.6.0f1\Editor\Unity.exe`; the project is pinned to it. Unity 6.3 is the current LTS line, but selecting the locally installed 6.6 supported release avoids an unverified multi-gigabyte installation and lets the Android build be proved on this machine. Re-evaluate before production lock.

The client uses a single reusable scene and routed views. Runtime UI is constructed from small presentation components, while puzzle data and validation remain plain C#. This prevents scene-per-puzzle coupling and makes deterministic tests possible.

### Rendering and UI

The client uses the built-in render pipeline and UI Toolkit (runtime `UIDocument` + `PanelSettings`, 1080×2400 reference, match 0.5). All screens, including the isometric Spatial board, are drawn with `Painter2D` vector geometry: no 3D camera, no materials, one UI draw path. This keeps draw calls and APK size low and makes offscreen capture trivial. The spec asks for URP; it is not needed for the flat look and is deferred unless a 3D camera renderer is introduced.

Typography uses an in-project procedural 5×7 pixel glyph renderer, avoiding an unlicensed font dependency and eliminating stock Unity text from the primary view. The palette and spacing live in `RonrikuTheme`.

### Application boundaries

- `Application`: routing and session orchestration.
- `Domain`: Unity-free deterministic puzzle metadata, generation, answers, and validation.
- `Infrastructure`: analytics, persistence, sharing, deep links, API, and Solana adapters.
- `Presentation`: screens and reusable visual/input components.
- `Composition`: explicit bootstrapping and dependency wiring.

No service locator or dependency-injection framework is used. Services are passed through a small composition root.

### Input, motion, accessibility, and haptics

The client pins Unity's official `com.unity.inputsystem` package at `1.20.0`, the registry's current release compatible with Unity 6000.x. The shell uses pointer/touch-safe buttons with 48 px minimum targets. Safe-area padding is recalculated from `Screen.safeArea`. Motion is centralized and can be reduced to instant state changes. Haptics route through `IHapticsService`; the initial implementation is silent in Editor and calls the Android vibration hook only when enabled and supported.

### Backend and Solana

No backend or Solana package is pinned in Phase 1. Their interfaces will be introduced before implementation, after current maintenance, Android compatibility, license, and official documentation are verified. Gameplay remains anonymous and local until then. Planned backend: Supabase/PostgreSQL behind typed repositories. Planned chain: Solana Devnet Challenge Pool; gameplay and rating stay offchain.

## Spatial trial: Shadow Match (rules version 2)

- Structure: 4 / 5 / 6 face-connected cubes (Easy / Standard / Hard) grown deterministically inside a 3×3×3 box.
- State: one of 24 cube orientations (`CubeOrientations`, BFS-indexed from identity; rotations act about box centre (1,1,1) so cells stay in-box).
- Moves: `TurnLeft`/`TurnRight` (±90° about Z, vertical) and `TipBack`/`TipForward` (±90° about Y). Z + Y generate all 24 rotations.
- Goal: top-down shadow (9-bit mask, bit `x + 3y`) equals the target mask. Any orientation casting the target shadow is accepted.
- Generation constraints: start is unsolved; ≥6 distinct shadows across orientations; ≤4 orientations cast the target; par within 1–2 / 2–3 / 3–5; Standard and Hard targets cannot be reached by turning alone. Up to 512 deterministic attempts, otherwise the generator throws.
- Validation: the client submits the move list; `SpatialPuzzleValidator` replays it from the issued start (max 64 moves). This is the same contract the server will use.
- Score: `max(100, 700 + 300·difficulty + max(0, targetMs − elapsedMs)/100 − 60·max(0, moves − par) − 120·resets)`, target 30/45/60 s.
- Presentation: the structure is rotated by quaternion slerp (160 ms ease-out), then dropped so its lowest cube rests on the floor. Target tiles are teal; the current shadow is overlaid. Mini-maps show target vs current shadow with filled/outlined cells.

## Content and state rules

- Logical seed and presentation variant seed are separate.
- Rules changes require an explicit `rulesVersion`.
- Puzzle validators consume domain data, never presentation state.
- Persistent data starts with a schema version and migrations.
- Competitive outcomes are server-authoritative when online; local results are labeled local.

## Build flow

`RonrikuBuild` creates/updates the boot scene, configures portrait Android settings, runs verification, and builds the APK. This removes fragile manual scene wiring while still producing ordinary serialized Unity assets.
