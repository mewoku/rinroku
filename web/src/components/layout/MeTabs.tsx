"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useSession } from "../SessionProvider";

/** Sub-navigation for the ME tab: collection, profile, friends, ranks. */
export function MeTabs() {
  const path = usePathname();
  const { profile } = useSession();
  const items = [
    { href: "/inventory", label: "Collection" },
    ...(profile ? [{ href: `/u/${encodeURIComponent(profile.handle)}`, label: "Profile" }] : [{ href: "/login", label: "Account" }]),
    { href: "/friends", label: "Friends" },
    { href: "/leaderboard", label: "Ranks" },
  ];
  return (
    <nav aria-label="Me" className="-mt-2 mb-6 flex gap-2 overflow-x-auto p-1">
      {items.map((i) => {
        const active = path === i.href;
        return (
          <Link
            key={i.href}
            href={i.href}
            aria-current={active ? "page" : undefined}
            className={`px-border flex min-h-10 shrink-0 items-center px-4 font-pixel text-[12px] uppercase ${active ? "bg-accent text-bg-0" : "bg-surface-1 text-muted hover:text-text"}`}
            style={active ? ({ "--pb": "var(--accent)" } as React.CSSProperties) : undefined}
          >
            {i.label}
          </Link>
        );
      })}
    </nav>
  );
}
