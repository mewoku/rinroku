# RONRIKU release checklist (Android)

Facts below were checked on 2026-09-26 against the repo and the current `Builds/Android/RONRIKU.apk`
(built 2026-09-26 01:34). Tick every box before uploading.

## 0. Blockers found today

| # | Blocker | Fix |
|---|---|---|
| B1 | Current APK is **debug-signed** (`CN=Android Debug`). dApp Store rejects it. | `store/make-keystore.ps1`, then build with the `RONRIKU_*` env vars (SOLANA_DAPP_STORE.md step 3). |
| B2 | Backend is `http://127.0.0.1:54321` (adb reverse, dev only). For real users it silently stays offline, and it forces **cleartext HTTP on** in the manifest. | Pick one in §2 before building. |
| B3 | No public domain yet: `/privacy`, `/terms` and the website must be on public **HTTPS** for the listing. `deploy/.env` has `DOMAIN=:80`, `SITE_URL=http://localhost:8080`. | Deploy `deploy/` to the VPS with a hostname (§2). |
| B4 | Publisher Portal account, **KYC/KYB**, and a mainnet wallet with ~0.25 SOL. | Owner only. |
| B5 | Review takes **3–5 business days** → the app can be *submitted* this morning, not *live*. | — |
| B6 | v3 has **not been played on a device** yet (STATE.md open item 1). | 15-minute device pass (§5) before submitting. |

## 1. Versioning

Current: `bundleVersion: 1.0`, `AndroidBundleVersionCode: 1` (`client/ProjectSettings/ProjectSettings.asset`,
not overridden by `RonrikuBuild.cs`). APK reports `versionCode='1' versionName='1.0'`.

- [ ] First store release: keep **1 / 1.0**.
- [ ] Every later upload: **increase versionCode** (2, 3, …; stores reject equal/lower) and bump
      versionName (1.0.1, 1.1). Unity: Player Settings → Android → Other Settings → *Version* and
      *Bundle Version Code*, or edit the two lines in ProjectSettings.asset while the editor is closed.
- [ ] Update "what's new" in `store/listing.md` / `store/dapp-store/config.yaml`.
- [ ] Tag the commit: `git tag v1.0-android-1`.

## 2. Backend for real users (decide before building)

`client/Assets/Ronriku/Resources/ronriku-online.json` is baked into the APK.

- **Option A — online (recommended once the VPS is up):** deploy `deploy/` on the VPS with a real
  hostname (`DOMAIN=play.example.com`, `SITE_URL=https://play.example.com`, `BIND_ADDR=0.0.0.0`,
  ports 80/443, `CONTACT_EMAIL=...`; `deploy/up.sh`), plus STATE.md open item 5: non-default
  Supabase JWT secret, real domain in the wallet-link message, publisher cron running. Then set `"url": "https://play.example.com"` and the production
  `anonKey` in `ronriku-online.json`. `RonrikuBuild.BackendIsCleartext()` then turns
  `insecureHttpOption` **off** automatically. Test from a phone on mobile data (not Wi-Fi, no adb).
- **Option B — offline-only release (fastest for this morning):** set `"url": ""`. `OnlineService`
  then never connects, cleartext is **off**, everything plays locally (ratings shown as LOCAL). Adjust
  the "PLAYS OFFLINE" paragraph of the description (see `store/listing.md`). Ship online in 1.0.1.
- [ ] Never ship `http://127.0.0.1:54321`.
- [ ] `ronriku-online.json` only ever holds the public anon key — never the service-role key.

## 3. Build + artefact checks

- [ ] `python store/tools/make_art.py` (only if art changed) and **RONRIKU → Apply Icons** (the release
      build also calls it). Default icon + 6 adaptive densities; Unity 6.6 no longer has
      Legacy/Round Android icon slots (minSdk 26 ⇒ every device uses adaptive icons).
- [ ] Signed release build (SOLANA_DAPP_STORE.md step 3) → `RONRIKU_ANDROID_BUILD_OK` in the log.
- [ ] `apksigner verify --print-certs` shows **your** certificate.
- [ ] `aapt2 dump badging`: package `com.ronriku.game`, versionCode as intended, `targetSdkVersion:'36'`,
      `minSdkVersion 26`, `native-code: 'arm64-v8a'`.
- [ ] **Permissions**: exactly `INTERNET`, `VIBRATE`, and Android's auto-added
      `com.ronriku.game.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION`. (Confirmed on the current APK.) No
      location, storage, camera, mic, phone, accounts, or ad ID. The gravity sensor needs no permission.
      `AndroidManifestHardening.cs` only extends `configChanges` (crash workaround); it adds no permissions.
- [ ] 16 KB page-size alignment: `zipalign -c -P 16 -v 4 RONRIKU.apk` → "Verification successful"
      (passes today).
- [ ] Size: release APK ≈ 21 MB (ARM64 only, IL2CPP, stripping High, R8 minify). Keep
      `Builds/Android/RONRIKU_mapping.txt` for each release (deobfuscating Java traces).
- [ ] Not a development build (no "Development Build" watermark; `Debug.isDebugBuild` false ⇒ the local
      analytics logger drops events).

## 4. Settings already correct (verify nothing regressed)

- 64-bit only (ARM64), IL2CPP, min API 26, target API = highest installed (36). Google Play's target
  rule (currently API 35 for new apps/updates, moving to 36) is met.
- Portrait only; Unity splash off; Unity Analytics / Crash Reporting / Ads / Performance Reporting
  **disabled** (`UnityConnectSettings.asset`). No third-party SDKs in `Packages/manifest.json`.
- Stack traces: logs none, errors script-only.
- Android auto-backup is Unity's default (on). It backs up the local profile and session token to the
  user's own Google Drive — acceptable; mention nothing extra needed.

## 5. Crash / ANR risks (docs/RISKS.md) and device pass

- [ ] **Activity-recreate crash** (`UnityFoldingFeaturesWrapper.init() should be called only once`):
      seen 3/17 times only when launching within a second of `adb install -r`. Mitigated by
      `AndroidManifestHardening`. Test: install, wait 5 s, launch; rotate lock, dark-mode toggle, font
      size change, split-screen while running. If it crashes on normal launches, switch
      `PlayerSettings.Android.applicationEntry` to Activity and rebuild.
- [ ] Network timeouts: online calls time out after 6 s and fall back to offline — test in airplane
      mode and with the server down (no ANR, no spinner forever).
- [ ] Device pass on Pixel 6a / Seeker: cold start, W1 L1 Battle, a Rune Hand, Ice Dash, Beat Crawl
      (beat timing through the speaker), Daily, shop, ME settings, share button, background/foreground
      during music, 10 minutes of play for heat/frame-rate.
- [ ] `adb logcat -b crash` empty after the pass.

## 6. Store listing and policy

- [ ] Listing text + media from `store/listing.md`; screenshots still match the current UI.
- [ ] Privacy policy and terms live on HTTPS (`/privacy`, `/terms`) with the contact email set.
- [ ] Content rating: cartoon/fantasy violence only ⇒ PEGI 3–7 / ESRB E; no real-money purchases in
      the app; no chat.
- [ ] Landing page (`web/src/app/page.tsx`) links to `/privacy` and `/terms` in its footer (not added by
      me: that file was being edited by someone else). The app never claims wallet features it lacks
      (MWA in Unity is STATE.md open item 3).

### Solana dApp Store vs Google Play on NFTs / crypto

| | Solana dApp Store | Google Play |
|---|---|---|
| Crypto / NFT features | Allowed (payments, NFT minting/trading). Disclose key handling. | Allowed only under the *Blockchain-based content* policy: disclose tokenised assets in the listing/Data Safety; NFT sales must use Google Play Billing where they unlock in-app value; **no** gambling-like NFT mechanics, no promotion of earnings/"play to earn" claims. |
| RONRIKU today | App has no wallet; NFTs (devnet) are bought on the website only. Nothing to disclose beyond the description's honest note. | Same app is fine. Do **not** add in-app links that steer users to buy NFTs on the website (anti-steering). If MWA/NFT purchases are added in-app later, re-review the policy and billing rules first. |
| Signing | Own key, not a Play key. | Play App Signing; upload a separate **upload key**. Never reuse the dApp Store key. |
| Package format | APK | **AAB required** for new apps: set `EditorUserBuildSettings.buildAppBundle = true` for a Play build (not done in `RonrikuBuild.cs` today). |
| Accounts | Deletion by email is fine. | Apps that create accounts (anonymous accounts count once they persist server-side) must offer **in-app account deletion + a web deletion link**. Not implemented — needed before Play. |
| Data Safety form | — | Answers drafted in `store/privacy.md`. "Encrypted in transit" is only true with the HTTPS backend. |
| Testing | Review 3–5 business days | New personal developer accounts must run a **closed test with ≥12 testers for 14 days** before production. Plan ~3 weeks. |
| Target audience | — | Declare 13+ to stay out of the Families program. |

## 7. After publishing

- [ ] Watch the portal email / Play vitals for crashes (no crash SDK in the app — logcat reports from
      testers are the only signal; consider adding one later and updating the privacy policy).
- [ ] Back up: keystore, `Builds/Android/RONRIKU.apk` + mapping file for this version, git tag.
- [ ] Update `docs/STATE.md` with the release version and store status.
