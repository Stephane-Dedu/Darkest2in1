import React from "react";
import { AbsoluteFill, interpolate, useCurrentFrame } from "remotion";
import { Clip, Still, Wipe } from "../components/Media";
import { Caption, ChapterTag } from "../components/Text";

/** DD1's painted Hamlet gives way, right to left, to our 3D Hamlet; the camera then pulls back to the whole screen. */
export const HamletScene: React.FC = () => (
  <AbsoluteFill>
    <Wipe
      start={80}
      duration={46}
      from={<Clip name="dd1-hamlet" seconds={2.75} rate={0.8} />}
      to={
        <Still
          src="our-hamlet.png"
          style={{ filter: "brightness(1.18) contrast(1.04)" }}
          keys={[
            [80, { scale: 1.38, x: 0.42, y: 0.51 }],
            [205, { scale: 1.45, x: 0.45, y: 0.5 }],
            [268, { scale: 1.0, x: 0.5, y: 0.5 }],
            [330, { scale: 1.02, x: 0.5, y: 0.5 }],
          ]}
        />
      }
    />
    <ChapterTag numeral="I" title="The Hamlet" start={4} end={205} />
    <Caption game="dd1" text="The painted Hamlet, where every expedition begins" start={10} end={86} />
    <Caption game="ours" text="Its ten services, rebuilt as a 3D town from Darkest Dungeon II's own models" start={126} end={252} />
  </AbsoluteFill>
);

/** DD1's estate map, then our painted 3D valley and the quest board on it. */
export const MapScene: React.FC = () => {
  const frame = useCurrentFrame();
  const board = interpolate(frame, [182, 210], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  return (
    <AbsoluteFill>
      <Wipe
        start={56}
        duration={46}
        from={<Clip name="dd1-map" seconds={1.85} rate={0.6} />}
        to={
          <Still
            src="our-map.png"
            keys={[
              [56, { scale: 1.1, x: 0.5, y: 0.47 }],
              [210, { scale: 1.24, x: 0.48, y: 0.4 }],
            ]}
          />
        }
      />
      <AbsoluteFill style={{ opacity: board }}>
        <Still
          src="our-map-ui.png"
          keys={[
            [182, { scale: 1.06, x: 0.56, y: 0.48 }],
            [312, { scale: 1.0, x: 0.5, y: 0.5 }],
          ]}
        />
      </AbsoluteFill>
      <ChapterTag numeral="II" title="The Estate Map" start={4} end={180} />
      <Caption game="dd1" text="Choose an area, a quest and a party" start={10} end={60} />
      <Caption game="ours" text="A painted 3D valley: Darkest Dungeon II's regions, each paired with a DD1 area" start={104} end={196} />
      <Caption
        game="ours"
        text="Sprawl · Ruins, Foetor · Warrens, Tangle · Weald, Shroud · Cove: each with its own XP, quests and bosses"
        start={202}
        end={308}
      />
    </AbsoluteFill>
  );
};
