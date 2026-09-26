import React from "react";
import { AbsoluteFill, interpolate, useCurrentFrame } from "remotion";
import { C, PALETTE, PaletteName } from "../theme";

/** Dark ambient background with two slowly drifting radial blobs and a 4 px pixel grid, like the in-game backdrop. */
export const Ambient: React.FC<{ palette: PaletteName }> = ({ palette }) => {
  const frame = useCurrentFrame();
  const p = PALETTE[palette];
  const a = Math.sin(frame / 90) * 6;
  const b = Math.cos(frame / 110) * 6;
  return (
    <AbsoluteFill style={{ background: C.bg0 }}>
      <AbsoluteFill
        style={{
          background: `radial-gradient(circle at ${22 + a}% ${24 + b}%, ${p.ambient} 0%, transparent 45%),
                       radial-gradient(circle at ${80 - b}% ${78 + a}%, ${p.ambient} 0%, transparent 50%)`,
          opacity: interpolate(frame, [0, 12], [0.4, 1], { extrapolateRight: "clamp" }),
        }}
      />
      <AbsoluteFill
        style={{
          backgroundImage:
            "linear-gradient(rgba(0,0,0,0.35) 1px, transparent 1px), linear-gradient(90deg, rgba(0,0,0,0.35) 1px, transparent 1px)",
          backgroundSize: "8px 8px",
          opacity: 0.5,
        }}
      />
      <AbsoluteFill style={{ background: "radial-gradient(ellipse at center, transparent 55%, rgba(0,0,0,0.6) 100%)" }} />
    </AbsoluteFill>
  );
};
