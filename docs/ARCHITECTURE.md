# Architecture

Updated: 2026-09-13

## Decisions

### Client and version

The repository was empty, so there was no established Unity version to preserve. The installed and runnable editor is Unity `6000.6.0f1` at `D:\6000.6.0f1\Editor\Unity.exe`; the project is pinned to it. Unity 6.3 is the current LTS line, but selecting the locally installed 6.6 supported release avoids an unverified multi-gigabyte installation and lets the Android build be proved on this machine. Re-evaluate before production lock.

The client uses a single reusable scene and routed views. Runtime UI is constructed from small presentation components, while puzzle data and validation remain plain C#. This prevents scene-per-puzzle coupling and makes deterministic tests possible.

### Rendering and UI

Phase 1 uses the built-in render pipeline and uGUI primitives to minimize import and Android build risk. The isometric preview uses flat vector-style UI geometry with deterministic projection, which preserves the reference composition at all portrait ratios. A later Spatial renderer may use an orthographic 3D camera behind the same host boundary.

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

## Content and state rules

- Logical seed and presentation variant seed are separate.
- Rules changes require an explicit `rulesVersion`.
- Puzzle validators consume domain data, never presentation state.
- Persistent data starts with a schema version and migrations.
- Competitive outcomes are server-authoritative when online; local results are labeled local.

## Build flow

`RonrikuBuild` creates/updates the boot scene, configures portrait Android settings, runs verification, and builds the APK. This removes fragile manual scene wiring while still producing ordinary serialized Unity assets.
