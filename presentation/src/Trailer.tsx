import React from "react";
import { AbsoluteFill, Audio, interpolate, Sequence, staticFile } from "remotion";
import { Grade } from "./components/Atmosphere";
import { Recap, EndCard } from "./scenes/Closing";
import { EXPEDITION_FRAMES, ExpeditionCard, ExpeditionScene } from "./scenes/Expedition";
import { ColdOpen, TitleCard } from "./scenes/Opening";
import { HamletScene, MapScene } from "./scenes/Town";

// [scene, first frame, length] at 30 fps.
const COLD = 0;
const TITLE = 132;
const HAMLET = 288;
const MAP = 618;
const CARD = 930;
const EXPEDITION = 978;
const RECAP = EXPEDITION + EXPEDITION_FRAMES;
const END = RECAP + 225;
export const TOTAL_FRAMES = END + 210;

// Soundtrack, all cut from the official trailers by scripts/prepare-media.mjs.
// bed-b starts so that the Ancestor's "Now, like me, you are a part of this place" (0.44 s in) lands on the reveal of our
// corridors, and its climax on our battle.
const BED_B = 1316;

const ramp = (frame: number, points: number[], values: number[]) =>
  interpolate(frame, points, values, { extrapolateLeft: "clamp", extrapolateRight: "clamp" });

export const Trailer: React.FC = () => (
  <AbsoluteFill style={{ backgroundColor: "black" }}>
    <Sequence from={COLD} durationInFrames={TITLE - COLD}>
      <ColdOpen />
    </Sequence>
    <Sequence from={TITLE} durationInFrames={HAMLET - TITLE}>
      <TitleCard />
    </Sequence>
    <Sequence from={HAMLET} durationInFrames={MAP - HAMLET}>
      <HamletScene />
    </Sequence>
    <Sequence from={MAP} durationInFrames={CARD - MAP}>
      <MapScene />
    </Sequence>
    <Sequence from={CARD} durationInFrames={EXPEDITION - CARD}>
      <ExpeditionCard />
    </Sequence>
    <Sequence from={EXPEDITION} durationInFrames={EXPEDITION_FRAMES}>
      <ExpeditionScene />
    </Sequence>
    <Sequence from={RECAP} durationInFrames={END - RECAP}>
      <Recap />
    </Sequence>
    <Sequence from={END} durationInFrames={TOTAL_FRAMES - END}>
      <EndCard />
    </Sequence>
    <Grade />

    {/* DD1, House of Ruin: "Ruin has come to our family." */}
    <Sequence from={12} durationInFrames={111}>
      <Audio src={staticFile("media/vo-ruin.wav")} volume={(f) => ramp(f, [0, 5, 98, 111], [0, 0.85, 0.85, 0])} />
    </Sequence>
    {/* DD2, release trailer: "That the fractured bones of this world can be mended." */}
    <Sequence from={138} durationInFrames={150}>
      <Audio src={staticFile("media/vo-mended.wav")} volume={(f) => ramp(f, [0, 6, 132, 150], [0, 0.85, 0.85, 0])} />
    </Sequence>
    {/* DD1, Early Access trailer music under the Hamlet, the map and the road; it hands over to bed-b. */}
    <Sequence from={258} durationInFrames={1110}>
      <Audio src={staticFile("media/bed-a.wav")} volume={(f) => ramp(f, [0, 30, BED_B - 258 - 11, BED_B - 258 + 34], [0, 0.64, 0.64, 0])} />
    </Sequence>
    {/* DD1, release trailer: the Ancestor's line, the climax, then the fade under the end card. */}
    <Sequence from={BED_B} durationInFrames={TOTAL_FRAMES - BED_B}>
      <Audio src={staticFile("media/bed-b.wav")} volume={(f) => ramp(f, [0, 12], [0, 0.84])} />
    </Sequence>
  </AbsoluteFill>
);
