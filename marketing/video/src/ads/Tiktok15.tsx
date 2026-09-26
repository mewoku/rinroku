import React from "react";
import { AbsoluteFill, Sequence } from "remotion";
import { EndCard } from "../components/EndCard";
import { Shake, Slam } from "../components/Kinetic";
import { Shot } from "../components/Shot";
import { Music, Sting } from "../components/Sound";
import { ShotId } from "../shots";
import { bars, C } from "../theme";

/** Concept A, "Solve to strike": 15 s vertical, hook-first, hard cuts on the 140 BPM bar (HeroRun track). */
export const TIKTOK15_FRAMES = 450;

type Beat = {
  from: number; // in bars
  to: number;
  shot: ShotId;
  offset?: number; // seconds into the clip beyond SHOTS[shot].startFrom
  text: string;
  sub?: string;
  color?: string;
  hit?: number; // frame (within the beat) where the on-screen hit lands: shake + stinger
};

const BEATS: Beat[] = [
  { from: 0, to: 1, shot: "battle-combo", text: "YOUR BRAIN\nIS THE WEAPON", hit: 29 },
  { from: 1, to: 2, shot: "battle-combo", offset: 5.7, text: "ANSWER FAST", sub: "combo = more damage", color: C.yellow, hit: 21 },
  { from: 2, to: 3, shot: "rune-play", offset: 0.9, text: "CHIPS X MULT", hit: 36 },
  { from: 3, to: 4, shot: "ice-dash", offset: 0, text: "SLIDE." },
  { from: 4, to: 5, shot: "beat-crawl", offset: 1.0, text: "MOVE ON\nTHE BEAT." },
  { from: 5, to: 5.75, shot: "battle-ko", text: "K.O.", color: C.yellow, hit: 14 },
  { from: 5.75, to: 6.75, shot: "daily", text: "ONE DAILY.\nONE RATING." },
];
const END_FROM = bars(6.75);

export const Tiktok15: React.FC = () => (
  <AbsoluteFill style={{ background: C.bg0 }}>
    {BEATS.map((b, i) => {
      const from = bars(b.from);
      const len = bars(b.to) - from;
      return (
        <Sequence key={i} from={from} durationInFrames={len} premountFor={15}>
          <Shake at={b.hit ?? -99}>
            <Shot id={b.shot} offset={b.offset} punch={i > 0} zoom={b.shot === "rune-play" || b.shot === "battle-ko" ? 1 : 1.06} />
          </Shake>
          <AbsoluteFill style={{ background: "linear-gradient(180deg, rgba(7,8,11,0.82) 0%, rgba(7,8,11,0.35) 24%, transparent 36%)" }} />
          <Slam text={b.text} sub={b.sub} color={b.color} delay={i === 0 ? 2 : 1} />
        </Sequence>
      );
    })}
    <Sequence from={END_FROM} premountFor={15}>
      <EndCard cta={"FREE ON SEEKER"} small="Solana dApp Store · or play in your browser" />
    </Sequence>

    <Music file="music_herorun_i2.wav" volume={0.85} fadeOut={24} />
    {BEATS.filter((b) => b.hit !== undefined).map((b, i) => (
      <Sting key={i} file={b.shot === "battle-ko" ? "stinger_victory.wav" : "stinger_combo.wav"} at={bars(b.from) + (b.hit ?? 0)} volume={0.9} />
    ))}
  </AbsoluteFill>
);
