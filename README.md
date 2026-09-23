# RONRIKU

RONRIKU is a portrait mobile reasoning game built with Unity for Android. The current repository contains the Phase 1 visual vertical slice: a responsive Daily home screen that opens a deterministic spatial puzzle host.

## Toolchain

- Unity `6000.6.0f1`
- Android Build Support from the Unity installation
- Package: `com.ronriku.game`
- Portrait orientation

## Verify

```powershell
.\scripts\unity-verify.ps1
```

## Build Android

```powershell
.\scripts\unity-build-android.ps1
```

The APK is written to `Builds/Android/RONRIKU.apk`. Installation requires a connected Android device or emulator:

```powershell
& 'D:\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe' install -r '.\Builds\Android\RONRIKU.apk'
```

See [docs/STATE.md](docs/STATE.md) for verified status and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for design decisions.
