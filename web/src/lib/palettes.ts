/** World / trial palettes from PLAN_V2 §2. Keep in sync with src/styles/tokens.css. */
export type PaletteName = "lab" | "pattern" | "link" | "forest" | "frost" | "boss";

export interface Palette {
  name: PaletteName;
  label: string;
  accent: string;
  accent2: string;
  ambient: string;
}

export const PALETTES: Record<PaletteName, Palette> = {
  lab: { name: "lab", label: "Lab", accent: "#11C5B3", accent2: "#135B73", ambient: "#0B3B4A" },
  pattern: { name: "pattern", label: "Pattern", accent: "#FF4FD8", accent2: "#7B5CFF", ambient: "#2A1450" },
  link: { name: "link", label: "Link", accent: "#FFB347", accent2: "#FF6B5A", ambient: "#4A1E12" },
  forest: { name: "forest", label: "Forest", accent: "#9BE35A", accent2: "#2F8F4E", ambient: "#10301A" },
  frost: { name: "frost", label: "Frost", accent: "#8FE3FF", accent2: "#3A6BFF", ambient: "#0C2250" },
  boss: { name: "boss", label: "Boss", accent: "#FF3B5C", accent2: "#FFC83D", ambient: "#3A0A14" },
};

/** Adventure worlds (Unity Adventure.WorldNames / core WORLD_NAMES) → palette. */
export const WORLD_PALETTES: { name: "LAB" | "PRISM" | "EMBER" | "GROVE" | "FROST"; palette: PaletteName; blurb: string }[] = [
  { name: "LAB", palette: "lab", blurb: "Clean rooms, cubes and shadows." },
  { name: "PRISM", palette: "pattern", blurb: "Rules hidden in light." },
  { name: "EMBER", palette: "link", blurb: "Paths through the embers." },
  { name: "GROVE", palette: "forest", blurb: "Mossy mazes, patient monsters." },
  { name: "FROST", palette: "frost", blurb: "The coldest logic." },
];

export const BRAND ={ teal: "#11C5B3", yellow: "#E8DA37" } as const;

export function hexToRgb(hex: string): [number, number, number] {
  const n = parseInt(hex.replace("#", ""), 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}
