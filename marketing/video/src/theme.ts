// Brand tokens (docs/PLAN_V2.md "Pixel Ambient") and timing constants.
export const W = 1080;
export const H = 1920;
export const FPS = 30;

/** HeroRun track is 140 BPM: one beat ≈ 12.86 frames, one 4-beat bar ≈ 51.4 frames. */
export const BEAT = (FPS * 60) / 140;
export const BAR = BEAT * 4;
export const bars = (n: number) => Math.round(n * BAR);
export const sec = (s: number) => Math.round(s * FPS);

export const C = {
  bg0: "#07080B",
  bg1: "#0E1016",
  surface: "#14161D",
  line: "#2A2F3B",
  text: "#E7E8E5",
  muted: "#8A94A6",
  teal: "#11C5B3",
  tealDeep: "#135B73",
  yellow: "#E8DA37",
  chips: "#2F7BFF",
  mult: "#FF3B5C",
} as const;

/** Ambient hue per world / screen, matching the in-game palettes. */
export const PALETTE = {
  lab: { accent: "#11C5B3", ambient: "#0B3B4A" },
  prism: { accent: "#FF4FD8", ambient: "#2A1450" },
  ember: { accent: "#FFB347", ambient: "#4A1E12" },
  frost: { accent: "#8FE3FF", ambient: "#0C2250" },
  boss: { accent: "#FF3B5C", ambient: "#3A0A14" },
  daily: { accent: "#7FD8FF", ambient: "#0E2A66" },
} as const;
export type PaletteName = keyof typeof PALETTE;

export const FONT_DISPLAY = "Silkscreen, 'Courier New', monospace";
export const FONT_BODY = "'Pixelify Sans', 'Courier New', monospace";

/** Vertical safe zone for TikTok/Shorts/Reels UI. */
export const SAFE = { top: 150, bottom: 170, right: 120, left: 64 } as const;
