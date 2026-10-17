import React from "react";
import { AbsoluteFill, Easing, interpolate, useCurrentFrame } from "remotion";
import { colors, fonts } from "../theme";
import { fadeWindow } from "./Atmosphere";

export type Game = "dd1" | "dd2" | "ours";

const games: Record<Game, { name: string; accent: string }> = {
  dd1: { name: "Darkest Dungeon", accent: colors.ash },
  dd2: { name: "Darkest Dungeon II", accent: colors.gold },
  ours: { name: "Darkest2in1", accent: colors.torch },
};

/** Lower-left caption naming the game on screen and what it shows. */
export const Caption: React.FC<{ game: Game; text: string; start: number; end: number }> = ({ game, text, start, end }) => {
  const frame = useCurrentFrame();
  const opacity = fadeWindow(frame, start, end, 10);
  const rise = interpolate(frame, [start, start + 14], [18, 0], {
    easing: Easing.out(Easing.cubic),
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  if (opacity <= 0) return null;
  const { name, accent } = games[game];
  return (
    <AbsoluteFill style={{ pointerEvents: "none", opacity }}>
      <AbsoluteFill
        style={{ background: "linear-gradient(0deg, rgba(5,4,3,0.88) 0%, rgba(5,4,3,0.55) 20%, rgba(0,0,0,0) 38%)" }}
      />
      <div style={{ position: "absolute", left: 96, bottom: 84, maxWidth: 1180, transform: `translateY(${rise}px)` }}>
        <div style={{ display: "flex", alignItems: "center", gap: 18 }}>
          <div style={{ width: 46, height: 3, background: accent, boxShadow: `0 0 14px ${accent}` }} />
          <div
            style={{
              fontFamily: fonts.caps,
              fontSize: 34,
              letterSpacing: 4,
              color: accent,
              textShadow: "0 2px 10px rgba(0,0,0,0.9)",
            }}
          >
            {name}
          </div>
        </div>
        <div
          style={{
            marginTop: 10,
            fontFamily: fonts.body,
            fontWeight: 500,
            fontSize: 40,
            lineHeight: 1.25,
            color: colors.bone,
            textShadow: "0 2px 12px rgba(0,0,0,0.95)",
          }}
        >
          {text}
        </div>
      </div>
    </AbsoluteFill>
  );
};

/** Top-left chapter mark: a roman numeral and the chapter name. */
export const ChapterTag: React.FC<{ numeral: string; title: string; start: number; end: number }> = ({
  numeral,
  title,
  start,
  end,
}) => {
  const frame = useCurrentFrame();
  const opacity = fadeWindow(frame, start, end, 14);
  if (opacity <= 0) return null;
  const rule = interpolate(frame, [start, start + 24], [0, 260], {
    easing: Easing.out(Easing.cubic),
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  return (
    <AbsoluteFill style={{ pointerEvents: "none", opacity }}>
      <AbsoluteFill style={{ background: "linear-gradient(180deg, rgba(5,4,3,0.75) 0%, rgba(0,0,0,0) 22%)" }} />
      <div style={{ position: "absolute", left: 96, top: 62, display: "flex", alignItems: "baseline", gap: 22 }}>
        <div style={{ fontFamily: fonts.caps, fontSize: 60, color: colors.torch, textShadow: "0 0 24px rgba(232,145,47,0.5)" }}>
          {numeral}
        </div>
        <div style={{ fontFamily: fonts.caps, fontSize: 46, letterSpacing: 3, color: colors.bone, textShadow: "0 2px 12px black" }}>
          {title}
        </div>
      </div>
      <div style={{ position: "absolute", left: 96, top: 142, width: rule, height: 2, background: colors.blood }} />
    </AbsoluteFill>
  );
};

/** A spoken line from a trailer, written out for viewers watching without sound. */
export const Subtitle: React.FC<{ text: string; start: number; end: number; top?: number; size?: number }> = ({
  text,
  start,
  end,
  top,
  size = 46,
}) => {
  const frame = useCurrentFrame();
  const opacity = fadeWindow(frame, start, end, 12);
  if (opacity <= 0) return null;
  return (
    <AbsoluteFill style={{ pointerEvents: "none", opacity }}>
      {top !== undefined ? (
        <AbsoluteFill style={{ background: "linear-gradient(180deg, rgba(5,4,3,0.8) 0%, rgba(5,4,3,0.45) 18%, rgba(0,0,0,0) 30%)" }} />
      ) : null}
      <div
        style={{
          position: "absolute",
          left: 0,
          right: 0,
          ...(top !== undefined ? { top } : { bottom: 120 }),
          textAlign: "center",
          fontFamily: fonts.caps,
          fontSize: size,
          letterSpacing: 1.5,
          color: colors.bone,
          textShadow: "0 2px 14px rgba(0,0,0,0.95)",
        }}
      >
        {text}
      </div>
    </AbsoluteFill>
  );
};
