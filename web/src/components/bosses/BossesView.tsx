"use client";

import { useMemo } from "react";
import { generateFigure } from "@ronriku/core";
import { PixelButton } from "../ui/PixelButton";
import { DemoBadge } from "../ui/Badge";
import { PixelIcon } from "../ui/PixelIcon";
import { SectionTitle } from "../ui/PixelPanel";
import { useToast } from "../ui/Toast";
import { VoxelViewer } from "../figure/VoxelViewer";
import { WalletConnect } from "../wallet/WalletConnect";
import { useSolPurchase } from "../wallet/useSolPurchase";
import { LeaderboardView } from "../leaderboard/LeaderboardView";
import { fetchBossEvents, type BossEvent } from "@/lib/api";
import { formatShards, formatSol } from "@/lib/economy";
import { useAsync } from "@/lib/hooks";
import { PALETTES, type PaletteName } from "@/lib/palettes";

function bossFigure(ev: BossEvent): string {
  // Stable per-event guardian look (decorative; the fight itself runs in the game).
  let h = 1469598103n;
  for (const c of ev.id) h = BigInt.asUintN(64, (h ^ BigInt(c.charCodeAt(0))) * 1099511628211n);
  return generateFigure(h, 5, true).encode();
}

function timeLeft(ends: string): string {
  const ms = new Date(ends).getTime() - Date.now();
  if (ms <= 0) return "ENDED";
  const d = Math.floor(ms / 86_400_000);
  const h = Math.floor((ms % 86_400_000) / 3_600_000);
  return d > 0 ? `${d}D ${h}H LEFT` : `${h}H LEFT`;
}

export function BossesView() {
  const state = useAsync(() => fetchBossEvents(), []);
  const demo = state.status === "ready" && state.value.source === "demo";
  return (
    <div className="flex flex-col gap-10">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="max-w-[640px] text-[15px] leading-6 text-muted">
          Three chained puzzles, one timer, one big HP bar. Entry is paid in shards in the game — or with devnet SOL here. Win within the time limit for the raid reward.
        </p>
        {demo && <DemoBadge reason={state.status === "ready" ? state.value.reason : undefined} />}
      </div>
      {state.status === "loading" && <div className="px-panel dither-bg h-[240px] animate-pulse" />}
      {state.status === "ready" && state.value.data.length === 0 && <p className="px-panel p-6 text-center text-muted">No raid scheduled right now. Check back soon.</p>}
      {state.status === "ready" && (
        <ul className="flex flex-col gap-6">
          {state.value.data.map((ev) => (
            <BossCard key={ev.id} ev={ev} demo={demo} />
          ))}
        </ul>
      )}
      <section aria-label="Boss leaderboard">
        <SectionTitle kicker="Fastest wins" title="Raid board" />
        <LeaderboardView scopes={["boss"]} />
      </section>
    </div>
  );
}

function BossCard({ ev, demo }: { ev: BossEvent; demo: boolean }) {
  const encoding = useMemo(() => bossFigure(ev), [ev]);
  const paletteName = (ev.palette in PALETTES ? ev.palette : "boss") as PaletteName;
  const live = new Date(ev.startsAt).getTime() <= Date.now();
  const { purchase, busy, connected } = useSolPurchase();
  const toast = useToast();

  const paySol = async () => {
    try {
      await purchase("boss-entry", { bossId: ev.id });
      toast("Entry recorded — start the raid in the game.", "ok");
    } catch (e) {
      toast(e instanceof Error ? e.message : "Payment failed", "error");
    }
  };

  return (
    <li data-palette={paletteName} data-accent="true" className="px-panel scanlines relative grid items-center gap-6 overflow-hidden p-6 md:grid-cols-[auto_1fr]" style={{ background: "linear-gradient(135deg, color-mix(in srgb, var(--ambient) 90%, #000) 0%, #14161d 75%)" }}>
      <div className="relative mx-auto" style={{ background: "radial-gradient(closest-side, color-mix(in srgb, var(--accent) 35%, transparent), transparent)" }}>
        <VoxelViewer encoding={encoding} size={176} resolution={52} rim={PALETTES[paletteName].accent} label={`${ev.name} guardian`} />
      </div>
      <div className="relative flex flex-col gap-3">
        <p className="font-pixel text-[12px] text-accent-2">{live ? `LIVE · ${timeLeft(ev.endsAt)}` : `STARTS ${new Date(ev.startsAt).toUTCString().slice(0, 16).toUpperCase()}`}</p>
        <h2 className="text-[32px] leading-10 text-accent">{ev.name}</h2>
        <div className="grid grid-cols-3 gap-3 font-pixel text-[12px]">
          <span>
            <span className="block text-[10px] text-muted">STAGES</span>
            {ev.stages}
          </span>
          <span>
            <span className="block text-[10px] text-muted">TIME</span>
            {Math.round(ev.timeLimitMs / 60000)} MIN
          </span>
          <span>
            <span className="block text-[10px] text-muted">REWARD</span>
            <span className="text-yellow">{formatShards(ev.rewardShards)} ◆</span>
          </span>
        </div>
        <div className="mt-2 flex flex-wrap items-center gap-3">
          <PixelButton href="/play">
            <PixelIcon name="skull" /> {formatShards(ev.entryShards)} ◆ in game
          </PixelButton>
          {connected ? (
            <PixelButton variant="secondary" palette="frost" onClick={paySol} disabled={demo || !live || busy}>
              {busy ? "Confirm in wallet…" : `${formatSol(ev.entryLamports)} SOL`}
            </PixelButton>
          ) : (
            <WalletConnect size="md" />
          )}
        </div>
      </div>
    </li>
  );
}
