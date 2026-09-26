import React from "react";
import { AbsoluteFill, Img, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig } from "remotion";
import { C, FONT_BODY, FONT_DISPLAY } from "../theme";
import { Ambient } from "./Ambient";

/** End card: logo, tagline, one action. Hold ≥ 3 s. */
export const EndCard: React.FC<{ cta: string; small?: string }> = ({ cta, small }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const enter = (d: number) => spring({ frame: frame - d, fps, config: { damping: 200 }, durationInFrames: 16 });
  const logo = enter(0);
  const tag = enter(10);
  const btn = enter(20);
  const pulse = 1 + Math.sin(Math.max(0, frame - 36) / 6) * 0.02;
  return (
    <AbsoluteFill>
      <Ambient palette="lab" />
      <AbsoluteFill style={{ alignItems: "center", justifyContent: "center", gap: 44, padding: "0 90px" }}>
        {/* The voxel hero, cropped from the map screenshot (pixel-perfect upscale). */}
        <div style={{ width: 300, height: 300, overflow: "hidden", position: "relative", opacity: logo, transform: `translateY(${interpolate(logo, [0, 1], [40, 0])}px)` }}>
          <Img
            src={staticFile("shots/arcade-0-map.png")}
            // Hero stands at about (540, 1968) in the 1080×2400 map frame; scale 1.6 and centre it in the 300 px box.
            style={{ position: "absolute", width: 1080 * 1.6, left: 150 - 540 * 1.6, top: 150 - 1968 * 1.6, imageRendering: "pixelated" }}
          />
        </div>
        <div
          style={{
            fontFamily: FONT_DISPLAY,
            fontWeight: 400, // Silkscreen Bold fills the O and D counters: "ODLET" reads as blobs
            fontSize: 150,
            color: C.teal,
            letterSpacing: 4,
            textShadow: `0 8px 0 ${C.tealDeep}, 0 16px 0 #0B3B4A, 0 0 60px rgba(17,197,179,0.45)`,
            transform: `scale(${interpolate(logo, [0, 1], [1.2, 1])})`,
            opacity: logo,
          }}
        >
          ODLET
        </div>
        <div style={{ fontFamily: FONT_DISPLAY, fontSize: 64, color: C.yellow, opacity: tag }}>SOLVE TO STRIKE</div>
        <div
          style={{
            marginTop: 30,
            fontFamily: FONT_DISPLAY,
            fontWeight: 700,
            fontSize: 48,
            color: C.bg0,
            background: `linear-gradient(90deg, ${C.teal}, #7FE8DC)`,
            padding: "30px 44px",
            boxShadow: `0 10px 0 ${C.tealDeep}, 0 0 60px rgba(17,197,179,0.5)`,
            opacity: btn,
            transform: `translateY(${interpolate(btn, [0, 1], [30, 0])}px) scale(${pulse})`,
            textAlign: "center",
            whiteSpace: "pre-line",
          }}
        >
          {cta}
        </div>
        {small ? <div style={{ fontFamily: FONT_BODY, fontSize: 32, color: C.muted, opacity: btn }}>{small}</div> : null}
      </AbsoluteFill>
    </AbsoluteFill>
  );
};
