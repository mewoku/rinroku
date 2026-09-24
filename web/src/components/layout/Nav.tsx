"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { PixelIcon, type IconName } from "../ui/PixelIcon";
import { useSession } from "../SessionProvider";
import { formatShards } from "@/lib/economy";
import { WalletConnect } from "../wallet/WalletConnect";

const NAV: { href: string; label: string; icon: IconName; color: string; also?: string[] }[] = [
  { href: "/play", label: "Play", icon: "play", color: "var(--lab-accent)" },
  { href: "/daily", label: "Daily", icon: "bolt", color: "var(--pattern-accent)" },
  { href: "/bosses", label: "Bosses", icon: "skull", color: "var(--boss-accent)" },
  { href: "/market", label: "Shop", icon: "shop", color: "var(--link-accent)" },
  { href: "/inventory", label: "Me", icon: "me", color: "var(--forest-accent)", also: ["/friends", "/leaderboard", "/u", "/login"] },
];

function isActive(path: string, item: { href: string; also?: string[] }) {
  return [item.href, ...(item.also ?? [])].some((h) => path === h || path.startsWith(h + "/"));
}

export function Logo({ size = 20 }: { size?: number }) {
  return (
    <Link href="/" className="group flex items-center gap-2" aria-label="RONRIKU home">
      <span aria-hidden="true" className="relative grid size-8 place-items-center bg-teal" style={{ boxShadow: "inset -4px -4px 0 0 #135b73, 0 0 16px -2px rgb(17 197 179 / 0.6)" }}>
        <span className="size-3 bg-yellow" style={{ boxShadow: "inset -2px -2px 0 0 #b8ab1f" }} />
      </span>
      <span className="font-pixel text-text group-hover:text-teal" style={{ fontSize: size, lineHeight: `${size}px` }}>
        RONRIKU
      </span>
    </Link>
  );
}

export function SiteHeader() {
  const path = usePathname();
  const { profile, online, ready } = useSession();
  return (
    <header className="sticky top-0 z-40 border-b-2 border-line bg-[rgb(7_8_11/0.82)] backdrop-blur-[6px]">
      <div className="mx-auto flex h-16 max-w-[1200px] items-center gap-4 px-4">
        <Logo />
        <nav aria-label="Primary" className="ml-6 hidden items-center gap-1 md:flex">
          {NAV.map((n) => {
            const active = isActive(path, n);
            return (
              <Link
                key={n.href}
                href={n.href}
                aria-current={active ? "page" : undefined}
                className={`flex h-10 items-center gap-2 px-3 font-pixel text-[12px] uppercase transition-colors ${active ? "text-text" : "text-muted hover:text-text"}`}
                style={active ? { color: n.color, boxShadow: `inset 0 -2px 0 0 ${n.color}` } : undefined}
              >
                <PixelIcon name={n.icon} size={16} />
                {n.label}
              </Link>
            );
          })}
        </nav>
        <div className="ml-auto flex items-center gap-3">
          {ready && profile && (
            <Link href={`/u/${profile.handle}`} className="hidden items-center gap-2 font-pixel text-[12px] text-yellow sm:flex" title="Shards">
              <PixelIcon name="shard" size={16} />
              <span className="tabular">{formatShards(profile.shards)}</span>
            </Link>
          )}
          {ready && !profile && (
            <Link href="/login" className="font-pixel text-[12px] uppercase text-muted hover:text-text">
              {online ? "Sign in" : "Guest"}
            </Link>
          )}
          <div className="hidden sm:block">
            <WalletConnect size="sm" />
          </div>
        </div>
      </div>
    </header>
  );
}

export function BottomNav() {
  const path = usePathname();
  return (
    <nav
      aria-label="Primary"
      className="fixed inset-x-0 bottom-0 z-40 border-t-2 border-line bg-[rgb(7_8_11/0.92)] pb-[env(safe-area-inset-bottom)] backdrop-blur-[6px] md:hidden"
    >
      <ul className="mx-auto grid h-[72px] max-w-[480px] grid-cols-5">
        {NAV.map((n) => {
          const active = isActive(path, n);
          return (
            <li key={n.href}>
              <Link
                href={n.href}
                aria-current={active ? "page" : undefined}
                className="flex h-full flex-col items-center justify-center gap-1 font-pixel text-[10px] uppercase"
                style={{ color: active ? n.color : "var(--muted)" }}
              >
                <span
                  className="grid size-8 place-items-center"
                  style={active ? { background: `color-mix(in srgb, ${n.color} 18%, transparent)`, boxShadow: `0 0 12px -2px ${n.color}` } : undefined}
                >
                  <PixelIcon name={n.icon} size={16} />
                </span>
                {n.label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
