# Corridor hero animation

The corridor keeps the owner's DD2 hero meshes, textures, palette and weapons.
`HeroStage` spawns separate presentation actors through the existing native loader.
`CorridorHeroMotion` adds a corridor-only animation layer to those instances.
No replacement mesh or extracted game asset is distributed, and shared animation
controllers are not modified. The walk layer affects only the corridor actors.

## Softer contacts and foot recovery, round262, October 17, 2026

The owner still finds the steps stiff. The sampler's ankle rotation had170–286 degrees/cycle
velocity discontinuities at contact changes. The swing toe remained17 degrees down at40%
recovery, with only0.08-leg lift. Five new red cases reproduce those defects. Contact roll and
toe recovery now use bounded quintic easing; lift rises earlier, peaking at0.12 leg lengths.
Its vertical velocity and acceleration reach zero at liftoff and landing. Horizontal planted
travel, step length, stance fraction and fitted walking pace remain unchanged.

Recorded round261 bones also expose a real hip step:516/606 contact changes jump more than
5 virtual pixels, max32.03. The common-pelvis constraint previously switched on/off at the
contact bit. Four red coupled-geometry cases reproduce a0.0594-leg discontinuity. Body support
now releases over the first25% of swing and prepares contact over its last40%, using ground
anchors. The middle swing remains free and planted feet retain full constraints. A slightly
lower0.925-leg relaxed height keeps knee/hip excursion within the earlier bounds.

Native iteration matters here. Following the moving airborne target spread the impulse but
created larger downward steps; nominal anchors then exposed a retained combat-stance offset.
`CorridorSupportContact` captures each actual liftoff anchor and supports both travel directions.
Two negative-control cases reproduce the nominal-anchor snap. It resets on stop/visibility/
restart. Future landing anchors stay nominal, matching the distance-driven contact tracker.
Normal sources contain no temporary observer or private QA class substitution.

Final unsaved Leper/Shieldbreaker/Flagellant/Jester routes pass forward/reverse/stop/repeated
buttons across all four regions.35-second footage and100ms pose samples inspected. The native
bone comparison records16/576 contact changes above5px, max7.23px; the largest all-frame
height change falls32.03->15.43px. The95th percentile stays3.52px. Continuous planted-foot
slip max0.58px and IK target error0.1953px remain sub-pixel. This reduces the abrupt contacts;
some startup, native idle and class-specific motion still needs broader animation polish.

Release+1,266 Core+274 UI checks pass,69 focused. Current442-source normal build overlays only
three code/test files on round261. Evidence, failed intermediate passes, source manifests,
fresh backup and walk-preview.mp4: `local-art/walk-softness-v17`. QA changes no estate; native
profiles/config/scenery and independent Shieldbreaker work are preserved. Imported Vilhelm's
root-motion animation is separate. Owner's launch/record authorization remains current.

## Native pose and fitted pace, round261, October 17, 2026

The owner reopened DD2, then explicitly authorized launching and recording. This supersedes
the launch hold below. Early live captures caught idle/door stops, so acceptance uses an
isolated unsaved fixture with controlled routes and temporary read-only bone measurements.

Native footage exposed a common-pelvis issue that isolated single-leg checks missed. A long
rear contact and even an airborne foot could force the whole body down. The 62% stance /
1.24 contact arc produced supporting knee bends above90 degrees on the test rigs. Reduce
stance to55%, with Stride=2*Stance retaining one projected leg length per footfall. Only
supporting contacts constrain pelvis height while walking. Swing targets remain free IK
targets; both legs retain the earlier reach constraint while blending back to idle.
Two geometry regressions cover shared support and airborne targets. The shared-support
case first failed at63.8 degrees; releasing earlier alone still failed at55.2, and supporting
contacts only passed. Contact/restart/reverse continuity and full step distance remain tested.

The current fitted native legs measure154–202px, considerably larger than the102.6px older
recorded-size replay. A fixed180px/s ceiling gave only0.89–1.17 steps/s. Four red actual held-
loop cases reproduce that slow cadence. Raise the physical ceiling to320px/s and retain the
party's minimum1.8 projected leg lengths/s limit. The native test party consequently walks
at about277px/s with1.38–1.80 steps/s across the four rigs. Reverse remains half speed and
the100ms stall catch-up cap remains. This changes travel time, not distance per footfall.

Release,1,266 Core and262 UI checks pass, including57 focused locomotion cases. Native
forward/reverse/stop/repeated-button routes pass in all four regions. The first fixture
uses Highwayman, Grave Robber, Plague Doctor and Man-at-Arms; an additional private fixture
uses Leper, Shieldbreaker, Flagellant and Jester as seen in the owner's live party.
The latter changes only the private QA class list. No estate is selected or persisted.
Native videos, bone traces, red/green logs, exact442-source normal snapshot and fresh
rollback are in `local-art/walk-observe-v16`. Final normal sources exclude the observer and
private class fixture. Preserve native profiles, estate2, scenery, config and unrelated WIP.

Observed improvement is narrower than a complete animation audit. Combat idle translations,
every class/scale and loaded-campaign handoffs still need broader acceptance. The imported
Vilhelm root-motion animation remains separate. Earlier launch-hold notes are historical.

## Longer steps, round260, October 17, 2026

The owner still finds distance per step underwhelming. Round259 corrected held movement
speed but retained a 0.90-leg contact arc and 62% stance, which covers only 0.726 projected
leg lengths per footfall. Three crop/gait acceptance failures and five actual held-loop
failures reproduce the short step across native paintings, fallback and fitted rig sizes.

The native DD2 corridor gait now uses a 1.24-leg contact arc at the same 62% stance.
`Rate` consumes that same arc, so the planted foot retains the ground's pixel velocity.
Distance per footfall becomes one projected leg length, 37.8% longer. Ground speed remains
180px/s with the existing small-rig limit and half-speed reverse. At the previously recorded
102.6px projected leg this gives about 103px per step and 1.75 steps/s, versus 74px/2.42.
The imported Vilhelm clip uses its own extracted root-motion calibration and is separate.

The held-loop regression detects alternating landings and measures actual camera travel
between them, rather than asserting only a gait constant. Existing contact/restart/reverse,
swing continuity, body support and reachable soft-knee checks pass. The long-frame contact
fixture now specifies skipped phases, preserving its lift/landing scenario across stride
changes. All 51 focused checks, Release and full 1,266 Core / 256 UI checks pass.
Exact normal source snapshot, red/green logs, metrics and fresh rollback live in
`local-art/walk-stride-v15`. The normal build contains no temporary observer.

Native visual comfort remains unverified. Keep DD2 closed until the owner explicitly says
OK. No game launch, OS input or estate access this round. This section supersedes the
shorter-stride measurements below; the ground clock and scenery registration remain.

## Continuous walking pace, round259, October 17, 2026

The owner reports that steps and scenery are still wrong while walking continuously.
Tests previously exercised distance matching and isolated traversal, but did not constrain
the actual held-walk clock's physical speed. The unchanged integration is now extracted to
`Driver.Walking.cs` so tests run real held movement against Core Crawl and the actual crop.

Nine initial failures reproduce 309px/s native / 600px/s fallback travel, up to 4.15 steps/s
on the 102.6px projected leg derived from the earlier native capture, and 247–480px jumps
after a delayed frame. Red/green evidence is in `local-art/walk-pace-v14`.

Held movement now targets 180 virtual pixels/s, using the rendered square spacing rather
than a fixed 1.2 seconds per logical tile. Short fitted native rigs can lower the party's
speed to 1.8 projected leg lengths/s. Imported clips retain their own distance calibration.
Backing up is half the same physical speed. Only 100ms of movement can be consumed in one
frame; a render stall no longer catches up by moving several strides unseen. The logical
square count, camera crop, artwork, stride arc, poses and explicit passage interaction remain.
Logical walking time per square is therefore longer in larger crops, about 2.06s for the
six-square canonical painting. The separate discrete developer-step easing stays at 1.2s.

Replay through the real held integration gives 180px/s across six/twenty-square paintings
and the legacy fallback, 2.42 steps/s on the recorded short native rig, and an 18px maximum
advance after the tested 800ms stall. A scaled 55px leg gets 99px/s and 2.48 steps/s. Fourteen
new cases also cover backwards travel, frame rate, tile carry and stopping at a passage;
42 focused checks and full Release / 1,266 Core / 251 UI checks pass. The future native
motion fixture now enforces the physical held-walk limit rather than the old tile timer.

These are code/recorded-size replays, not a new native observation. DD2 must stay closed
until the owner explicitly says OK. Native comfort, exact rig projection, asset adoption,
real controls and sustained walking through all four regions remain pending. The final
normal build contains no temporary observer. Existing loading, typography, stress,
passages, scenery and installed heroes are retained; capture a fresh baseline before QA.

## Earlier cadence, round252, October 16, 2026

The gait follows signed rendered ground distance, including the fitted rig's
projected leg length. Its contact arc is now 0.90 leg lengths with 62% stance,
giving about 0.726 projected leg lengths per step. The former 0.46 arc gave only
0.371, causing the reported rapid shuffle. There is no fixed cycle duration.
Hidden actors reset contact tracking; restarting aligns phase with the current
leading foot and blends the lift into the observed pose.

Short halls use a centred portion of the panorama: view span is
`(tileCount - 1) / max(9, tileCount - 1)`. A two-square route therefore no longer
sweeps the full painting in one square. Button steps share the ordinary 1.2-second
walking duration. Repeated button presses wait for that step to finish, and held
movement resumes afterward. Continuous crossings retain their existing carry.

Foot contacts track unwrapped signed phase. A long frame that skips liftoff and
landing starts a new contact rather than dragging the previous planted foot past
the skeleton's reach. Swing offsets respect direction changes and the initially
observed foot. Both contacts still constrain pelvis height before IK solves.

The exact installed round248 source snapshot plus these changes passes Release,
1,176 Core and 219 UI tests; the shared tree also passes 1,216 Core and 219 UI.
Read-only native instrumentation verifies four
regions' forward/reverse/stop/restart and repeated button cases over 3,804 observed
motion frames: maximum 2.770 steps/s, 206.3 virtual pixels/s and 0.195-pixel planted
target error, with no duplicate actor update. Regional captures and sampled frames
of a 55-second native video were inspected. The final build omits the temporary probe.
Private evidence, source/install hashes and rollback: `local-art/gait-cadence-v10`.
No estate was accessed and no OS input was sent. This does not verify every hero,
hall length, saved campaign, interaction or battle handoff. Vilhelm retains his
separate imported root-motion animation; this fixture uses native DD2 heroes.

The initial walk-layer account below is historical; the distance clock and
contact behavior above supersede its fixed-duration and short-stride description.

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

## Initial walk layer, historical

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

Weight shifts toward the supporting leg. The pelvis rises over that foot and
settles during double support; yaw follows the separation of the feet. Smaller
chest counter-rotation lets the shoulders follow the hips, with steadying head
motion. Round196 increases the torso's net yaw/sway by 50%, to 2.25/1.5 degrees,
by reducing shoulder counter-motion. The phase and hip/leg motion stay the same.
Walking raises the native combat crouch toward 93% of leg length, with
only 0.7–2.3% compression, keeping a cautious bend without a low squat.
Leper's Sword_AuxSHJnt is a pelvis child, so it needs explicit grip preservation:
capture its native pose relative to the right wrist before the walk layer, then
carry it with that hand after the torso moves. Restore its local pose before the
next Animator evaluation, like the other driven bones. Both hands are checked.
The native idle still supplies
the remaining pose and secondary motion. Applied bone transforms are restored
before the next Animator evaluation, including unkeyed transforms, so the layer
does not accumulate offsets. The previous whole-slot bouncing is removed.

The initial native review showed excessive foot travel and lift with a stiff
torso. The owner's feedback led to the shorter, lower, slower step and coordinated
upper-body movement above. A completely new model was unnecessary to preserve
the requested DD2 appearance.

## Verification and limits

Eight production-source tests cover grounded constant-speed contacts, swing
clearance, double support, continuous loop joins, reverse cadence, idle blending
and hidden-stage suspension, relaxed knee clearance and support-timed torso rise.
Release, 920 Core tests and 149 UI tests passed in round195.
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

Round195 native evidence is in
`C:\Users\Piral\.universal-modder\inspection\hero-grip-transition-20261010`.
The private probe compares native hand-to-sword transforms before and after each
procedural frame: the old layer failed with 8.78 bone-local units of drift and
8.68 degrees; the revised layer passes for both wrists over 180 frames each,
with at most 0.059 units of drift and no angular drift. Native Leper planted-knee
flexion is 31.4–57.6 degrees across the revised gait. `walk-final.mkv` records the
four-hero walk. Subjective feel and the other classes still need owner review.

The corridor's warm colour correction now follows the actual render environment
during combat handoffs. HeroStage caches the road ambient-probe and global-shadow
colours before a fight. While those differ during combat/return, it uses the
neutral combat tint. This is updated before material blocks' LateUpdate; scene
load alone is too early, and unload precedes the road lighting's return. Native
entry and debug return captures verify both boundaries. `check-handoff.py` reads
neutral hood pixels from the reproduced yellow frame and corrected capture; the
red/blue ratio falls from 1.413 (FAIL) to 1.107 (PASS). No offline colour test
claims to substitute for the native renderer.
