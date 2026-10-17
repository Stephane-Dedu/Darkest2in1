import React from "react";
import { AbsoluteFill, Easing, interpolate, Sequence, useCurrentFrame } from "remotion";
import { Embers, TorchGlow } from "../components/Atmosphere";
import { Clip, Wipe } from "../components/Media";
import { Caption, Subtitle } from "../components/Text";
import { colors, fonts } from "../theme";

/** The chapter transition: a torch flares up in the dark and names the expedition. */
export const ExpeditionCard: React.FC = () => {
  const frame = useCurrentFrame();
  const flare = interpolate(frame, [0, 14, 48], [0, 1, 0.55], { extrapolateRight: "clamp" });
  const text = interpolate(frame, [6, 18, 38, 48], [0, 1, 1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const scale = interpolate(frame, [0, 48], [1.08, 1], { easing: Easing.out(Easing.cubic) });
  return (
    <AbsoluteFill style={{ backgroundColor: colors.ink }}>
      <TorchGlow strength={0.6 * flare} />
      <Embers count={60} seed="expedition" opacity={flare} />
      <AbsoluteFill style={{ justifyContent: "center", alignItems: "center", opacity: text, transform: `scale(${scale})` }}>
        <div style={{ display: "flex", alignItems: "baseline", gap: 30 }}>
          <span style={{ fontFamily: fonts.caps, fontSize: 112, color: colors.torch }}>III</span>
          <span style={{ fontFamily: fonts.caps, fontSize: 92, letterSpacing: 6, color: colors.bone }}>The Expedition</span>
        </div>
      </AbsoluteFill>
    </AbsoluteFill>
  );
};

// Scene frames of each cut (30 fps).
const COACH = 0;
const CORRIDOR = 100;
const DD1_BATTLE = 220;
const REVEAL = 80; // wipe start, relative to DD1_BATTLE
const ENTER = 570;
const PUNISH = 736;
const JUDGEMENT = 799;
const CHOP = 859;
const DEATHBLOW = 910;
export const EXPEDITION_FRAMES = 988;

/** DD2's road and DD1's halls, then our corridors and a battle that starts where the party stands. */
export const ExpeditionScene: React.FC = () => (
  <AbsoluteFill style={{ backgroundColor: "black" }}>
    <Sequence from={COACH} durationInFrames={CORRIDOR - COACH + 10}>
      <Clip name="dd2-coach" seconds={2.85} rate={0.85} fadeIn={8} />
    </Sequence>
    <Sequence from={CORRIDOR} durationInFrames={DD1_BATTLE - CORRIDOR + 10}>
      <Clip name="dd1-corridor" seconds={4} fadeIn={8} />
    </Sequence>
    <Sequence from={DD1_BATTLE} durationInFrames={ENTER - DD1_BATTLE}>
      <Wipe
        start={REVEAL}
        duration={46}
        from={<Clip name="dd1-battle" seconds={3.8} rate={0.9} fadeIn={8} />}
        to={
          <Sequence from={REVEAL}>
            <Clip name="our-walk" seconds={9} />
          </Sequence>
        }
      />
    </Sequence>
    <Sequence from={ENTER} durationInFrames={PUNISH - ENTER}>
      <Clip name="our-enter" seconds={5.55} />
    </Sequence>
    <Sequence from={PUNISH} durationInFrames={JUDGEMENT - PUNISH}>
      <Clip name="our-punish" seconds={2.1} />
    </Sequence>
    <Sequence from={JUDGEMENT} durationInFrames={CHOP - JUDGEMENT}>
      <Clip name="our-judgement" seconds={2.0} />
    </Sequence>
    <Sequence from={CHOP} durationInFrames={DEATHBLOW - CHOP}>
      <Clip name="our-chop" seconds={1.7} />
    </Sequence>
    <Sequence from={DEATHBLOW} durationInFrames={EXPEDITION_FRAMES - DEATHBLOW}>
      <Clip name="our-deathblow" seconds={2.6} />
    </Sequence>

    <Caption game="dd2" text="Heroes, the stagecoach and the road" start={COACH + 8} end={CORRIDOR - 4} />
    <Caption game="dd1" text="Rooms and corridors, by torchlight" start={CORRIDOR + 8} end={DD1_BATTLE - 4} />
    <Caption game="dd1" text="Battles fought in the halls where the party stands" start={DD1_BATTLE + 8} end={DD1_BATTLE + REVEAL} />
    <Caption
      game="ours"
      text="DD1's rooms and corridors, walked by DD2's 3D heroes through DD2's regions"
      start={DD1_BATTLE + REVEAL + 50}
      end={ENTER - 6}
    />
    <Caption game="ours" text="Battles begin where you stand: no map load, no loading screen" start={ENTER + 8} end={PUNISH - 6} />
    <Caption game="ours" text="Darkest Dungeon II's heroes, skills and finishing blows" start={PUNISH + 4} end={EXPEDITION_FRAMES - 6} />
    {/* The Ancestor's line from DD1's release trailer lands on the reveal (bed-b, see Trailer.tsx). */}
    <Subtitle text="“Now, like me, you are a part of this place.”" start={351} end={556} top={58} size={42} />
  </AbsoluteFill>
);
