"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { PixelButton } from "../ui/PixelButton";
import { PixelPanel, SectionTitle } from "../ui/PixelPanel";
import { DemoBadge } from "../ui/Badge";
import { PixelIcon } from "../ui/PixelIcon";
import { useToast } from "../ui/Toast";
import { VoxelViewer } from "../figure/VoxelViewer";
import { useSession } from "../SessionProvider";
import { BackendUnavailableError, fetchFriends, respondFriendRequest, searchHandles, sendFriendRequest } from "@/lib/api";
import { useAsync } from "@/lib/hooks";
import type { FriendRow, Profile, Sourced } from "@/lib/types";

export function FriendsView() {
  const { userId, online, ready } = useSession();
  const friends = useAsync(() => fetchFriends(), [userId]);
  const toast = useToast();
  const [q, setQ] = useState("");
  const [results, setResults] = useState<Sourced<Profile[]> | null>(null);

  useEffect(() => {
    const t = setTimeout(async () => {
      if (q.trim().length < 2) return setResults(null);
      setResults(await searchHandles(q));
    }, 250);
    return () => clearTimeout(t);
  }, [q]);

  const fail = (e: unknown) => toast(e instanceof BackendUnavailableError ? "Friends need the live backend." : e instanceof Error ? e.message : "Failed", "error");

  const add = async (handle: string) => {
    try {
      await sendFriendRequest(handle);
      toast(`Request sent to @${handle}`, "ok");
      friends.reload();
    } catch (e) {
      fail(e);
    }
  };
  const respond = async (f: FriendRow, accept: boolean) => {
    try {
      await respondFriendRequest(f.userId, accept);
      toast(accept ? `You and @${f.handle} are friends.` : "Request declined.", accept ? "ok" : "info");
      friends.reload();
    } catch (e) {
      fail(e);
    }
  };

  const list = friends.status === "ready" ? friends.value.data : [];
  const demo = friends.status === "ready" && friends.value.source === "demo";
  const incoming = list.filter((f) => f.status === "pending_in");
  const outgoing = list.filter((f) => f.status === "pending_out");
  const accepted = list.filter((f) => f.status === "accepted");

  if (ready && online && !userId) {
    return (
      <PixelPanel accent className="mx-auto flex max-w-[480px] flex-col items-center gap-4 p-8 text-center">
        <h2 className="text-[20px]">Sign in to add friends</h2>
        <PixelButton href="/login">Play as guest</PixelButton>
      </PixelPanel>
    );
  }

  return (
    <div className="flex flex-col gap-8">
      <PixelPanel accent className="flex flex-col gap-3 p-4">
        <label htmlFor="friend-search" className="font-pixel text-[10px] text-muted">
          FIND BY HANDLE
        </label>
        <div className="relative">
          <span className="pointer-events-none absolute top-1/2 left-4 -translate-y-1/2 text-muted">
            <PixelIcon name="search" />
          </span>
          <input
            id="friend-search"
            className="px-input pl-11"
            value={q}
            maxLength={20}
            autoComplete="off"
            spellCheck={false}
            placeholder="handle"
            onChange={(e) => setQ(e.target.value.replace(/[^a-zA-Z0-9_]/g, ""))}
          />
        </div>
        {results && (
          <div className="flex flex-col gap-2">
            {results.source === "demo" && <DemoBadge reason={results.reason} />}
            {results.data.length === 0 && <p className="text-[14px] text-muted">No players found.</p>}
            {results.data.map((p) => (
              <Row key={p.id} handle={p.handle} rating={p.rating} avatar={p.avatarEncoding}>
                <PixelButton size="sm" onClick={() => add(p.handle)} disabled={results.source === "demo"}>
                  Add
                </PixelButton>
              </Row>
            ))}
          </div>
        )}
      </PixelPanel>

      {demo && <DemoBadge reason={friends.status === "ready" ? friends.value.reason : undefined} />}

      {incoming.length > 0 && (
        <section aria-label="Incoming requests">
          <SectionTitle kicker="Requests" title={`${incoming.length} waiting`} />
          <div className="flex flex-col gap-2">
            {incoming.map((f) => (
              <Row key={f.userId} handle={f.handle} rating={f.rating} avatar={f.avatarEncoding}>
                <PixelButton size="sm" onClick={() => respond(f, true)} disabled={demo}>
                  Accept
                </PixelButton>
                <PixelButton size="sm" variant="ghost" onClick={() => respond(f, false)} disabled={demo}>
                  Decline
                </PixelButton>
              </Row>
            ))}
          </div>
        </section>
      )}

      <section aria-label="Friends list">
        <SectionTitle kicker="Friends" title={`${accepted.length} friend${accepted.length === 1 ? "" : "s"}`} />
        {friends.status === "loading" && <div className="px-panel dither-bg h-16 animate-pulse" />}
        {friends.status === "ready" && accepted.length === 0 && <p className="px-panel p-6 text-center text-muted">Search a handle above to add your first friend.</p>}
        <div className="flex flex-col gap-2">
          {accepted.map((f) => (
            <Row key={f.userId} handle={f.handle} rating={f.rating} avatar={f.avatarEncoding}>
              <PixelButton size="sm" variant="secondary" href={`/u/${encodeURIComponent(f.handle)}`}>
                Profile
              </PixelButton>
            </Row>
          ))}
        </div>
      </section>

      {outgoing.length > 0 && (
        <section aria-label="Sent requests">
          <SectionTitle kicker="Sent" title="Pending" />
          <div className="flex flex-col gap-2">
            {outgoing.map((f) => (
              <Row key={f.userId} handle={f.handle} rating={f.rating} avatar={f.avatarEncoding}>
                <span className="font-pixel text-[10px] text-muted">PENDING</span>
              </Row>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}

function Row({ handle, rating, avatar, children }: { handle: string; rating: number; avatar: string | null; children: React.ReactNode }) {
  return (
    <div className="px-panel flex min-h-16 items-center gap-3 px-3 py-2">
      <span className="size-12 shrink-0">{avatar && <VoxelViewer encoding={avatar} size={48} resolution={24} autoRotate={false} interactive={false} label="" />}</span>
      <Link href={`/u/${encodeURIComponent(handle)}`} className="min-w-0 flex-1">
        <span className="block truncate text-[16px] hover:text-accent">@{handle}</span>
        <span className="tabular font-pixel text-[10px] text-muted">RATING {rating}</span>
      </Link>
      <div className="flex shrink-0 items-center gap-2">{children}</div>
    </div>
  );
}
