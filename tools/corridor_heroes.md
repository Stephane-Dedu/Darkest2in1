# Corridor hero animation

The corridor keeps the owner's DD2 hero meshes, textures, palette and weapons.
`HeroStage` spawns separate presentation actors through the existing native loader.
`CorridorHeroMotion` adds a corridor-only animation layer to those instances.
No replacement mesh or extracted game asset is distributed, and shared animation
controllers are not modified. The walk layer affects only the corridor actors.

## Combat palette

The prefab's Hero shader has a blue `_ShadowColour`; changing scene lights does
not remove it. `HeroCombatPalette` loads the installed Foetor combat material
preset, matching the owner's warm battle reference, and applies native material
property overrides to the corridor actors and heroes in fights in place. It finds
the preset by catalog InternalId because DD2 exposes GUID load keys. The location
is cached; load handles and actor overrides are released when their presentation
ends. Shared materials, textures, normal DD2 battles and enemy palettes stay native.

The owner subsequently still saw blue walking heroes. Native captures confirmed
that the warm shadow preset alone did not neutralize the final corridor image.
The corridor now additionally uses material tint0.96/0.90/0.74. Fight actors keep
the arena tint because they already have the native battle lighting. Both retain
brightness1.25, tint intensity1.0 and saturation1.10. Native neutral-hood pixels
changed from RGB75.29/78.04/84.48 to81.65/75.16/67.97; walking and battle captures
were inspected. The exact subjective match still needs the owner's review.

The same battle meshes, textures, native combat stance, gait and camera-facing
angle remain in use. Rebuilding or re-rigging those models was unnecessary.
The unsuccessful global-lighting experiments remain outside the product. Existing
model brightness configuration remains available.

## Facing and rig

The stage camera looks from negative Z. After native art loads and the body is
fitted, the motion layer derives anatomical axes from the hips and foot bind
matrices. The slot turns to a right-facing three-quarter view with the front
toward the camera. This corrects the away-facing road/inn pose too.

The installed base-game heroes and Crusader, Duelist and human Abomination all
have the required `ROOTSHJnt`, left/right `Leg_HipSHJnt`, `Leg_KneeSHJnt` and
`Leg_AnkleSHJnt` suffixes. Prefixes vary between classes. Bind matrices determine
neutral hip spacing and flat ankle orientation, rather than assuming a Humanoid
Avatar or a common bone rotation axis. Native artwork remains loaded by DD2.

## Walk layer

`Driver` passes actual signed hallway velocity, normalized to 1 forward and -0.5
backwards. A route pending in a room does not animate a walk. The motion layer
uses unscaled time and a 0.14-second response for starting, stopping and changing
direction. Each rank has a different phase. Hidden stages do not advance it.

The 1.28-second cycle has a 62% contact phase, a stride of 0.46 leg lengths and
foot lift of 0.08 leg lengths. Hermite swing tangents match contact velocity at
both boundaries. Left and right phases differ by half a cycle; at least one foot
always contacts the ground. Analytical two-bone IK bends the knees toward the
anatomical forward direction. These are stylized in-place steps; their contact
speed is not claimed to match the fast 720-pixel-per-square scenery exactly.

Weight shifts toward the supporting leg, with pelvis compression/rotation,
counter-moving chest and steadying head motion. Arms and held weapons follow
the torso as a unit to preserve two-handed grips. The native idle still supplies
the remaining pose and secondary motion. Applied bone transforms are restored
before the next Animator evaluation, including unkeyed transforms, so the layer
does not accumulate offsets. The previous whole-slot bouncing is removed.

The initial native review showed excessive foot travel and lift with a stiff
torso. The owner's feedback led to the shorter, lower, slower step and coordinated
upper-body movement above. A completely new model was unnecessary to preserve
the requested DD2 appearance.

## Verification and limits

Six production-source tests cover grounded constant-speed contacts, swing
clearance, double support, continuous loop joins, reverse cadence, idle blending
and hidden-stage suspension. Release, 917 Core tests and 131 UI tests passed.
Native captures in estate 2 cover Vestal, Flagellant, Leper and Jester, forward
walking, slower backwards movement, stopping and loading a saved corridor.
No stage animation exceptions were found. The other classes have been checked
for compatible bones in their installed bundles but still need individual visual
reviews, especially garments and alternate art modes.

Private evidence is in
`C:\Users\Piral\.universal-modder\inspection\corridor-walk-20261010`.
`corridor-walk.mp4` is the revised native capture. The two original estate 2 files
are backed up there and restored after testing. Protected estates were not read
or changed by the test workflow. No generated or extracted assets are committed.
