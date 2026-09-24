"use client";

import Link from "next/link";
import { useState } from "react";
import { PixelTabs } from "../ui/PixelTabs";
import { DemoBadge } from "../ui/Badge";
import { VoxelViewer } from "../figure/VoxelViewer";
import { useSession } from "../SessionProvider";
import { fetchLeaderboard } from "@/lib/api";
import { useAsync } from "@/lib/hooks";
import type { LeaderboardRow, LeaderboardScope } from "@/lib/types";

const TABS: { id: LeaderboardScope; label: string }[] = [
  { id: "global", label: "Global" },
  { id: "daily", label: "Daily" },
  { id: "friends", label: "Friends" },
  { id: "boss", label: "Boss" },
];

const SCORE_LABEL: Record<LeaderboardScope, string> = { global: "RATING", daily: "POINTS", friends: "RATING", boss: "TIME" };

export function scoreText(r: LeaderboardRow, scope: LeaderboardScope): string {
  if (scope === "boss" && r.elapsedMs != null) {
    const s = Math.round(r.elapsedMs / 1000);
    return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}`;
  }
  return r.score.toLocaleString("en-US");
}
const MEDAL = ["var(--yellow)", "#c9d1dc", "#d08a4a"];

export function LeaderboardView({ scopes = ["global", "daily", "friends", "boss"] }: { scopes?: LeaderboardScope[] }) {
  const [scope, setScope] = useState<LeaderboardScope>(scopes[0] ?? "global");
  const tabs = TABS.filter((t) => scopes.includes(t.id));
  const state = useAsync(() => fetchLeaderboard(scope), [scope]);
  const { profile } = useSession();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <PixelTabs label="Leaderboard scope" tabs={tabs} value={scope} onChange={setScope} />
        {state.status === "ready" && state.value.source === "demo" && <DemoBadge reason={state.value.reason} />}
      </div>

      {state.status === "loading" && (
        <ul className="flex flex-col gap-2" aria-busy="true">
          {Array.from({ length: 8 }, (_, i) => (
            <li key={i} className="px-panel dither-bg h-16 animate-pulse" />
          ))}
        </ul>
      )}
      {state.status === "error" && <p className="text-danger">{state.error}</p>}
      {state.status === "ready" && state.value.data.length === 0 && (
        <p className="px-panel p-6 text-center text-muted">{scope === "friends" ? "Add friends to see how you compare." : "No scores yet today — be the first."}</p>
      )}
      {state.status === "ready" && state.value.data.length > 0 && (
        <>
          <Podium rows={state.value.data.slice(0, 3)} scope={scope} />
          <ol className="flex flex-col gap-2" aria-label={`${scope} leaderboard`}>
            <li className="grid grid-cols-[40px_40px_1fr_auto] gap-3 px-3 font-pixel text-[10px] text-muted">
              <span>#</span>
              <span />
              <span>PLAYER</span>
              <span>{SCORE_LABEL[scope]}</span>
            </li>
            {state.value.data.map((r) => {
              const me = profile?.handle && r.handle.toLowerCase() === profile.handle.toLowerCase();
              return (
                <li key={`${r.rank}-${r.handle}`}>
                  <Link
                    href={`/u/${encodeURIComponent(r.handle)}`}
                    className="px-panel grid min-h-14 grid-cols-[40px_40px_1fr_auto] items-center gap-3 px-3 py-2 hover:brightness-125"
                    style={me ? { ["--pb" as string]: "var(--accent)" } : undefined}
                  >
                    <span className="tabular font-pixel text-[16px]" style={{ color: MEDAL[r.rank - 1] ?? "var(--muted)" }}>
                      {r.rank}
                    </span>
                    <span className="size-10">{r.avatarEncoding && <VoxelViewer encoding={r.avatarEncoding} size={40} resolution={20} autoRotate={false} interactive={false} label="" />}</span>
                    <span className="truncate text-[16px]">
                      @{r.handle}
                      {me && <span className="ml-2 font-pixel text-[10px] text-accent">YOU</span>}
                    </span>
                    <span className="tabular font-pixel text-[16px] text-accent">{scoreText(r, scope)}</span>
                  </Link>
                </li>
              );
            })}
          </ol>
        </>
      )}
    </div>
  );
}

function Podium({ rows, scope }: { rows: LeaderboardRow[]; scope: LeaderboardScope }) {
  const order = [rows[1], rows[0], rows[2]];
  const heights = [56, 80, 40];
  return (
    <div className="grid grid-cols-3 items-end gap-2 pt-4 sm:gap-4" aria-hidden="true">
      {order.map((r, i) =>
        r ? (
          <div key={r.handle} className="flex flex-col items-center gap-1">
            {r.avatarEncoding && <VoxelViewer encoding={r.avatarEncoding} size={i === 1 ? 112 : 88} resolution={i === 1 ? 40 : 32} interactive={false} phase={i} label="" />}
            <span className="max-w-full truncate text-[13px]">@{r.handle}</span>
            <span className="tabular font-pixel text-[12px] text-accent">
              {scoreText(r, scope)} <span className="text-muted">{SCORE_LABEL[scope].slice(0, 3)}</span>
            </span>
            <div
              className="grid w-full place-items-center font-pixel text-[20px] text-bg-0"
              style={{ height: heights[i], background: `linear-gradient(180deg, ${MEDAL[r.rank - 1]}, color-mix(in srgb, ${MEDAL[r.rank - 1]} 45%, #07080b))`, boxShadow: `0 0 24px -6px ${MEDAL[r.rank - 1]}` }}
            >
              {r.rank}
            </div>
          </div>
        ) : (
          <div key={i} />
        ),
      )}
    </div>
  );
}
