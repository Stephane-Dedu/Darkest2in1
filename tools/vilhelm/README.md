# Sir Vilhelm test hero

Local, optional character work for the owner's DS3/DD2 installation. No model,
texture, animation, native binary or decompiled source belongs in this repository.
The future optional Souls destination is separate work.

`Experimental/VilhelmHero` enables an **Invite Sir Vilhelm** button at the
Stagecoach when `PrivateHeroModels/sir_vilhelm/sir_vilhelm.gltf` exists and DD2's
owned Crusader resource is available. It creates a coach candidate, then uses the
ordinary recruitment flow. It does not replace Crusader or enter random coach rolls.
Existing progress is retained when the option is switched off.
The option controls invitations; an already recruited visitor still registers
and renders while the private pack remains installed.

The test class has independent class/stat/skill entries and owned skill resources.
Its five abilities currently retain DD2 Crusader combat numbers, targeting,
conditions and upgrade rules. These are a combat integration baseline, not a
finished translation of DS3's damage or casting systems.

| Ability | Native rules | Original motion |
| --- | --- | --- |
| Onyx Slash | Smite | `a025_030000` |
| Onyx Sweep | Reap | `a025_030010` |
| Great Heal | Battle Heal | `a000_042003`, `042103`, `042303` |
| Elfriede's Blackflame | Radiance | `a245_038700` |
| Advancing Strike | Holy Lance | `a025_030100` |

Idle/walk use `a002_000000`/`020000`; front impact uses `a000_005002`;
death/corpse clips are `a000_017002`/`018002`. The original armor meshes, texture
UVs, weights and c0000 animation rig are retained. The Onyx Blade equipment mount
converts its native negative-Y blade axis to the attachment's positive-Z axis,
around the `R_Weapon` grip. No hand or animation track is rotated to compensate.
The old `004000` pose remains in the pack for compatibility. It has less than 3 mm
of forward foot travel and is unsuitable for walking. The actual locomotion clip
has 41 original frames and a native 2 m movement curve over 1.333333 s. `motion.json`
stores that extracted curve. Signed rendered ground travel advances its inverse,
including fitted model scale, stops and reverse. No walk tracks are retargeted.

Native controller phases and actor Timeline clips time the source motions. Antic,
action and recovery use contiguous source segments, including native action clips
without an `_action` suffix and recovery names which already contain `action`.
Crossfades join stance, walking, attacks and impacts. The character shader receives
only active native material controllers. An albedo-derived shading map adapts the
native diffuse texture to DD2's painted hero shader; original color remains separate.
The runtime pack now uses native diffuse PNGs, rather than the extraction kit's
specular-to-metallic PBR color bake. `GltfModel` already converts the image origin;
the renderer must not flip the UVs a second time.
The owned runtime diffuse copy uses a bounded tone curve to replace the missing
DS3 specular contribution. Source PNGs and alpha are unchanged. Native hard/soft
cast rims are disabled through actor-local material overrides during Blackflame;
DD2 applies these at rendering time. Other actors and reactive effects retain
their normal material settings.

Blackflame uses DS3's original RGBA atlas `s11629`, an 8x8 sheet. Keep its native
alpha: deriving alpha from luminance removes the black core. Ignition starts at
source time 0.566667 s. The persistent aura follows native Radiance buff presence and
turn expiry. The borrowed performer's particle clips are suppressed only for this
character's own skills; native damage, camera, signals and target reactions remain.
Particle placement/rate/blending approximate the DS3 effect, rather than executing
its FXR simulation. Native DS3 duration 45 s is deliberately not a DD turn rule.
The effect uses separate near-black and pale layers, following the native effect's
value ranges. The DD2 particle material is loaded through its actual Addressable
location. Its RGB-derived-alpha keyword is disabled to retain the native dark core.

## Private conversion

The local extraction kit provides `assemble_parts`, FLVER/Havok readers, native
part textures, c0000 skeleton and raw animation binders. On this machine it is
`C:/Users/Piral/ds3-extract`. `prepare_model.py` uses those existing local tools;
it never launches DS3 or writes into its installation.

```powershell
python tools/vilhelm/prepare_model.py --kit C:/Users/Piral/ds3-extract --out <private-output>
python tools/vilhelm/install_model.py --model <private-output> --portrait <portrait.png> --flame <s11629.png> --game <DD2-folder> --backup <private-backup-folder>
```

The installer refuses a running DD2, verifies dependencies, backs up the previous
private character folder and checks installed hashes. It copies only runtime
dependencies, the portrait and `vfx/blackflame.png`. Restart DD2 after a new pack.
The locomotion pack also installs `motion.json`.

## Verification

`-dd3-vilhelm-check` builds an unsaved party through the normal expedition APIs.
It selects native skills and eligible targets, observes calculation/application,
then finishes scenery/lifecycle checks across four regions. Survival healing is
explicit fixture setup; this is not a balance test. Ordinary campaigns and their
input are unaffected. No estate path is selected or persisted.

Combine `-dd3-vilhelm-check -dd3-crawl-motion-check` for sustained forward/reverse
walking and floor alignment. That fixture does not execute combat actions.
Add `-dd3-vilhelm-cast-check` to the ordinary Vilhelm check for a shorter cast-only
review. Five exact rendered ignition frames are written to the private model's
`qa/` folder, with a check for borrowed hard/soft rim keywords after rendering.

Private source/timing evidence, Blender mount comparisons, installed hashes and
native captures are under `local-art/vilhelm-v01`; DD2 skill/API evidence is under
`local-art/vilhelm-native-audit`. Read the latest MODLOG/PARITY entry for what has
actually passed native acceptance. Cloth physics, full DS3 VFX simulation, native
voice/audio and final balance remain separate from this model/animation integration.

October 15 acceptance: five native skills, original motion phases/idle returns,
native buff expiry, base/mastered loadouts, transient ordinary recruitment and
four-region travel/battle-return pass. Exact native frames show the visible blade
flame and removal of the borrowed gold rim. Meshes, nodes and the previous 12 clips
remain byte-identical at the accessor level; only the original locomotion clip
was added. Sustained native forward/reverse/stop checks pass in all four regions
over 3,370 observed frames, requiring the original walk calibration and an updated
imported pose every frame. Reviewed native recordings/captures cover that movement.
The native planted-foot error bound measures the DD2 rigs, not the imported bones.
Death/corpse presentation and saved recruitment/homecoming transactions still need
native acceptance; cloth/audio and balance remain unfinished.
