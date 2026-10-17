import React from "react";
import { AbsoluteFill, interpolate, random, useCurrentFrame, useVideoConfig } from "remotion";
import { colors } from "../theme";

/** Rising embers, deterministic per seed so every render is identical. */
export const Embers: React.FC<{ count?: number; seed?: string; opacity?: number }> = ({
  count = 46,
  seed = "embers",
  opacity = 1,
}) => {
  const frame = useCurrentFrame();
  const { width, height } = useVideoConfig();
  return (
    <AbsoluteFill style={{ pointerEvents: "none", opacity }}>
      {new Array(count).fill(0).map((_, i) => {
        const r = (k: string) => random(`${seed}-${i}-${k}`);
        const life = 140 + r("life") * 160;
        const t = ((frame + r("phase") * life) % life) / life;
        const x = r("x") * width + Math.sin((frame + r("w") * 400) / (28 + r("s") * 30)) * 26;
        const y = height * (1.05 - t * (0.75 + r("h") * 0.5));
        const size = 2 + r("size") * 4;
        const alpha = Math.sin(Math.PI * t) * (0.35 + r("a") * 0.65);
        return (
          <div
            key={i}
            style={{
              position: "absolute",
              left: x,
              top: y,
              width: size,
              height: size,
              borderRadius: "50%",
              background: r("c") > 0.7 ? colors.ember : colors.torch,
              opacity: alpha,
              boxShadow: `0 0 ${size * 3}px ${size}px rgba(232,145,47,0.45)`,
            }}
          />
        );
      })}
    </AbsoluteFill>
  );
};

/** Film grain and a dark vignette over everything, like DD's painted frame edges. */
export const Grade: React.FC = () => {
  const frame = useCurrentFrame();
  const seed = Math.floor(frame / 2) % 12;
  return (
    <AbsoluteFill style={{ pointerEvents: "none" }}>
      <AbsoluteFill
        style={{
          background: "radial-gradient(ellipse at center, rgba(0,0,0,0) 55%, rgba(0,0,0,0.62) 100%)",
        }}
      />
      <svg width="100%" height="100%" style={{ position: "absolute", opacity: 0.07, mixBlendMode: "overlay" }}>
        <filter id={`grain-${seed}`}>
          <feTurbulence type="fractalNoise" baseFrequency="0.85" numOctaves="2" seed={seed} stitchTiles="stitch" />
        </filter>
        <rect width="100%" height="100%" filter={`url(#grain-${seed})`} />
      </svg>
    </AbsoluteFill>
  );
};

/** A warm light that breathes like a torch. */
export const TorchGlow: React.FC<{ x?: string; y?: string; strength?: number }> = ({
  x = "50%",
  y = "48%",
  strength = 0.5,
}) => {
  const frame = useCurrentFrame();
  const flicker =
    0.8 + 0.12 * Math.sin(frame / 3.1) + 0.08 * Math.sin(frame / 1.7 + 1) + 0.06 * (random(`flick-${frame}`) - 0.5);
  return (
    <AbsoluteFill
      style={{
        pointerEvents: "none",
        background: `radial-gradient(circle at ${x} ${y}, rgba(232,145,47,${strength * flicker}) 0%, rgba(155,33,22,${
          strength * 0.35 * flicker
        }) 28%, rgba(0,0,0,0) 62%)`,
      }}
    />
  );
};

/** Opacity for an element shown between two frames, with fades at each end. */
export const fadeWindow = (frame: number, start: number, end: number, fade = 12) =>
  interpolate(frame, [start, start + fade, end - fade, end], [0, 1, 1, 0], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
