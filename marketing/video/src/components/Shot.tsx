import React from "react";
import { AbsoluteFill, Img, interpolate, OffthreadVideo, spring, staticFile, useCurrentFrame, useVideoConfig } from "remotion";
import { hasClip, sequence } from "../assets";
import { ShotDef, SHOTS, ShotId } from "../shots";

type Props = {
  id: ShotId;
  /** Extra seconds to skip in the clip (on top of SHOTS[id].startFrom), e.g. a later hit of the same clip. */
  offset?: number;
  /** Punch-in on the cut (1.08 → 1) for hard beat cuts. */
  punch?: boolean;
  /** Ken Burns direction for the still fallback. */
  drift?: "in" | "out";
  fit?: "cover" | "contain";
  /** Extra zoom on recorded footage (keeps small game UI legible when full-bleed). */
  zoom?: number;
  style?: React.CSSProperties;
};

/**
 * One gameplay shot. Source priority: public/clips/<id>.mp4 or <raw>.mp4 → JPG sequence clips/raw/<raw>/ →
 * the evidence screenshot with a slow Ken Burns push, so the ad always renders.
 */
export const Shot: React.FC<Props> = ({ id, offset = 0, punch = false, drift = "in", fit = "cover", zoom = 1, style }) => {
  const frame = useCurrentFrame();
  const { fps, durationInFrames } = useVideoConfig();
  const def: ShotDef = SHOTS[id];
  const objectPosition = `50% ${Math.round(def.focusY * 100)}%`;

  const punchScale = punch
    ? interpolate(spring({ frame, fps, config: { damping: 200 }, durationInFrames: 8 }), [0, 1], [1.08, 1])
    : 1;

  const clipName = hasClip(id) ? id : def.raw && hasClip(def.raw) ? def.raw : undefined;
  if (clipName) {
    return (
      <AbsoluteFill style={{ overflow: "hidden", transform: `scale(${punchScale * zoom})`, ...style }}>
        <OffthreadVideo
          src={staticFile(`clips/${clipName}.mp4`)}
          startFrom={Math.round((def.startFrom + offset) * fps)}
          muted
          style={{ width: "100%", height: "100%", objectFit: fit, objectPosition }}
        />
      </AbsoluteFill>
    );
  }

  // Frame sequence from the Unity Footage test: step through the JPGs at their capture rate.
  const seq = sequence(def.raw);
  if (seq) {
    const startSec = def.tail !== undefined ? Math.max(0, seq.count / seq.fps - def.tail) : def.startFrom + offset;
    const idx = Math.min(seq.count - 1, Math.max(0, Math.floor(startSec * seq.fps + (frame * seq.fps) / fps)));
    return (
      <AbsoluteFill style={{ overflow: "hidden", transform: `scale(${punchScale})`, ...style }}>
        <Img
          src={staticFile(`clips/raw/${def.raw}/${String(idx).padStart(5, "0")}.jpg`)}
          style={{ width: "100%", height: "100%", objectFit: fit, objectPosition }}
        />
      </AbsoluteFill>
    );
  }

  // Ken Burns fallback: slow scale + slight vertical drift across the shot's length.
  const t = interpolate(frame, [0, Math.max(1, durationInFrames)], [0, 1], { extrapolateRight: "clamp" });
  const [from, to] = drift === "in" ? [1.02, 1.12] : [1.12, 1.02];
  const kb = interpolate(t, [0, 1], [from, to]);
  const y = interpolate(t, [0, 1], [0, -18]);
  return (
    <AbsoluteFill style={{ overflow: "hidden", ...style }}>
      <Img
        src={staticFile(`shots/${def.still}`)}
        style={{
          width: "100%",
          height: "100%",
          objectFit: fit,
          objectPosition,
          transform: `translateY(${y}px) scale(${kb * punchScale})`,
          imageRendering: "pixelated",
        }}
      />
    </AbsoluteFill>
  );
};
