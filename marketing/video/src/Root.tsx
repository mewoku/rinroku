import React from "react";
import { Composition } from "remotion";
import { Bumper6, BUMPER6_FRAMES } from "./ads/Bumper6";
import { Tiktok15, TIKTOK15_FRAMES } from "./ads/Tiktok15";
import { Trailer30, TRAILER30_FRAMES } from "./ads/Trailer30";
import { ensureFonts } from "./assets";
import { FPS, H, W } from "./theme";

ensureFonts();

export const Root: React.FC = () => (
  <>
    <Composition id="Tiktok15" component={Tiktok15} durationInFrames={TIKTOK15_FRAMES} fps={FPS} width={W} height={H} />
    <Composition id="Trailer30" component={Trailer30} durationInFrames={TRAILER30_FRAMES} fps={FPS} width={W} height={H} />
    <Composition id="Bumper6" component={Bumper6} durationInFrames={BUMPER6_FRAMES} fps={FPS} width={W} height={H} />
  </>
);
