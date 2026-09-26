import { continueRender, delayRender, staticFile } from "remotion";
import generated from "./assets.generated.json";

/** Written by scripts/prepare.mjs: which clips / audio / fonts exist right now. */
export const ASSETS = generated as {
  clips: string[];
  sequences?: Record<string, { count: number; fps: number }>;
  audio: string[];
  stills: string[];
  fonts: string[];
};

export const hasClip = (id: string) => ASSETS.clips.includes(id);
export const sequence = (name?: string) => (name ? ASSETS.sequences?.[name] : undefined);
export const hasAudio = (file: string) => ASSETS.audio.includes(file);

const FACES: Array<[family: string, file: string, weight: string]> = [
  ["Silkscreen", "Silkscreen-Regular.ttf", "400"],
  ["Silkscreen", "Silkscreen-Bold.ttf", "700"],
  ["Pixelify Sans", "PixelifySans.ttf", "400 700"],
];

let fontsRequested = false;
/** Load the game's OFL pixel fonts from public/fonts before the first frame renders. */
export function ensureFonts() {
  if (fontsRequested || typeof document === "undefined") return;
  fontsRequested = true;
  const handle = delayRender("Loading pixel fonts");
  const loads = FACES.filter(([, file]) => ASSETS.fonts.includes(file)).map(([family, file, weight]) =>
    new FontFace(family, `url(${staticFile(`fonts/${file}`)})`, { weight })
      .load()
      .then((face) => {
        (document.fonts as unknown as { add(f: FontFace): void }).add(face);
      }),
  );
  Promise.all(loads)
    .catch((err) => console.warn("Font load failed, using fallback", err))
    .finally(() => continueRender(handle));
}
