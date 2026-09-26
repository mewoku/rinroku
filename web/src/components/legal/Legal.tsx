import type { ReactNode } from "react";
import { PixelPanel } from "@/components/ui/PixelPanel";
import { publicEnv } from "@/lib/env";

/** Shared building blocks for /privacy and /terms: readable measure, pixel headings, calm panels. */
export function LegalSection({ id, title, children }: { id: string; title: string; children: ReactNode }) {
  return (
    <PixelPanel as="section" id={id} className="scroll-mt-24 space-y-3 text-[15px] leading-6 text-muted">
      <h2 className="text-[16px] leading-6 text-text uppercase">{title}</h2>
      {children}
    </PixelPanel>
  );
}

export function LegalList({ items }: { items: ReactNode[] }) {
  return (
    <ul className="space-y-2">
      {items.map((item, i) => (
        <li key={i} className="flex gap-3">
          <span aria-hidden className="mt-[9px] size-[6px] shrink-0 bg-accent" />
          <span>{item}</span>
        </li>
      ))}
    </ul>
  );
}

export function Strong({ children }: { children: ReactNode }) {
  return <strong className="font-normal text-text">{children}</strong>;
}

/** The contact line: the configured address, or the store listing when none is configured. */
export function Contact() {
  const email = publicEnv.contactEmail;
  if (!email) return <>the support contact listed on our app store page</>;
  return (
    <a className="text-accent underline underline-offset-4 hover:text-text" href={`mailto:${email}`}>
      {email}
    </a>
  );
}

export const LEGAL_UPDATED = "26 September 2026";
