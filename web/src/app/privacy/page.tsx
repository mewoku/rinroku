import type { Metadata } from "next";
import Link from "next/link";
import { PageHeader, PageShell } from "@/components/layout/PageShell";
import { Contact, LEGAL_UPDATED, LegalList, LegalSection, Strong } from "@/components/legal/Legal";

export const metadata: Metadata = {
  title: "Privacy",
  description: "What RONRIKU stores, why, and how to delete it. No ads, no tracking SDKs.",
};

export default function PrivacyPage() {
  return (
    <PageShell palette="lab">
      <PageHeader kicker={`Updated ${LEGAL_UPDATED}`} title="Privacy" />
      <div className="mx-auto max-w-[720px] space-y-6">
        <p className="text-[17px] leading-7 text-text">
          RONRIKU is a puzzle game. It needs very little about you: no email, no real name, no phone number, no location. There are no ads and no
          analytics or tracking SDKs in the app or on this site.
        </p>

        <LegalSection id="short" title="In short">
          <LegalList
            items={[
              <>The Android app works fully offline. Progress is saved on your device.</>,
              <>
                When it can reach our server, the app creates an <Strong>anonymous account</Strong> (a random ID) so your progress, rating and figures are
                kept and ranked.
              </>,
              <>
                A Solana <Strong>wallet address</Strong> is only stored if you choose to link a wallet on the website. We never see or store private keys
                or seed phrases.
              </>,
              <>We do not sell data, show ads, or share data with advertisers or data brokers.</>,
            ]}
          />
        </LegalSection>

        <LegalSection id="device" title="Stored on your device">
          <LegalList
            items={[
              <>Your local profile: progress, stars, times, streak, shards, figures, and settings (sound, haptics, motion, reduced motion).</>,
              <>A session token for the anonymous account, when online.</>,
              <>
                On the website, your browser keeps the sign-in session and a short list of purchases that were paid but not yet confirmed, so they can be
                finished if the tab closes.
              </>,
            ]}
          />
          <p>Uninstalling the app or clearing its storage deletes this data.</p>
        </LegalSection>

        <LegalSection id="server" title="Stored on our server (when online)">
          <LegalList
            items={[
              <>
                <Strong>Account:</Strong> a random user ID created by anonymous sign-in (Supabase Auth). No email or password.
              </>,
              <>
                <Strong>Profile:</Strong> a handle and display name (generated for you; you can change them), avatar figure, rating, shards, streak and
                counts of completed dailies.
              </>,
              <>
                <Strong>Game results:</Strong> level stars and best times, daily and boss results, and a compact record of your answers and moves. The
                server replays these to check results are genuine.
              </>,
              <>
                <Strong>Social and shop:</Strong> friend requests, figures you own, marketplace listings, and a ledger of shard and SOL transactions.
              </>,
              <>
                <Strong>Wallet (website, optional):</Strong> the public address you link by signing a one-time message, and the signatures of purchases
                you make.
              </>,
            ]}
          />
        </LegalSection>

        <LegalSection id="public" title="What other players can see">
          <p>
            Your handle, display name, avatar figure, rating and streak appear on leaderboards and your profile page. Friends see each other in their
            friend lists. Anything you do on the Solana blockchain (wallet address, transactions, figures minted as NFTs) is public by nature of the
            blockchain and cannot be deleted by us.
          </p>
        </LegalSection>

        <LegalSection id="tech" title="Technical data">
          <LegalList
            items={[
              <>
                Like any web service, our server sees your IP address and basic request details. We use them only to deliver the service, rate-limit
                abuse and keep it secure. Rate limits are kept in memory, not in a database.
              </>,
              <>
                The app requests two Android permissions: <Strong>Internet</Strong> (online play) and <Strong>Vibrate</Strong> (haptics). The motion
                sensor used for the parallax effect is read on the device and never sent anywhere.
              </>,
              <>Unity Analytics, Unity crash reporting, Unity Ads and all other third-party analytics are switched off.</>,
            ]}
          />
        </LegalSection>

        <LegalSection id="processors" title="Who processes data for us">
          <LegalList
            items={[
              <>Our hosting provider, which runs the server and database (Supabase, self-hosted or hosted).</>,
              <>
                When you use wallet features on the website: your wallet app and a Solana RPC provider, which receive your transactions and IP address
                under their own privacy policies. NFTs are minted on Solana <Strong>devnet</Strong>, a test network with no real money.
              </>,
            ]}
          />
        </LegalSection>

        <LegalSection id="retention" title="Keeping and deleting data">
          <p>
            We keep account data while the account exists. To delete your online account and everything linked to it, contact us at <Contact /> with
            your player handle (shown on the ME tab). We delete it within 30 days. Local data is removed by uninstalling the app. Data already written to
            the blockchain cannot be removed.
          </p>
          <p>You can also ask us for a copy of your data or to correct it, and you can complain to your local data protection authority.</p>
        </LegalSection>

        <LegalSection id="children" title="Children">
          <p>RONRIKU is not directed at children under 13, and we do not knowingly collect data from them. If you believe a child has an account, contact us and we will delete it.</p>
        </LegalSection>

        <LegalSection id="changes" title="Changes and contact">
          <p>
            If this policy changes, we update this page and the date above. Questions: <Contact />. See also the <Link className="text-accent underline underline-offset-4 hover:text-text" href="/terms">terms of use</Link>.
          </p>
        </LegalSection>
      </div>
    </PageShell>
  );
}
