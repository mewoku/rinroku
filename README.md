# RONRIKU

RONRIKU is a portrait mobile reasoning game for Android and the Solana Seeker, built with Unity. Each UTC day brings a Daily of three deterministic trials — Pattern, Spatial (Shadow Match) and Logic (Link) — scored into a persistent Reasoning Rating and streak. Everything currently runs locally; backend and wallet features are planned.

## Toolchain

- Unity `6000.6.0f1` (installed at `D:\6000.6.0f1`)
- Android Build Support from the Unity installation (IL2CPP, ARM64)
- Package `com.ronriku.game`, portrait, API 26+

## Project layout

```
client/                     Unity project
  Assets/Ronriku/Scripts/
    Domain/                 plain C#: puzzles, Daily plan, rating, profile
    Infrastructure/         persistence, analytics
    Presentation/           UI Toolkit screens and components
    Composition/            bootstrap and runtime config
  Assets/Ronriku/Tests/     EditMode (domain) and PlayMode (routes, capture)
docs/                       state, architecture, risks, evidence screenshots
scripts/                    batchmode test/verify/build helpers
```

The single scene `Bootstrap.unity` holds only a camera and the `RONRIKU` object. All screens are built in code at runtime, so the Scene view looks empty until you press Play.

## Test

With the editor closed (batchmode):

```powershell
.\scripts\unity-test.ps1 -Platform EditMode
.\scripts\unity-test.ps1 -Platform PlayMode
.\scripts\unity-test.ps1 -Platform PlayMode -Category Capture   # writes docs/evidence/*.png
```

With the editor open, use the Unity CLI (Pipeline package):

```bash
unity command run_tests --mode EditMode --timeout 600
unity command run_tests --mode PlayMode --filter Uncategorized --filter_type category --async_tests true
unity command test_status
```

## Build

Menu **RONRIKU → Build Android (Release)** or `.\scripts\unity-build-android.ps1` writes a fresh `Builds/Android/RONRIKU.apk` (about 16 MB) plus `RONRIKU_mapping.txt` for de-obfuscating Java stack traces. **Build Android (Development)** writes `RONRIKU-dev.apk` with profiler support and the editor automation package.

Every build first checks that the next 60 Dailies generate valid trials.

```powershell
& 'D:\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe' install -r '.\Builds\Android\RONRIKU.apk'
```

See [docs/STATE.md](docs/STATE.md) for verified status, [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for design decisions and [docs/RISKS.md](docs/RISKS.md) for known risks.
