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

Idle/walk use `a002_000000`/`004000`; front impact uses `a000_005002`;
death/corpse clips are `a000_017002`/`018002`. The original armor meshes, texture
UVs, weights and c0000 animation rig are retained. The Onyx Blade equipment mount
converts its native negative-Y blade axis to the attachment's positive-Z axis,
around the `R_Weapon` grip. No hand or animation track is rotated to compensate.

Native controller phases and actor Timeline clips time the source motions. Antic,
action and recovery use contiguous source segments, including native action clips
without an `_action` suffix and recovery names which already contain `action`.
Crossfades join stance, walking, attacks and impacts. The character shader receives
only active native material controllers. An albedo-derived shading map adapts the
native diffuse texture to DD2's painted hero shader; original color remains separate.
The runtime pack now uses native diffuse PNGs, rather than the extraction kit's
specular-to-metallic PBR color bake. `GltfModel` already converts the image origin;
the renderer must not flip the UVs a second time.

Blackflame uses DS3's original RGBA atlas `s11629`, an 8x8 sheet. Keep its native
alpha: deriving alpha from luminance removes the black core. Ignition starts at
source time0.566667s. The persistent aura follows native Radiance buff presence and
turn expiry. The borrowed performer's particle clips are suppressed only for this
character's own skills; native damage, camera, signals and target reactions remain.
Particle placement/rate/blending approximate the DS3 effect, rather than executing
its FXR simulation. Native DS3 duration45s is deliberately not a DD turn rule.

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

## Verification

`-dd3-vilhelm-check` builds an unsaved party through the normal expedition APIs.
It selects native skills and eligible targets, observes calculation/application,
then finishes scenery/lifecycle checks across four regions. Survival healing is
explicit fixture setup; this is not a balance test. Ordinary campaigns and their
input are unaffected. No estate path is selected or persisted.

Combine `-dd3-vilhelm-check -dd3-crawl-motion-check` for sustained forward/reverse
walking and floor alignment. That fixture does not execute combat actions.

Private source/timing evidence, Blender mount comparisons, installed hashes and
native captures are under `local-art/vilhelm-v01`; DD2 skill/API evidence is under
`local-art/vilhelm-native-audit`. Read the latest MODLOG/PARITY entry for what has
actually passed native acceptance. Cloth physics, full DS3 VFX simulation, native
voice/audio and final balance remain separate from this model/animation integration.

October14 acceptance: five native skills, original motion phases/idle returns,
native buff expiry, base/mastered loadouts and transient ordinary recruitment pass.
Emitted particles were observed, but visible flame quality has not passed review.
The final diffuse-map and independent-alpha changes are built offline and await
native visual review. A temporary extra UV flip was rejected and removed.
No game launches before2026-10-14 19:38:14 UTC /21:38:14 Europe/Paris, per owner.
