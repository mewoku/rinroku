import React from "react";
import { Audio, interpolate, Sequence, staticFile, useVideoConfig } from "remotion";
import { hasAudio } from "../assets";

/** A music bed from the game's exported WAVs; silently skipped if the file is missing. */
export const Music: React.FC<{ file: string; from?: number; duration?: number; volume?: number; fadeOut?: number }> = ({
  file,
  from = 0,
  duration,
  volume = 0.8,
  fadeOut = 15,
}) => {
  const { durationInFrames } = useVideoConfig();
  if (!hasAudio(file)) return null;
  const len = duration ?? durationInFrames - from;
  return (
    <Sequence from={from} durationInFrames={len} layout="none">
      <Audio
        src={staticFile(`audio/${file}`)}
        volume={(f) => interpolate(f, [0, 6, len - fadeOut, len], [0, volume, volume, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" })}
      />
    </Sequence>
  );
};

/** A one-shot stinger (combo / victory). */
export const Sting: React.FC<{ file: string; at: number; volume?: number }> = ({ file, at, volume = 1 }) => {
  if (!hasAudio(file)) return null;
  return (
    <Sequence from={at} layout="none">
      <Audio src={staticFile(`audio/${file}`)} volume={volume} />
    </Sequence>
  );
};
