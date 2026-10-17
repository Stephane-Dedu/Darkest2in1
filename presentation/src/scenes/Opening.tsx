import React from "react";
import { AbsoluteFill, Easing, interpolate, useCurrentFrame } from "remotion";
import { Embers, TorchGlow } from "../components/Atmosphere";
import { Subtitle } from "../components/Text";
import { colors, fonts } from "../theme";

/** Black, a few embers, and the Ancestor's first line. */
export const ColdOpen: React.FC = () => (
  <AbsoluteFill style={{ backgroundColor: colors.ink }}>
    <Embers count={22} seed="cold" opacity={0.7} />
    <Subtitle text="“Ruin has come to our family.”" start={14} end={126} size={54} />
  </AbsoluteFill>
);

/** The title, lit by a torch, while DD2's Academic promises that broken things can be mended. */
export const TitleCard: React.FC = () => {
  const frame = useCurrentFrame();
  const reveal = interpolate(frame, [0, 34], [0, 1], { easing: Easing.out(Easing.cubic), extrapolateRight: "clamp" });
  const tagline = interpolate(frame, [26, 50], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const out = interpolate(frame, [140, 156], [1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const spacing = interpolate(frame, [0, 156], [10, 4]);
  return (
    <AbsoluteFill style={{ backgroundColor: colors.ink, opacity: out }}>
      <TorchGlow y="46%" strength={0.42 * reveal} />
      <Embers count={50} seed="title" />
      <AbsoluteFill style={{ justifyContent: "center", alignItems: "center", flexDirection: "column" }}>
        <div
          style={{
            fontFamily: fonts.title,
            fontWeight: 700,
            fontSize: 210,
            letterSpacing: spacing,
            color: colors.bone,
            opacity: reveal,
            filter: `blur(${(1 - reveal) * 12}px)`,
            textShadow: "0 0 40px rgba(232,145,47,0.45), 0 6px 30px rgba(0,0,0,0.9)",
            marginTop: -60,
          }}
        >
          Darkest<span style={{ color: colors.torch }}>2in1</span>
        </div>
        <div style={{ width: 520 * reveal, height: 2, background: colors.blood, margin: "6px 0 26px" }} />
        <div style={{ fontFamily: fonts.caps, fontSize: 54, letterSpacing: 6, color: colors.ash, opacity: tagline }}>
          a fan-made Darkest Dungeon 3
        </div>
      </AbsoluteFill>
      <Subtitle text="“…that the fractured bones of this world can be mended.”" start={10} end={156} size={36} />
    </AbsoluteFill>
  );
};
