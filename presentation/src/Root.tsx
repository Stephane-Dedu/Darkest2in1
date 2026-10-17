import React from "react";
import { Composition } from "remotion";
import { FPS, HEIGHT, WIDTH } from "./theme";
import { TOTAL_FRAMES, Trailer } from "./Trailer";

export const Root: React.FC = () => (
  <Composition id="Darkest2in1" component={Trailer} durationInFrames={TOTAL_FRAMES} fps={FPS} width={WIDTH} height={HEIGHT} />
);
