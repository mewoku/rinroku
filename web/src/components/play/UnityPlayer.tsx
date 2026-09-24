"use client";

import { useEffect, useState } from "react";
import { Unity, useUnityContext } from "react-unity-webgl";
import { PixelButton } from "../ui/PixelButton";
import { PixelPanel } from "../ui/PixelPanel";
import { PixelIcon } from "../ui/PixelIcon";
import { VoxelViewer } from "../figure/VoxelViewer";
import { demoFigures } from "@/lib/demo/data";
import type { UnityBuildFiles } from "@/lib/server/unityBuild";

export function PixelProgress({ value, label }: { value: number; label: string }) {
  const segments = 20;
  const filled = Math.round(Math.max(0, Math.min(1, value)) * segments);
  return (
    <div className="w-full max-w-[360px]" role="progressbar" aria-label={label} aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(value * 100)}>
      <div className="mb-2 flex justify-between font-pixel text-[12px] text-muted">
        <span>{label}</span>
        <span className="tabular text-accent">{Math.round(value * 100)}%</span>
      </div>
      <div className="px-border flex gap-[2px] bg-bg-0 p-[2px]">
        {Array.from({ length: segments }, (_, i) => (
          <span
            key={i}
            className="h-4 flex-1"
            style={{
              background: i < filled ? (i === filled - 1 ? "var(--yellow)" : "linear-gradient(180deg, var(--accent), var(--accent-2))") : "var(--surface-2)",
            }}
          />
        ))}
      </div>
    </div>
  );
}

export function UnityPlayer({ build }: { build: UnityBuildFiles | null }) {
  if (!build) return <BuildMissing />;
  return <UnityFrame build={build} />;
}

function UnityFrame({ build }: { build: UnityBuildFiles }) {
  const { unityProvider, isLoaded, loadingProgression, initialisationError, requestFullscreen } = useUnityContext({
    ...build,
    companyName: "RONRIKU",
    productName: "RONRIKU",
    productVersion: "2",
    // Unity 6 WebGL: flush persistentDataPath (profile.json) to IndexedDB so progress survives reloads.
    autoSyncPersistentDataPath: true,
  });
  const [dpr, setDpr] = useState(1);
  useEffect(() => setDpr(Math.min(2, window.devicePixelRatio || 1)), []);

  return (
    <div className="flex flex-col items-center gap-4">
      <div
        className="px-panel relative w-full max-w-[480px] overflow-hidden p-0 md:max-w-[520px]"
        style={{ aspectRatio: "9 / 19.5", maxHeight: "calc(100dvh - 220px)" }}
      >
        <Unity
          unityProvider={unityProvider}
          devicePixelRatio={dpr}
          className="block h-full w-full"
          style={{ visibility: isLoaded ? "visible" : "hidden" }}
          tabIndex={0}
        />
        {!isLoaded && (
          <div className="absolute inset-0 flex flex-col items-center justify-center gap-6 p-6">
            {initialisationError ? (
              <>
                <p className="font-pixel text-[14px] text-danger">GAME FAILED TO START</p>
                <p className="text-center text-[14px] text-muted">{String(initialisationError.message ?? initialisationError)}</p>
              </>
            ) : (
              <>
                <p className="font-pixel text-[24px] text-teal">RONRIKU</p>
                <PixelProgress value={loadingProgression} label="LOADING" />
                <p className="px-blink font-pixel text-[10px] text-muted">BOOTING WEBGL…</p>
              </>
            )}
          </div>
        )}
      </div>
      <div className="flex gap-4">
        <PixelButton variant="secondary" size="sm" onClick={() => requestFullscreen(true)} disabled={!isLoaded}>
          Fullscreen
        </PixelButton>
      </div>
    </div>
  );
}

function BuildMissing() {
  const fig = demoFigures[2]!;
  return (
    <PixelPanel accent className="mx-auto flex max-w-[560px] flex-col items-center gap-6 p-6 text-center md:p-10">
      <VoxelViewer encoding={fig.encoding} size={160} resolution={48} label="Waiting figure" />
      <div>
        <p className="font-pixel text-[12px] text-warn uppercase">
          <PixelIcon name="lock" size={12} className="mr-2 inline" />
          Build not available yet
        </p>
        <h2 className="mt-2 text-[24px] leading-8">The browser build is still in the oven.</h2>
        <p className="mt-2 text-[15px] leading-6 text-muted">
          The Unity WebGL build hasn&apos;t been exported to <code className="text-text">web/public/unity/Build/</code> yet. Once it is, this page loads it automatically.
        </p>
      </div>
      <PixelProgress value={0} label="WAITING FOR BUILD" />
      <div className="flex flex-wrap justify-center gap-4">
        <PixelButton href="/market" palette="pattern">
          Browse figures
        </PixelButton>
        <PixelButton href="/leaderboard" variant="secondary" palette="link">
          Leaderboard
        </PixelButton>
      </div>
    </PixelPanel>
  );
}
