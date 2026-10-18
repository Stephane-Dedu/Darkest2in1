import React from "react";
import { AbsoluteFill, Easing, Freeze, Img, interpolate, OffthreadVideo, staticFile, useCurrentFrame } from "remotion";
import { FPS, HEIGHT, WIDTH } from "../theme";

/** A footage cut from public/media; official trailer clips are muted by default. */
export const Clip: React.FC<{ name: string; seconds: number; rate?: number; fadeIn?: number; volume?: number; style?: React.CSSProperties }> = ({
  name,
  seconds,
  rate = 1,
  fadeIn = 0,
  volume = 0,
  style,
}) => {
  const frame = useCurrentFrame();
  const last = Math.floor((seconds * FPS) / rate) - 2;
  const opacity = fadeIn > 0 ? interpolate(frame, [0, fadeIn], [0, 1], { extrapolateRight: "clamp" }) : 1;
  return (
    <AbsoluteFill style={{ opacity, backgroundColor: "black" }}>
      <Freeze frame={last} active={frame > last}>
        <OffthreadVideo
          src={staticFile(`media/${name}.mp4`)}
          muted={volume === 0}
          volume={(f) => volume * interpolate(f, [0, 3, Math.max(4, last - 3), last], [0, 1, 1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" })}
          playbackRate={rate}
          style={{ width: "100%", height: "100%", objectFit: "cover", ...style }}
        />
      </Freeze>
    </AbsoluteFill>
  );
};

export type Shot = { scale: number; x: number; y: number };

/**
 * A still with a camera move: each key is a frame and a shot (scale and the image point, 0..1, kept at the centre).
 * The translation is clamped so the image always covers the frame.
 */
export const Still: React.FC<{ src: string; keys: [number, Shot][]; style?: React.CSSProperties }> = ({ src, keys, style }) => {
  const frame = useCurrentFrame();
  const frames = keys.map(([f]) => f);
  const pick = (k: keyof Shot) =>
    interpolate(
      frame,
      frames,
      keys.map(([, s]) => s[k]),
      { easing: Easing.inOut(Easing.cubic), extrapolateLeft: "clamp", extrapolateRight: "clamp" },
    );
  const scale = pick("scale");
  const clampT = (t: number, size: number) => Math.max(-(scale - 1) * size * 0.5, Math.min((scale - 1) * size * 0.5, t));
  const tx = clampT(-scale * (pick("x") - 0.5) * WIDTH, WIDTH);
  const ty = clampT(-scale * (pick("y") - 0.5) * HEIGHT, HEIGHT);
  return (
    <AbsoluteFill style={{ overflow: "hidden", backgroundColor: "black" }}>
      <Img
        src={staticFile(`media/${src}`)}
        style={{
          width: "100%",
          height: "100%",
          objectFit: "cover",
          transform: `translate(${tx}px, ${ty}px) scale(${scale})`,
          ...style,
        }}
      />
    </AbsoluteFill>
  );
};

/**
 * DD1 → ours: the new image sweeps in from the right edge to the left behind a soft, torch-lit seam.
 */
export const Wipe: React.FC<{
  from: React.ReactNode;
  to: React.ReactNode;
  start: number;
  duration: number;
  feather?: number;
}> = ({ from, to, start, duration, feather = 18 }) => {
  const frame = useCurrentFrame();
  const p = interpolate(frame, [start, start + duration], [0, 1], {
    easing: Easing.inOut(Easing.sin),
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  const edge = (1 - p) * (100 + feather);
  const mask = `linear-gradient(90deg, transparent ${edge - feather}%, black ${edge}%)`;
  const seam = edge - feather * 0.5;
  const seamOpacity = Math.sin(Math.PI * p);
  return (
    <AbsoluteFill>
      {frame < start + duration ? <AbsoluteFill>{from}</AbsoluteFill> : null}
      <AbsoluteFill style={{ maskImage: mask, WebkitMaskImage: mask }}>{to}</AbsoluteFill>
      {p > 0 && p < 1 ? (
        <AbsoluteFill style={{ pointerEvents: "none" }}>
          <div
            style={{
              position: "absolute",
              top: 0,
              bottom: 0,
              left: `${seam}%`,
              width: 3,
              marginLeft: -1.5,
              opacity: seamOpacity,
              background: "linear-gradient(180deg, rgba(255,179,92,0) 0%, rgba(255,179,92,0.95) 35%, rgba(255,179,92,0.95) 65%, rgba(255,179,92,0) 100%)",
              boxShadow: "0 0 28px 10px rgba(232,145,47,0.55), 0 0 90px 30px rgba(155,33,22,0.35)",
            }}
          />
        </AbsoluteFill>
      ) : null}
    </AbsoluteFill>
  );
};
