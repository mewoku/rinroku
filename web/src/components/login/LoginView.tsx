"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { PixelButton } from "../ui/PixelButton";
import { PixelPanel } from "../ui/PixelPanel";
import { Badge } from "../ui/Badge";
import { useToast } from "../ui/Toast";
import { VoxelViewer } from "../figure/VoxelViewer";
import { WalletLink } from "../wallet/WalletLink";
import { useSession } from "../SessionProvider";
import { setHandle, signInAsGuest, signOut } from "@/lib/api";
import { handleSchema } from "@/lib/validation";
import { demoFigures } from "@/lib/demo/data";

export function LoginView() {
  const { ready, online, userId, profile, refresh } = useSession();
  const [busy, setBusy] = useState(false);
  const [handle, setHandleInput] = useState("");
  const [err, setErr] = useState<string | null>(null);
  const toast = useToast();
  const router = useRouter();

  const guest = async () => {
    setBusy(true);
    try {
      await signInAsGuest();
      await refresh();
      toast("Welcome! Pick a handle.", "ok");
    } catch (e) {
      toast(e instanceof Error ? e.message : "Sign-in failed", "error");
    } finally {
      setBusy(false);
    }
  };

  const save = async () => {
    const parsed = handleSchema.safeParse(handle);
    if (!parsed.success) return setErr(parsed.error.issues[0]?.message ?? "Invalid handle");
    setErr(null);
    setBusy(true);
    try {
      await setHandle(parsed.data);
      await refresh();
      toast(`Hello, @${parsed.data}!`, "ok");
      router.push("/inventory");
    } catch (e) {
      setErr(e instanceof Error ? (/duplicate|unique|taken/i.test(e.message) ? "That handle is taken." : e.message) : "Could not save");
    } finally {
      setBusy(false);
    }
  };

  const step = !userId ? 1 : 2;

  return (
    <div className="mx-auto flex max-w-[480px] flex-col items-center gap-6 pt-4">
      <VoxelViewer encoding={(profile?.avatarEncoding ?? demoFigures[5]?.encoding)!} size={176} resolution={52} label="Your figure" />
      <PixelPanel accent className="flex w-full flex-col gap-5 p-6">
        <div className="flex items-center justify-between">
          <h1 className="text-[24px] leading-8">{userId ? "Your account" : "Start playing"}</h1>
          <Badge color="var(--accent)">Step {step}/2</Badge>
        </div>

        {!ready && <p className="text-muted">Connecting…</p>}

        {ready && !online && (
          <div className="flex flex-col gap-3">
            <Badge color="var(--warn)">Server offline</Badge>
            <p className="text-[14px] leading-5 text-muted">The game server isn&apos;t reachable right now, so accounts are unavailable. You can still play the Daily locally in the browser.</p>
            <PixelButton href="/play">Play offline</PixelButton>
          </div>
        )}

        {ready && online && !userId && (
          <div className="flex flex-col gap-3">
            <p className="text-[15px] leading-6 text-muted">No email, no password. A guest account keeps your streak, shards and figures on this device. Link a wallet later to keep them anywhere.</p>
            <PixelButton size="lg" onClick={guest} disabled={busy}>
              {busy ? "…" : "Play as guest"}
            </PixelButton>
          </div>
        )}

        {ready && online && userId && (
          <form
            className="flex flex-col gap-3"
            onSubmit={(e) => {
              e.preventDefault();
              void save();
            }}
          >
            <label htmlFor="handle" className="font-pixel text-[10px] text-muted">
              HANDLE {profile?.handle ? `· CURRENT @${profile.handle}` : ""}
            </label>
            <input
              id="handle"
              className="px-input"
              value={handle}
              maxLength={20}
              autoComplete="username"
              spellCheck={false}
              placeholder={profile?.handle ?? "your_handle"}
              onChange={(e) => setHandleInput(e.target.value.replace(/[^a-zA-Z0-9_]/g, ""))}
              aria-invalid={!!err}
              aria-describedby="handle-help"
            />
            <p id="handle-help" className={`text-[13px] ${err ? "text-danger" : "text-muted"}`}>
              {err ?? "3–20 letters, numbers or _. Shown on leaderboards."}
            </p>
            <PixelButton type="submit" disabled={busy || handle.length < 3}>
              {busy ? "Saving…" : "Save handle"}
            </PixelButton>
          </form>
        )}

        {ready && online && userId && (
          <div className="flex flex-col gap-3 border-t-2 border-line pt-5">
            <p className="font-pixel text-[10px] text-muted">WALLET (DEVNET)</p>
            <WalletLink />
            <button className="self-start font-pixel text-[10px] text-muted underline hover:text-text" onClick={() => void signOut().then(refresh)}>
              SIGN OUT
            </button>
          </div>
        )}
      </PixelPanel>
    </div>
  );
}
