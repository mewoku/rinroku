import React from "react";
import { interpolate, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { C } from "../theme";

/** A plain Seeker-like device frame (20:9) holding a shot. Rises in with a damped spring. */
export const Phone: React.FC<{ height?: number; accent?: string; rise?: boolean; children: React.ReactNode }> = ({
  height = 1320,
  accent = C.teal,
  rise = true,
  children,
}) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const width = Math.round(height * (1080 / 2400));
  const s = rise ? spring({ frame, fps, config: { damping: 200 }, durationInFrames: 18 }) : 1;
  const y = interpolate(s, [0, 1], [80, 0]);
  const bezel = 18;
  return (
    <div
      style={{
        width: width + bezel * 2,
        height: height + bezel * 2,
        padding: bezel,
        borderRadius: 64,
        background: "#0A0B0F",
        border: `3px solid ${C.line}`,
        boxShadow: `0 40px 120px rgba(0,0,0,0.7), 0 0 90px ${accent}33`,
        transform: `translateY(${y}px)`,
        opacity: s,
      }}
    >
      <div style={{ position: "relative", width, height, borderRadius: 48, overflow: "hidden", background: C.bg0 }}>
        {children}
      </div>
    </div>
  );
};
