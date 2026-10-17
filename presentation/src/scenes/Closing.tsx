import React from "react";
import { AbsoluteFill, Easing, interpolate, useCurrentFrame } from "remotion";
import { Embers, TorchGlow } from "../components/Atmosphere";
import { Clip } from "../components/Media";
import { colors, fonts } from "../theme";

const points = [
  "Darkest Dungeon's expedition loop: Hamlet, quests, provisioning, curios, camping, loot, town events",
  "Darkest Dungeon II's heroes, skills and combat",
  "Battles start in place, with no loading screen",
  "DD2 regions paired with DD1 areas, each with its own XP, quests and bosses",
  "No game files shipped: it reads your own DD1 install and patches DD2 in memory",
];

/** What the mod is, in five lines, over the corridor footage. */
export const Recap: React.FC = () => {
  const frame = useCurrentFrame();
  const enter = interpolate(frame, [0, 16], [0, 1], { extrapolateRight: "clamp" });
  const out = interpolate(frame, [208, 225], [1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  return (
    <AbsoluteFill style={{ backgroundColor: colors.ink, opacity: out }}>
      <AbsoluteFill style={{ opacity: enter, filter: "blur(16px) brightness(0.32) saturate(0.8)", transform: "scale(1.08)" }}>
        <Clip name="our-walk" seconds={9} rate={0.6} />
      </AbsoluteFill>
      <Embers count={30} seed="recap" opacity={0.8} />
      <AbsoluteFill style={{ padding: "150px 0 0 230px" }}>
        <div
          style={{
            // Small caps, not blackletter: Grenze Gotisch draws "II" like "DD".
            fontFamily: fonts.caps,
            fontSize: 64,
            letterSpacing: 1,
            whiteSpace: "nowrap",
            color: colors.bone,
            opacity: enter,
            textShadow: "0 0 30px rgba(232,145,47,0.35)",
          }}
        >
          Darkest Dungeon's loop. <span style={{ color: colors.torch }}>Darkest Dungeon II's world.</span>
        </div>
        <div style={{ width: 420 * enter, height: 2, background: colors.blood, margin: "18px 0 48px" }} />
        {points.map((text, i) => {
          const t = interpolate(frame, [22 + i * 16, 38 + i * 16], [0, 1], {
            easing: Easing.out(Easing.cubic),
            extrapolateLeft: "clamp",
            extrapolateRight: "clamp",
          });
          return (
            <div
              key={text}
              style={{
                display: "flex",
                alignItems: "center",
                gap: 28,
                marginBottom: 34,
                opacity: t,
                transform: `translateX(${(1 - t) * -40}px)`,
              }}
            >
              <div style={{ width: 14, height: 14, transform: "rotate(45deg)", background: colors.torch, boxShadow: `0 0 14px ${colors.torch}` }} />
              <div style={{ fontFamily: fonts.body, fontWeight: 500, fontSize: 44, color: colors.bone, textShadow: "0 2px 10px black" }}>{text}</div>
            </div>
          );
        })}
      </AbsoluteFill>
    </AbsoluteFill>
  );
};

/** Name, where to find it, what it needs, and whose footage this is. */
export const EndCard: React.FC = () => {
  const frame = useCurrentFrame();
  const a = (from: number) => interpolate(frame, [from, from + 20], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const out = interpolate(frame, [180, 210], [1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  return (
    <AbsoluteFill style={{ backgroundColor: colors.ink }}>
      <AbsoluteFill style={{ opacity: out }}>
        <TorchGlow y="40%" strength={0.32} />
        <Embers count={44} seed="end" />
        <AbsoluteFill style={{ alignItems: "center", paddingTop: 230 }}>
          <div
            style={{
              fontFamily: fonts.title,
              fontWeight: 700,
              fontSize: 170,
              letterSpacing: 4,
              color: colors.bone,
              opacity: a(0),
              textShadow: "0 0 40px rgba(232,145,47,0.45), 0 6px 30px rgba(0,0,0,0.9)",
            }}
          >
            Darkest<span style={{ color: colors.torch }}>2in1</span>
          </div>
          <div style={{ fontFamily: fonts.caps, fontSize: 48, letterSpacing: 6, color: colors.ash, opacity: a(10), marginTop: 4 }}>
            a fan-made Darkest Dungeon 3
          </div>
          <div style={{ width: 520, height: 2, background: colors.blood, margin: "44px 0 40px", opacity: a(18) }} />
          <div style={{ fontFamily: fonts.body, fontWeight: 700, fontSize: 52, color: colors.torch, letterSpacing: 1, opacity: a(26) }}>
            github.com/Stephane-Dedu/Darkest2in1
          </div>
          <div style={{ fontFamily: fonts.body, fontSize: 34, color: colors.bone, marginTop: 22, opacity: a(36) }}>
            A BepInEx 5 plugin for Darkest Dungeon II · requires Darkest Dungeon and Darkest Dungeon II
          </div>
        </AbsoluteFill>
        <div
          style={{
            position: "absolute",
            left: 0,
            right: 0,
            bottom: 70,
            textAlign: "center",
            fontFamily: fonts.body,
            fontSize: 25,
            lineHeight: 1.5,
            color: colors.ash,
            opacity: a(48),
          }}
        >
          Unofficial fan project, not affiliated with or endorsed by Red Hook Studios.
          <br />
          Darkest Dungeon® and Darkest Dungeon® II, including the trailer footage shown, are © Red Hook Studios Inc.
        </div>
      </AbsoluteFill>
    </AbsoluteFill>
  );
};
