# Odlet privacy policy

Updated: 26 September 2026. Web version: `/privacy` on the Odlet site (source
`web/src/app/privacy/page.tsx`); keep both in sync. Store listings need the **public HTTPS URL** of the web
page: `https://odlet.xyz/privacy`.

Odlet is a puzzle game. It needs very little about you: no email, no real name, no phone number, no
location. There are no ads and no analytics or tracking SDKs in the app or on the website.

## In short

- The Android app works fully offline. Progress is saved on your device.
- When it can reach our server, the app creates an **anonymous account** (a random ID) so your progress,
  rating and figures are kept and ranked.
- A Solana **wallet address** is only stored if you choose to link a wallet on the website. We never see
  or store private keys or seed phrases.
- We do not sell data, show ads, or share data with advertisers or data brokers.

## Stored on your device

- Your local profile: progress, stars, times, streak, shards, figures, settings.
- A session token for the anonymous account, when online.
- Website: the browser keeps the sign-in session and a short list of paid-but-unconfirmed purchases.

Uninstalling the app or clearing its storage deletes this data.

## Stored on our server (when online)

- **Account:** random user ID from anonymous sign-in (Supabase Auth). No email or password.
- **Profile:** handle and display name (generated; changeable), avatar figure, rating, shards, streak,
  completed-daily count.
- **Game results:** level stars and best times, daily and boss results, and a compact record of answers
  and moves that the server replays to verify results.
- **Social and shop:** friend requests, owned figures, marketplace listings, shard/SOL transaction ledger.
- **Wallet (website, optional):** the public address linked by signing a one-time message, and purchase
  transaction signatures.

## Public to other players

Handle, display name, avatar figure, rating and streak (leaderboards, profile pages); friends see each
other. Blockchain activity (wallet address, transactions, NFTs on devnet) is public by nature and cannot
be deleted by us.

## Technical data

- The server sees IP addresses and request details, used only to deliver the service, rate-limit abuse
  and keep it secure. Rate limits are held in memory.
- Android permissions: `INTERNET`, `VIBRATE`. The motion sensor (parallax) is read on-device only.
- Unity Analytics, Unity crash reporting, Unity Ads and all third-party analytics are off.

## Processors

- Hosting provider running the server and database (Supabase, self-hosted or hosted).
- Website wallet features only: your wallet app and a Solana RPC provider (they receive transactions and
  your IP under their own policies). NFTs are minted on Solana **devnet** (no real money).

## Retention and deletion

Account data is kept while the account exists. To delete the online account and everything linked to
it, email **hello@odlet.xyz** with your player handle (ME tab); deletion within 30 days. Uninstall to
remove local data. On-chain data cannot be removed. You may also request a copy or correction of your
data and complain to your data protection authority.

## Children

Not directed at children under 13; we do not knowingly collect their data and delete it on request.

## Changes and contact

Changes are posted on this page with a new date. Contact: **hello@odlet.xyz**.

---

### Owner notes (not part of the policy)

- `hello@odlet.xyz` is a **placeholder**: create that mailbox (or change it here) before launch, and set
  `NEXT_PUBLIC_CONTACT_EMAIL` (web) / `CONTACT_EMAIL` (deploy/.env; `.env.example` already has it)
  so the web page shows it. Without it the page says "the support contact listed on our app store page".
- Account deletion is **manual** today (no in-app button, no RPC). To delete a user:
  `delete from auth.users where id = '<uuid>';` in the Supabase SQL editor (profile, progress, results,
  friendships, listings, transactions cascade; owned `figures` and past `listings.buyer_id` are set to
  null, so the figure rows stay but are no longer linked to the person). Find the id with `select id from public.profiles where handle = '<handle>';`.
  Google Play additionally requires an in-app deletion path **and** a web deletion URL for apps that
  create accounts — see RELEASE_CHECKLIST.md.
- Verified against the code on 2026-09-26: `Infrastructure/Analytics/IAnalyticsService.cs` is a local
  logger that drops events in release builds; `ProjectSettings/UnityConnectSettings.asset` has Analytics,
  Crash Reporting, Ads and Performance Reporting disabled; the built APK requests only INTERNET, VIBRATE
  and Android's own `DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION`; the website has no analytics package.
- If you later add crash reporting (e.g. Sentry) or analytics, update both versions and the Play Data
  Safety form.

### Google Play Data Safety answers (for later)

| Data type | Collected | Shared | Purpose | Optional |
|---|---|---|---|---|
| User IDs (random account ID) | Yes | No | App functionality, fraud prevention | Required when online |
| App activity: in-app actions (results, progress) | Yes | No | App functionality | Required when online |
| Other user-generated content (handle) | Yes | No | App functionality | Yes (auto-generated otherwise) |
| Financial info / location / contacts / device IDs / email | No | — | — | — |

Encrypted in transit: **yes only once the backend is HTTPS** (today's local build uses http via adb
reverse). Users can request deletion: yes (by email; add the web deletion URL).
