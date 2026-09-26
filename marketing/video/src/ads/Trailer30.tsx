import React from "react";
import { AbsoluteFill, interpolate, Sequence, useCurrentFrame } from "remotion";
import { Ambient } from "../components/Ambient";
import { EndCard } from "../components/EndCard";
import { Slam } from "../components/Kinetic";
import { Phone } from "../components/Phone";
import { Shot } from "../components/Shot";
import { Music, Sting } from "../components/Sound";
import { ShotId } from "../shots";
import { C, FONT_BODY, PALETTE, PaletteName, sec } from "../theme";

/**
 * Concept B, "Five worlds. One mind.": 30 s dApp Store / Seeker trailer, phone-framed on the ambient backdrop.
 * 0–15.5 s cut on 124 BPM bars (World 1 NuDisco, hype layer), boss on the 160 BPM boss track, then the Daily track.
 */
export const TRAILER30_FRAMES = 900;
const W1_BAR = (4 * 60) / 124; // 1.935 s

type Scene = { from: number; to: number; shot: ShotId; palette: PaletteName; text: string; small?: string };

const b = (n: number) => +(n * W1_BAR).toFixed(3);
const SCENES: Scene[] = [
  { from: 0, to: b(1.5), shot: "map-walk", palette: "lab", text: "5 WORLDS.\n60 GUARDIANS." },
  { from: b(1.5), to: b(3), shot: "battle-combo", palette: "prism", text: "EVERY ANSWER\nIS AN ATTACK" },
  { from: b(3), to: b(4), shot: "charm-pick", palette: "lab", text: "PLAY A HAND" },
  { from: b(4), to: b(5.5), shot: "rune-play", palette: "lab", text: "CHIPS X MULT" },
  { from: b(5.5), to: b(6.5), shot: "ice-dash", palette: "frost", text: "SLIDE TO PAR" },
  { from: b(6.5), to: 15.5, shot: "beat-crawl", palette: "ember", text: "FIGHT ON\nTHE BEAT" },
  { from: 15.5, to: 18.5, shot: "boss", palette: "boss", text: "WEEKLY\nBOSS RAIDS" },
  { from: 18.5, to: 22.5, shot: "daily", palette: "daily", text: "ONE DAILY.\nA REAL RATING." },
  { from: 22.5, to: 26.5, shot: "shop", palette: "prism", text: "COLLECT\nVOXEL HEROES", small: "Collectibles on Solana (devnet during early access)" },
];
const END_FROM = 26.5;

const SceneView: React.FC<{ s: Scene; first: boolean }> = ({ s, first }) => {
  const frame = useCurrentFrame();
  const len = sec(s.to) - sec(s.from);
  // Short cross-fade so palette changes read as a mood shift, not a jump.
  const opacity = interpolate(frame, [0, 5, len - 5, len], [first ? 1 : 0, 1, 1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  return (
    <AbsoluteFill style={{ opacity }}>
      <Ambient palette={s.palette} />
      <AbsoluteFill style={{ alignItems: "center", justifyContent: "flex-end", paddingBottom: 140 }}>
        <Phone height={1200} accent={PALETTE[s.palette].accent} rise={first}>
          <Shot id={s.shot} drift={Math.round(s.from) % 2 === 0 ? "in" : "out"} />
        </Phone>
      </AbsoluteFill>
      <Slam text={s.text} delay={3} size={104} color={s.palette === "boss" ? PALETTE.boss.accent : C.text} />
      {s.small ? (
        <div style={{ position: "absolute", left: 64, right: 120, bottom: 64, textAlign: "center", fontFamily: FONT_BODY, fontSize: 30, color: C.text, opacity: 0.8 }}>
          {s.small}
        </div>
      ) : null}
    </AbsoluteFill>
  );
};

export const Trailer30: React.FC = () => (
  <AbsoluteFill style={{ background: C.bg0 }}>
    {SCENES.map((s, i) => (
      <Sequence key={i} from={sec(s.from)} durationInFrames={sec(s.to) - sec(s.from)} premountFor={30}>
        <SceneView s={s} first={i === 0} />
      </Sequence>
    ))}
    <Sequence from={sec(END_FROM)} premountFor={30}>
      <EndCard cta={"FREE ON THE\nSOLANA DAPP STORE"} small="Also playable in your browser" />
    </Sequence>

    <Music file="music_world1_i2.wav" from={0} duration={sec(15.6)} volume={0.8} fadeOut={8} />
    <Music file="music_boss_i2.wav" from={sec(15.5)} duration={sec(3.1)} volume={0.8} fadeOut={6} />
    <Music file="music_daily_i2.wav" from={sec(18.5)} duration={sec(11.5)} volume={0.75} fadeOut={36} />
    <Sting file="stinger_combo.wav" at={sec(b(1.5)) + 28} volume={0.9} />
    <Sting file="stinger_combo.wav" at={sec(b(4)) + 63} volume={0.9} />
    <Sting file="stinger_levelup.wav" at={sec(END_FROM)} volume={0.8} />
  </AbsoluteFill>
);
