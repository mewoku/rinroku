# Risks and Blockers

Updated: 2026-09-13

| Risk | Evidence | Fallback / mitigation | Completion step | User-visible consequence |
|---|---|---|---|---|
| Unity version is Supported 6.6 rather than LTS 6.3 | Only installed editor found is `D:\6000.6.0f1` | Pin exactly; avoid package churn; re-evaluate before production lock | Install and migrate to an agreed LTS after Phase 1 if required | None expected in demo; long-term support window differs |
| Android device/emulator unavailable | `adb devices -l` returned no targets; installed SDK has no AVD or system image | Produced and inspected APK; route is covered by PlayMode test | Connect a Seeker-class device or create an AVD, then install, launch, and capture | Device performance, cutouts, haptics, install, and launch remain unverified |
| Reference files arrived under UUID names | Root contained two PNGs and no `docs/reference` | Preserve originals and copy to canonical paths by visual role | None | None |
| Final audio and pixel font assets unavailable | Empty repository | Silent audio/haptic service and procedural pixel glyphs | License and import final assets during polish | Phase 1 has no final sound design |
| Backend, wallet, and Devnet credentials unavailable | No project configuration or secrets supplied | Keep local mode and typed boundaries; never fabricate live state | Configure Supabase, Android MWA, and Devnet keys in later phases | Phase 1 is explicitly local |
| Real device visual checks may differ from Editor capture | Cutouts, OEM scaling, and GPU drivers vary | Safe-area component and representative aspect screenshots | Run physical-device matrix | Minor spacing/performance issues may remain |

No original user file was modified or deleted. The UUID reference images remain at repository root.
