import React from "react";
import { AbsoluteFill, Sequence } from "remotion";
import { EndCard } from "../components/EndCard";
import { Shake, Slam } from "../components/Kinetic";
import { Shot } from "../components/Shot";
import { Music, Sting } from "../components/Sound";
import { C } from "../theme";

/**
 * Concept C, "Hit.": 6 s bumper. SOLVE. (tap the answer, first hit) → STRIKE. (the real 25 × 4 = -100 KO) → logo.
 * Uses the game's own chips × mult box from the footage rather than an overlay, so the numbers are real.
 */
export const BUMPER6_FRAMES = 180;
const SOLVE_HIT = 29; // -20 pop in battle.mp4 @ 4.6 s + ~0.95 s
const KO_HIT = 25; // -100 KO in battle.mp4 @ 13.9 s + ~0.83 s

export const Bumper6: React.FC = () => (
  <AbsoluteFill style={{ background: C.bg0 }}>
    <Sequence from={0} durationInFrames={42} premountFor={10}>
      <Shake at={SOLVE_HIT} amp={22}>
        <Shot id="battle-combo" zoom={1.06} />
      </Shake>
      <AbsoluteFill style={{ background: "linear-gradient(180deg, rgba(7,8,11,0.82) 0%, rgba(7,8,11,0.35) 24%, transparent 36%)" }} />
      <Slam text="SOLVE." size={170} delay={1} />
    </Sequence>
    <Sequence from={42} durationInFrames={48} premountFor={10}>
      <Shake at={KO_HIT} amp={26}>
        <Shot id="battle-ko" offset={-0.35} punch zoom={1.06} />
      </Shake>
      <AbsoluteFill style={{ background: "linear-gradient(180deg, rgba(7,8,11,0.82) 0%, rgba(7,8,11,0.35) 24%, transparent 36%)" }} />
      <Slam text="STRIKE." size={170} color={C.yellow} delay={KO_HIT - 2} />
    </Sequence>
    <Sequence from={90} premountFor={10}>
      <EndCard cta="ON SEEKER" small="Solana dApp Store" />
    </Sequence>

    <Music file="music_herorun_i2.wav" volume={0.45} fadeOut={20} />
    <Sting file="stinger_combo.wav" at={SOLVE_HIT} />
    <Sting file="stinger_victory.wav" at={42 + KO_HIT} />
  </AbsoluteFill>
);
