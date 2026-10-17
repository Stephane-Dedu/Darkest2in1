# Presentation video

An 80-second presentation of Darkest2in1, made with [Remotion](https://www.remotion.dev/). It sets the official Darkest
Dungeon and Darkest Dungeon II trailers against footage of the mod:

| Time | Chapter | What changes |
| --- | --- | --- |
| 0:00 | Cold open, title | DD1's Ancestor ("Ruin has come to our family"), then DD2's Academic ("…the fractured bones of this world can be mended") |
| 0:09 | I · The Hamlet | DD1's painted Hamlet wipes, right to left, into the 3D Hamlet built from DD2 models |
| 0:20 | II · The Estate Map | DD1's estate map wipes into the painted 3D valley and its quest board |
| 0:31 | III · The Expedition | DD2's stagecoach, DD1's corridors and battles, then our corridors and a battle that starts in place |
| 1:05 | Recap, end card | Five-line summary, repository link, requirements and the Red Hook disclaimer |

No footage or game art is committed: `public/media/` and `out/` are ignored. They hold Red Hook's trailers and art,
cut locally from sources kept outside the repository.

## Rebuild it

Requirements: Node 20+ and ffmpeg on `PATH`.

1. Put the sources in `../local-art/presentation-v01` next to the repository, or point `DD_MEDIA_SRC` at another
   folder:
   - `trailers/`: `node scripts/prepare-media.mjs --fetch` downloads the four official trailers from the Steam store.
   - `rec/walk-battle-1.mp4`, `rec/battle-1.mp4`: window captures of the mod (`tools/um win record`, then
     `um video mux`), 1600x900. The cut points in `scripts/prepare-media.mjs` match the 2026-10-17 takes. Retime them
     for new captures.
   - `stills/hamlet-3d.png`, `stills/map-3d.png`, `stills/map-3d-ui.png`: in-game screenshots of the Hamlet, the
     valley render and the quest board.
2. `npm install`, then `npm run media` to cut everything into `public/media`.
3. `npm run studio` to preview, or `npm run render` for `out/darkest2in1.mp4` (1920x1080, 30 fps, H.264).

Scene timing is in `src/Trailer.tsx` and `src/scenes/`. The soundtrack is mixed from the trailers in `Trailer.tsx`;
`bed-b` is aligned so the Ancestor's "Now, like me, you are a part of this place" lands on the reveal of our corridors.

## Sharing it

Upload the rendered MP4 to a GitHub release or drop it into the README editor on GitHub (it becomes a
`user-attachments` link). Keep the end-card disclaimer: this is an unofficial fan project, and the trailer footage
belongs to Red Hook Studios.
