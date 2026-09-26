import type { Metadata } from "next";
import Link from "next/link";
import { PageHeader, PageShell } from "@/components/layout/PageShell";
import { Contact, LEGAL_UPDATED, LegalList, LegalSection, Strong } from "@/components/legal/Legal";

export const metadata: Metadata = {
  title: "Terms",
  description: "Terms of use for the RONRIKU app and website.",
};

export default function TermsPage() {
  return (
    <PageShell palette="lab">
      <PageHeader kicker={`Updated ${LEGAL_UPDATED}`} title="Terms" />
      <div className="mx-auto max-w-[720px] space-y-6">
        <p className="text-[17px] leading-7 text-text">
          By playing RONRIKU (the Android app or this website) you agree to these terms. If you do not agree, please do not use the game.
        </p>

        <LegalSection id="game" title="The game">
          <p>
            RONRIKU is provided free of charge, as is, and may change, pause or reset at any time, including seasons, ratings, shop items and balances.
            We try hard to keep it running but do not guarantee it will always be available or error-free.
          </p>
        </LegalSection>

        <LegalSection id="value" title="Shards, figures and NFTs">
          <LegalList
            items={[
              <>
                <Strong>Shards</Strong> and in-game figures are game items. They have no cash value, cannot be redeemed for money, and are not property.
              </>,
              <>
                Figures bought with SOL on the website are minted as NFTs on Solana <Strong>devnet</Strong>, a public test network. Devnet SOL and devnet
                NFTs have no monetary value. Nothing in RONRIKU is an investment or a promise of future value.
              </>,
              <>
                Blockchain transactions are final. You are responsible for your wallet, its keys and the transactions you sign. We never ask for your seed
                phrase.
              </>,
            ]}
          />
        </LegalSection>

        <LegalSection id="play" title="Fair play">
          <LegalList
            items={[
              <>Do not cheat, use bots or modified clients, exploit bugs, or tamper with the server or other players&apos; accounts.</>,
              <>Handles must not impersonate others or be offensive, hateful or illegal.</>,
              <>We may reset results, remove items or close accounts that break these rules.</>,
            ]}
          />
        </LegalSection>

        <LegalSection id="ip" title="Content and licences">
          <p>
            The game, its art and music belong to RONRIKU. The source code is MIT-licensed and the fonts are licensed under the SIL Open Font License;
            see the third-party notices in the project. You may share screenshots and videos of your play.
          </p>
        </LegalSection>

        <LegalSection id="liability" title="Liability">
          <p>
            To the extent the law allows, RONRIKU is provided without warranties, and we are not liable for indirect or consequential losses, lost
            progress, or losses from blockchain transactions, wallets or third-party services. Nothing here limits rights you have under consumer law
            that cannot be waived.
          </p>
        </LegalSection>

        <LegalSection id="end" title="Ending and changes">
          <p>
            You can stop playing at any time and ask us to delete your account (see the <Link className="text-accent underline underline-offset-4 hover:text-text" href="/privacy">privacy policy</Link>). We may update these terms; the date above shows the latest version, and continuing to play means you accept it. Contact: <Contact />.
          </p>
        </LegalSection>
      </div>
    </PageShell>
  );
}
