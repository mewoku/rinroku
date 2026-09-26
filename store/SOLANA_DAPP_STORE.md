# Publishing ODLET on the Solana dApp Store

Checked 2026-09-26 against docs.solanamobile.com and `@solana-mobile/dapp-store-cli@1.0.1`.

**What changed:** the old config-driven CLI flow (`dapp-store init`, `create publisher|app|release`,
`validate`, `publish submit`) is **gone**. Today:

1. **First release** is created in the web **Publisher Portal** — <https://publish.solanamobile.com>.
   The portal mints the Publisher / App / Release NFTs and uploads the media and APK to Arweave for you;
   you sign the transactions with a browser wallet.
2. **Later versions** can be pushed from the terminal with the CLI (`dapp-store --apk-file ...`), which
   matches the APK's package name to the existing app.

`store/dapp-store/config.yaml` holds every listing field in the legacy schema as the one place to copy
from; the CLI no longer reads it.

## What the owner needs

| Item | Details |
|---|---|
| Publisher Portal account | Sign up at publish.solanamobile.com, fill the publisher profile, **complete KYC/KYB** (identity check; allow time — it can take longer than the review itself). |
| Publisher wallet | A browser wallet (Phantom, Solflare or Backpack) on **mainnet**. Publisher/App/Release NFTs are minted on mainnet — devnet SOL does not work. |
| SOL | Solana Mobile says **~0.2 SOL** in the publisher wallet for transaction fees + storage uploads. The CLI refuses to start below **0.016 SOL** on the signer. Budget **0.25 SOL**; each later release costs a fraction of that. |
| Storage (ArDrive) | The portal recommends ArDrive for Arweave uploads; use its cost estimator (APK ≈ 21 MB + ~6 MB media) and top up the ArDrive balance before submitting. |
| Signed release APK | Release build signed with **your own key** — debug-signed builds are rejected. The key must **not** be a Google Play signing key (the dApp Store rejects APKs signed by an existing Play key). See "Release signing" below. |
| Public HTTPS URLs | https://odlet.xyz, https://odlet.xyz/privacy and https://odlet.xyz/terms must be live (deploy `deploy/` with `DOMAIN=odlet.xyz`). Localhost links will fail review. |
| Contact emails | Publisher + support email: `hello@odlet.xyz` (placeholder: create the mailbox first; also set `CONTACT_EMAIL` in `deploy/.env` so `/privacy` shows it). |
| Media | `store/media/` — see `store/listing.md` media table (icon 512², banner 1200×600, feature 1200², ≥4 screenshots ≥1080 px, same orientation). |

## Step by step (first release)

1. **Create the signing key** (once, keep forever):
   ```powershell
   powershell -ExecutionPolicy Bypass -File store\make-keystore.ps1
   ```
   It writes `%USERPROFILE%\ronriku-keys\ronriku-dappstore.keystore` (outside the repo), prompts for the
   password and prints the SHA-256 fingerprint. **Back up the file and the password now.**

2. **Pick the backend mode for this release** (see RELEASE_CHECKLIST.md §2): production HTTPS URL in
   `client/Assets/Ronriku/Resources/ronriku-online.json`, or `"url": ""` for an offline-only release.
   Never ship `http://127.0.0.1:54321`.

3. **Build the signed APK.** Unity reads the signing variables **when the build runs**, from the
   environment of the Unity process. Close the editor, then from PowerShell:
   ```powershell
   $env:RONRIKU_KEYSTORE      = "$HOME\ronriku-keys\ronriku-dappstore.keystore"
   $env:RONRIKU_KEY_ALIAS     = "ronriku"
   $env:RONRIKU_KEYSTORE_PASS = Read-Host "keystore password"
   $env:RONRIKU_KEY_PASS      = $env:RONRIKU_KEYSTORE_PASS      # PKCS12: same password
   & "D:\6000.6.0f1\Editor\Unity.exe" -batchmode -quit -projectPath "$PWD\client" `
       -executeMethod Ronriku.Editor.RonrikuBuild.BuildAndroidRelease -logFile "$PWD\Builds\android-release.log"
   Select-String -Path Builds\android-release.log -Pattern "RONRIKU_ANDROID_BUILD_OK|error"
   ```
   (Or launch the editor from that same shell and use **RONRIKU → Build Android (Release)**.) The build
   applies the icons (`RonrikuIcon.TryApply`) and the signing config automatically.

4. **Verify the APK is release-signed** with your key, not "Android Debug":
   ```powershell
   $env:JAVA_HOME = "D:\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK"
   $bt = "D:\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\36.0.0"
   & "$bt\apksigner.bat" verify --print-certs Builds\Android\ODLET.apk     # DN must NOT be CN=Android Debug
   & "$bt\aapt2.exe" dump badging Builds\Android\ODLET.apk | Select-String "package:|targetSdk|uses-permission|native-code"
   ```
   Expect `package: name='com.odlet.game' versionCode='1' versionName='1.0'`, targetSdk 36,
   permissions INTERNET + VIBRATE only, `native-code: 'arm64-v8a'`.

5. **Install it on the Seeker/Pixel and smoke-test** (`adb install -r Builds\Android\ODLET.apk`):
   launch, W1 L1 battle, Daily, airplane mode (offline), relaunch. Reviewers do exactly this.

6. **Publisher Portal:** sign in → complete profile + KYC/KYB → connect the publisher wallet (≥0.25 SOL)
   → set up storage (ArDrive, top up) → **Add a dApp → New dApp**: paste fields from
   `store/listing.md` / `config.yaml` (name `ODLET`, package `com.odlet.game`, category Games, short
   description ≤30 chars, long description, URLs, testing instructions) and upload media.

7. **New Version** → upload `Builds/Android/ODLET.apk`, "what's new" text → sign the prompted
   messages/transactions (Arweave uploads + App/Release NFT mints).

8. **Review:** the app enters the queue automatically. Results come by email from
   `publishersupport@dappstore.solanamobile.com` within **3–5 business days** (third-party guides say
   2–5). On approval it goes live immediately. **It cannot be live "this morning"**; submitting this
   morning is realistic if KYC is already done.

## Later versions (CLI)

```powershell
npm install -g @solana-mobile/dapp-store-cli          # Node >= 18
# 1. Bump versionCode (and versionName) — see RELEASE_CHECKLIST.md §1 — and rebuild signed with the SAME key.
# 2. Create an API key: Publisher Portal → Settings → API keys.
$env:DAPP_STORE_API_KEY = Read-Host "portal API key"
dapp-store --apk-file Builds\Android\ODLET.apk --keypair "$HOME\ronriku-keys\publisher.json" --whats-new "Bug fixes and balance"
# interrupted? dapp-store resume --release-id <id> --keypair ...
```

`--keypair` is a Solana CLI keypair file that signs and pays (≥0.016 SOL on mainnet). Use the publisher
wallet's key (if your publisher wallet lives only in a browser extension, create a file wallet with
`solana-keygen new -o $HOME\ronriku-keys\publisher.json`, import it into the extension and register
*that* as the publisher wallet — confirm in the portal which signer it accepts). Keep keypairs and the
API key out of the repo.

Rules for every update: same package `com.odlet.game`, **same signing key**, **higher versionCode**.

## Release signing — reference

- Script: `store/make-keystore.ps1` (keytool from Unity's bundled OpenJDK at
  `D:\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe`, RSA 4096,
  PKCS12, 10,000-day validity, alias `ronriku`; passwords passed via `:env`, never on the command line).
- `client/Assets/Ronriku/Editor/RonrikuBuild.cs` → `ConfigureSigning()` reads:
  `RONRIKU_KEYSTORE` (path; must exist, else the build silently falls back to **debug signing**),
  `RONRIKU_KEYSTORE_PASS`, `RONRIKU_KEY_ALIAS` (default `ronriku`), `RONRIKU_KEY_PASS`.
- Unity never serialises the keystore passwords, but it does save `AndroidUseCustomKeystore` and
  `AndroidKeystoreName` (your local path) into `ProjectSettings.asset` after a signed build. Don't commit
  that hunk (`git checkout -p client/ProjectSettings/ProjectSettings.asset`).
- Where to keep it: outside the repo (`%USERPROFILE%\ronriku-keys\`), plus a password-manager entry
  (file as attachment + password) and one offline copy (USB drive). Losing it = you can never update the
  listing; you would have to publish a new app.
- Google Play later: do **not** reuse this key. Use Play App Signing with a separate upload key.

Sources: [dApp Store submission](https://docs.solanamobile.com/dapp-store/submit-new-app),
[publishing CLI](https://docs.solanamobile.com/dapp-store/publishing-cli),
[listing guidelines](https://docs.solanamobile.com/dapp-store/listing-page-guidelines),
[prepare](https://docs.solanamobile.com/dapp-publishing/prepare),
[Play → dApp Store key rule](https://docs.solanamobile.com/dapp-store/publishing-from-google-play),
[legacy config example](https://github.com/solana-mobile/dapp-publishing/blob/main/example/config.yaml),
[Blueshift guide (asset sizes)](https://learn.blueshift.gg/en/courses/dapp-store-publishing/solana-dapp-store).
