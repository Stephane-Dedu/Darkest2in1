// Cuts the presentation's footage into public/media (gitignored).
//
//   node scripts/prepare-media.mjs [--fetch]
//
// Sources live outside the repository, in DD_MEDIA_SRC (default: ../local-art/presentation-v01 next to the repo):
//   trailers/   official Steam trailers (--fetch downloads them from the Steam store API with ffmpeg)
//   rec/        our own window captures of the mod (tools/um win record), muxed to .mp4
//   stills/     in-game screenshots of the 3D Hamlet and the expedition map
// Nothing produced here may be committed: it is Red Hook's footage and art.

import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "..");
const src = resolve(process.env.DD_MEDIA_SRC ?? join(root, "..", "..", "local-art", "presentation-v01"));
const out = join(root, "public", "media");
mkdirSync(out, { recursive: true });

// Steam store movie ids of the official trailers we quote.
const trailers = {
  "dd1-release": { app: 262060, movie: 256659907 },
  "dd1-ea-launch": { app: 262060, movie: 2036992 },
  "dd1-house-of-ruin": { app: 262060, movie: 2036155 },
  "dd2-release": { app: 1940340, movie: 256945472 },
};

// [output, source file, start s, end s]
const clips = [
  ["dd1-hamlet", "trailers/dd1-release.mp4", 29.6, 32.35],
  ["dd1-map", "trailers/dd1-ea-launch.mp4", 23.0, 24.85],
  ["dd2-coach", "trailers/dd2-release.mp4", 6.6, 9.45],
  ["dd1-corridor", "trailers/dd1-house-of-ruin.mp4", 81.5, 85.5],
  ["dd1-battle", "trailers/dd1-house-of-ruin.mp4", 88.6, 92.4],
  ["our-walk", "rec/walk-battle-1.mp4", 47.0, 56.0],
  ["our-enter", "rec/walk-battle-1.mp4", 94.5, 100.05],
  ["our-punish", "rec/battle-1.mp4", 39.4, 41.5],
  ["our-judgement", "rec/battle-1.mp4", 45.4, 47.4],
  ["our-chop", "rec/battle-1.mp4", 66.4, 68.1],
  ["our-deathblow", "rec/battle-1.mp4", 79.4, 82.0],
];

// [output, source file, start s, end s]: narration lines and music beds, kept as WAV.
const sounds = [
  ["vo-ruin", "trailers/dd1-house-of-ruin.mp4", 7.3, 11.0],
  ["vo-mended", "trailers/dd2-release.mp4", 40.6, 45.6],
  ["bed-a", "trailers/dd1-ea-launch.mp4", 3.5, 40.5],
  ["bed-b", "trailers/dd1-release.mp4", 35.2, 70.0],
];

// [output, source file]
const stills = [
  ["our-hamlet.png", "stills/hamlet-3d.png"],
  ["our-map.png", "stills/map-3d.png"],
  ["our-map-ui.png", "stills/map-3d-ui.png"],
];

const ffmpeg = (args) => execFileSync("ffmpeg", ["-nostdin", "-hide_banner", "-loglevel", "error", "-y", ...args], { stdio: "inherit" });

if (process.argv.includes("--fetch")) {
  mkdirSync(join(src, "trailers"), { recursive: true });
  for (const [name, { app, movie }] of Object.entries(trailers)) {
    const target = join(src, "trailers", `${name}.mp4`);
    if (existsSync(target)) continue;
    const res = await fetch(`https://store.steampowered.com/api/appdetails?appids=${app}`);
    const movies = (await res.json())[app].data.movies;
    const hls = movies.find((m) => m.id === movie)?.hls_h264;
    if (!hls) throw new Error(`trailer ${name} (${movie}) is no longer listed on Steam`);
    console.log(`fetching ${name}`);
    ffmpeg(["-i", hls, "-map", "0:v:0", "-map", "0:a:0", "-c", "copy", target]);
  }
}

for (const [name, file, from, to] of clips) {
  console.log(`clip ${name}`);
  // 1080p30, a keyframe every half second for exact seeking, no audio (the soundtrack is mixed in Remotion).
  ffmpeg(["-ss", `${from}`, "-to", `${to}`, "-i", join(src, file), "-an",
    "-vf", "scale=1920:1080:flags=lanczos,fps=30,format=yuv420p",
    "-c:v", "libx264", "-preset", "slow", "-crf", "16", "-g", "15", join(out, `${name}.mp4`)]);
}

for (const [name, file, from, to] of sounds) {
  console.log(`sound ${name}`);
  ffmpeg(["-ss", `${from}`, "-to", `${to}`, "-i", join(src, file), "-vn", "-ac", "2", "-ar", "48000", join(out, `${name}.wav`)]);
}

for (const [name, file] of stills) {
  console.log(`still ${name}`);
  ffmpeg(["-i", join(src, file), join(out, name)]);
}
console.log(`media ready in ${out}`);
