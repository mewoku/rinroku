// Copies stills, music and fonts from the repo into public/, then writes
// src/assets.generated.json listing which clips/audio actually exist, so the
// compositions can fall back to Ken Burns stills when a clip is not recorded yet.
import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "..");
const repo = resolve(root, "..", "..");
const pub = join(root, "public");

const STILLS = [
  "arcade-0-map.png",
  "arcade-1-battle.png",
  "arcade-2-battle-hit.png",
  "arcade-3-charm-pick.png",
  "arcade-5-rune-scoring.png",
  "arcade-6-ice-dash.png",
  "arcade-7-beat-crawl.png",
  "seeker-20x9-0-bosses.png",
  "seeker-20x9-0-shop.png",
  "seeker-20x9-1-home.png",
  "seeker-20x9-4-results.png",
];
const WEB_STILLS = ["play-1440.png"];
const AUDIO = [
  "music_herorun_i2.wav", // 140 BPM HeroDisco: 15 s vertical + bumper bed
  "music_world1_i2.wav", // 124 BPM NuDisco, hype layer: trailer 0–15.5 s
  "music_boss_i2.wav", // 160 BPM BossFunk: trailer boss beat
  "music_daily_i2.wav", // 118 BPM NuDisco: trailer Daily → end card
  "stinger_combo.wav",
  "stinger_victory.wav",
  "stinger_levelup.wav",
];
const FONTS = [
  ["client/Assets/Ronriku/Fonts/Silkscreen-Regular.ttf", "Silkscreen-Regular.ttf"],
  ["client/Assets/Ronriku/Fonts/Silkscreen-Bold.ttf", "Silkscreen-Bold.ttf"],
  ["client/Assets/Ronriku/Fonts/PixelifySans.ttf", "PixelifySans.ttf"],
];

function copyAll(list, fromDir, toDir) {
  mkdirSync(toDir, { recursive: true });
  const found = [];
  for (const entry of list) {
    const [from, to] = Array.isArray(entry) ? [join(repo, entry[0]), entry[1]] : [join(fromDir, entry), entry];
    if (existsSync(from)) {
      copyFileSync(from, join(toDir, to));
      found.push(to);
    } else {
      console.warn(`[prepare] missing ${from}`);
    }
  }
  return found;
}

const evidence = join(repo, "docs", "evidence");
const stills = [
  ...copyAll(STILLS, evidence, join(pub, "shots")),
  ...copyAll(WEB_STILLS, join(evidence, "web"), join(pub, "shots")),
];
const audio = copyAll(AUDIO, join(evidence, "audio"), join(pub, "audio"));
if (audio.length === 0) console.warn("[prepare] no music found: run Unity RONRIKU > Export Music WAVs; ads will render silent.");
const fonts = copyAll(FONTS, repo, join(pub, "fonts"));

mkdirSync(join(pub, "clips"), { recursive: true });
const clips = readdirSync(join(pub, "clips"))
  .filter((f) => f.toLowerCase().endsWith(".mp4"))
  .map((f) => f.replace(/\.mp4$/i, ""));

// JPG frame sequences from the Unity "Footage" PlayMode test (client/.../FootageTests.cs):
// public/clips/raw/<name>/00000.jpg … plus fps.txt.
const rawDir = join(pub, "clips", "raw");
const sequences = {};
if (existsSync(rawDir)) {
  for (const name of readdirSync(rawDir)) {
    let files;
    try {
      files = readdirSync(join(rawDir, name)).filter((f) => /^\d{5}\.jpg$/.test(f));
    } catch {
      continue;
    }
    if (files.length === 0) continue;
    const fpsFile = join(rawDir, name, "fps.txt");
    const fps = existsSync(fpsFile) ? parseFloat(readFileSync(fpsFile, "utf8")) || 30 : 30;
    sequences[name] = { count: files.length, fps };
  }
}

writeFileSync(
  join(root, "src", "assets.generated.json"),
  JSON.stringify({ clips, sequences, audio, stills, fonts }, null, 2) + "\n",
);
const seqList = Object.entries(sequences).map(([n, v]) => `${n}(${v.count}f@${v.fps})`);
console.log(
  `[prepare] mp4 clips: ${clips.length ? clips.join(", ") : "none"} · frame sequences: ${seqList.length ? seqList.join(", ") : "none"} · audio: ${audio.length} · stills: ${stills.length} (missing footage falls back to Ken Burns stills)`,
);
