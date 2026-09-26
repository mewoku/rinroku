import React from "react";
import { interpolate, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { C, FONT_BODY, FONT_DISPLAY, SAFE } from "../theme";

/** Slam-in headline (scale 1.35 → 1, damped) with the site's stacked pixel shadow. Max ~8 words. */
export const Slam: React.FC<{
  text: string;
  sub?: string;
  delay?: number;
  size?: number;
  color?: string;
  position?: "top" | "center" | "bottom";
}> = ({ text, sub, delay = 0, size, color = C.text, position = "top" }) => {
  // Fit the longest line into the safe width (Silkscreen advance is about 0.82 em).
  const longest = Math.max(...text.split("\n").map((l) => l.length));
  const fitted = Math.min(size ?? 150, Math.floor((1080 - SAFE.left - SAFE.right) / (longest * 0.82)));
  const frame = useCurrentFrame() - delay;
  const { fps } = useVideoConfig();
  const s = spring({ frame, fps, config: { damping: 18, stiffness: 220 }, durationInFrames: 12 });
  const scale = interpolate(s, [0, 1], [1.35, 1]);
  const opacity = interpolate(frame, [0, 3], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const subOpacity = interpolate(frame, [8, 14], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const placement: React.CSSProperties =
    position === "top"
      ? { top: SAFE.top + 40 }
      : position === "bottom"
        ? { bottom: SAFE.bottom + 120 }
        : { top: "50%", transform: "translateY(-50%)" };
  return (
    <div
      style={{
        position: "absolute",
        left: SAFE.left,
        right: SAFE.right,
        ...placement,
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        gap: 20,
        textAlign: "center",
      }}
    >
      <div
        style={{
          fontFamily: FONT_DISPLAY,
          fontWeight: 700,
          fontSize: fitted,
          lineHeight: 1.05,
          color,
          opacity,
          transform: `scale(${scale})`,
          textShadow: `0 6px 0 ${C.tealDeep}, 0 12px 0 #0B3B4A, 0 0 40px rgba(0,0,0,0.9)`,
          whiteSpace: "pre-line",
        }}
      >
        {text}
      </div>
      {sub ? (
        <div
          style={{
            fontFamily: FONT_BODY,
            fontSize: 40,
            color: C.text,
            opacity: subOpacity,
            background: "rgba(7,8,11,0.72)",
            padding: "10px 22px",
          }}
        >
          {sub}
        </div>
      ) : null}
    </div>
  );
};

/** The game's chips × mult box, slammed in (blue chips × red mult). */
export const ChipsMult: React.FC<{ chips: number; mult: number; delay?: number }> = ({ chips, mult, delay = 0 }) => {
  const frame = useCurrentFrame() - delay;
  const { fps } = useVideoConfig();
  const s = spring({ frame, fps, config: { damping: 14, stiffness: 240 }, durationInFrames: 12 });
  const box = (value: number, bg: string, border: string) => (
    <div
      style={{
        minWidth: 260,
        padding: "22px 36px",
        background: bg,
        border: `6px solid ${border}`,
        fontFamily: FONT_DISPLAY,
        fontWeight: 700,
        fontSize: 120,
        color: C.text,
        textAlign: "center",
        boxShadow: "0 16px 0 rgba(0,0,0,0.45)",
      }}
    >
      {value}
    </div>
  );
  return (
    <div
      style={{
        position: "absolute",
        left: 0,
        right: 0,
        top: "44%",
        display: "flex",
        justifyContent: "center",
        alignItems: "center",
        gap: 28,
        opacity: interpolate(frame, [0, 2], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" }),
        transform: `scale(${interpolate(s, [0, 1], [1.6, 1])})`,
      }}
    >
      {box(chips, C.chips, "#8DB6FF")}
      <div style={{ fontFamily: FONT_DISPLAY, fontSize: 96, color: C.text }}>×</div>
      {box(mult, C.mult, "#FF9AAE")}
    </div>
  );
};

/** Decaying screen shake from `at` for ~10 frames (matches in-game juice). */
export const Shake: React.FC<{ at: number; amp?: number; children: React.ReactNode }> = ({ at, amp = 18, children }) => {
  const frame = useCurrentFrame();
  const t = frame - at;
  const k = t >= 0 && t < 10 ? (1 - t / 10) * amp : 0;
  const x = Math.sin(t * 2.7) * k;
  const y = Math.cos(t * 3.3) * k * 0.6;
  return <div style={{ position: "absolute", inset: 0, transform: `translate(${x}px, ${y}px)` }}>{children}</div>;
};
