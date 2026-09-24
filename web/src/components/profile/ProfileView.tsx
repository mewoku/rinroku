"use client";

import { useState } from "react";
import { PixelButton } from "../ui/PixelButton";
import { PixelPanel, SectionTitle } from "../ui/PixelPanel";
import { DemoBadge } from "../ui/Badge";
import { StatTile } from "../ui/StatTile";
import { Modal } from "../ui/Modal";
import { useToast } from "../ui/Toast";
import { FigureCard } from "../figure/FigureCard";
import { FigureDetail } from "../figure/FigureDetail";
import { VoxelViewer } from "../figure/VoxelViewer";
import { useSession } from "../SessionProvider";
import { shortAddress } from "../wallet/WalletConnect";
import { BackendUnavailableError, fetchPublicProfile, sendFriendRequest } from "@/lib/api";
import { useAsync } from "@/lib/hooks";
import type { FigureRecord } from "@/lib/types";

export function ProfileView({ handle }: { handle: string }) {
  const state = useAsync(() => fetchPublicProfile(handle), [handle]);
  const { profile: me } = useSession();
  const [open, setOpen] = useState<FigureRecord | null>(null);
  const toast = useToast();

  if (state.status === "loading") return <div className="px-panel dither-bg h-[280px] animate-pulse" aria-busy="true" />;
  if (state.status === "error") return <p className="text-danger">{state.error}</p>;
  const { data, source, reason } = state.value;
  if (!data) {
    return (
      <PixelPanel className="flex flex-col items-center gap-4 p-8 text-center">
        <h1 className="text-[24px]">@{handle}</h1>
        <p className="text-muted">No player with that handle.</p>
        <PixelButton href="/friends">Find friends</PixelButton>
      </PixelPanel>
    );
  }
  const { profile, collection } = data;
  const isMe = me?.id === profile.id;

  const addFriend = async () => {
    try {
      await sendFriendRequest(profile.handle);
      toast(`Friend request sent to @${profile.handle}`, "ok");
    } catch (e) {
      toast(e instanceof BackendUnavailableError ? "Friends need the live backend." : e instanceof Error ? e.message : "Failed", "error");
    }
  };

  return (
    <div className="flex flex-col gap-8">
      <PixelPanel accent className="grid gap-6 p-4 sm:grid-cols-[auto_1fr] sm:items-center sm:p-8">
        <div className="relative mx-auto" style={{ background: "radial-gradient(closest-side, color-mix(in srgb, var(--accent) 30%, transparent), transparent)" }}>
          {profile.avatarEncoding ? (
            <VoxelViewer encoding={profile.avatarEncoding} size={200} resolution={56} label={`@${profile.handle}'s avatar`} />
          ) : (
            <div className="dither-bg px-border grid size-[200px] place-items-center font-pixel text-[12px] text-muted">NO AVATAR</div>
          )}
        </div>
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="glow-text text-[32px] leading-10">@{profile.handle}</h1>
            {source === "demo" && <DemoBadge reason={reason} />}
          </div>
          {profile.displayName && <p className="text-muted">{profile.displayName}</p>}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <StatTile label="Rating" value={profile.rating} tone="accent" />
            <StatTile label="Streak" value={profile.streak} tone="yellow" />
            <StatTile label="Best" value={profile.bestStreak} />
            <StatTile label="Figures" value={collection.length} />
          </div>
          <div className="flex flex-wrap items-center gap-3">
            {!isMe && (
              <PixelButton onClick={addFriend} disabled={source === "demo"}>
                Add friend
              </PixelButton>
            )}
            {isMe && (
              <PixelButton href="/inventory" variant="secondary">
                Manage inventory
              </PixelButton>
            )}
            {profile.walletAddress && <span className="font-pixel text-[10px] text-muted">WALLET {shortAddress(profile.walletAddress)}</span>}
          </div>
        </div>
      </PixelPanel>

      <section aria-label="Collection">
        <SectionTitle kicker="Collection" title={`${collection.length} figure${collection.length === 1 ? "" : "s"}`} />
        {collection.length === 0 ? (
          <p className="px-panel p-6 text-center text-muted">No figures yet.</p>
        ) : (
          <ul className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
            {collection.map((f, i) => (
              <li key={f.id}>
                <FigureCard figure={f} size={120} phase={i} highlight={f.id === profile.avatarFigureId} onOpen={() => setOpen(f)} />
              </li>
            ))}
          </ul>
        )}
      </section>

      <Modal open={!!open} onClose={() => setOpen(null)} title="Figure" palette="frost">
        {open && <FigureDetail figure={open} />}
      </Modal>
    </div>
  );
}
