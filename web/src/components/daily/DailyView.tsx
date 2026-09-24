"use client";

import { useEffect, useState } from "react";
import { dailyPlan, dayNumber, untilResetMs } from "@ronriku/core";
import { PixelButton } from "../ui/PixelButton";
import { PixelPanel, SectionTitle } from "../ui/PixelPanel";
import { StatTile } from "../ui/StatTile";
import { PixelIcon } from "../ui/PixelIcon";
import { LinkArt, PatternArt, SpatialArt } from "../landing/TrialArt";
import { LeaderboardView } from "../leaderboard/LeaderboardView";
import { useSession } from "../SessionProvider";
import { EARN } from "@/lib/economy";

const TRIAL_UI = {
  Pattern: { palette: "pattern", label: "Pattern", Art: PatternArt },
  Spatial: { palette: "lab", label: "Spatial", Art: SpatialArt },
  Logic: { palette: "link", label: "Link", Art: LinkArt },
} as const;

function fmt(ms: number) {
  const s = Math.max(0, Math.floor(ms / 1000));
  const p = (n: number) => String(n).padStart(2, "0");
  return `${p(Math.floor(s / 3600))}:${p(Math.floor((s % 3600) / 60))}:${p(s % 60)}`;
}

export function DailyView() {
  const [now, setNow] = useState<Date | null>(null);
  const { profile } = useSession();
  useEffect(() => {
    setNow(new Date());
    const t = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(t);
  }, []);
  const day = dayNumber(now ?? new Date());
  const plan = dailyPlan(day);
  const streak = profile?.streak ?? 0;

  return (
    <div className="flex flex-col gap-8">
      <PixelPanel accent className="flex flex-col gap-4 p-4 sm:flex-row sm:items-center sm:justify-between sm:p-6">
        <div>
          <p className="font-pixel text-[12px] text-muted">
            DAILY {String(day).padStart(3, "0")} · <span className="tabular">RESETS {now ? fmt(untilResetMs(now)) : "--:--:--"}</span>
          </p>
          <p className="mt-2 max-w-[520px] text-[15px] leading-6 text-muted">
            Same three puzzles for everyone today. Solve all three for +{EARN.dailyCompletion} shards, +{EARN.dailyStreakBonusPerDay} per streak day (cap +{EARN.dailyStreakBonusCap}).
          </p>
        </div>
        <PixelButton href="/play" size="lg">
          <PixelIcon name="play" /> Begin
        </PixelButton>
      </PixelPanel>

      <ol className="grid gap-4 md:grid-cols-3">
        {plan.trials.map((t) => {
          const ui = TRIAL_UI[t.kind as keyof typeof TRIAL_UI] ?? TRIAL_UI.Pattern;
          return (
            <li key={t.index} data-palette={ui.palette} data-accent="true" className="px-panel flex flex-col gap-3 p-4">
              <div className="flex justify-between font-pixel text-[10px] text-muted">
                <span>TRIAL {t.index + 1}</span>
                <span>{t.difficulty.toUpperCase()}</span>
              </div>
              <div className="dither-bg grid place-items-center p-3" style={{ background: "color-mix(in srgb, var(--ambient) 70%, #07080b)" }}>
                <div className="w-full max-w-[200px]">
                  <ui.Art />
                </div>
              </div>
              <h3 className="text-[24px] leading-8 text-accent">{ui.label}</h3>
            </li>
          );
        })}
      </ol>

      {profile && (
        <div className="grid grid-cols-3 gap-3">
          <StatTile label="Rating" value={profile.rating} tone="accent" />
          <StatTile label="Streak" value={streak} tone="yellow" />
          <StatTile label="Best" value={profile.bestStreak} />
        </div>
      )}

      <section aria-label="Today's leaderboard">
        <SectionTitle kicker="Today" title="Daily board" />
        <LeaderboardView scopes={["daily", "friends"]} />
      </section>
    </div>
  );
}
