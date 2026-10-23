# Remaining service review, round326

Status after327: the Graveyard post/cap material study below is accepted. Its
original2048 common_gravestones paint is fitted with an intact cap field and
local linear-light compensation. Existing Hamlet headstones retain their
separate field_hospital_node native painting. Installed art now comes from
hamlet-graveyard-stone-v84. The observations below retain their325 baseline.


This is an offline diagnosis, not an installed art change. Accepted325 models,
field and runtime298 stay installed. Source is v82 art/review-study, matched
against the installed package and raw verified library. Eight unlit actual
Core-band views cover Stagecoach, Nomad Wagon, Graveyard and Survivalist.

The Stagecoach has coherent native paint, closed body, native joinery, wheels
and lamps. The Survivalist's native Tangle tent has readable cloth damage,
seams and foliage. Neither needs another broad texture replacement based on
these views. The Nomad has a distinct muted plum canopy, worn oak and native
merchant sign. Its sign is the accepted427x464 DD2 Nomad inn crest on a rigid
panel, with full pixels/alpha/aspect, not the removed DD1 eye sprite. Retain59.

The Graveyard's four posts and four pointed caps are the clearest next material
candidate. The broad, quiet stone faces contrast with the native headstones'
painted cracks and chipped edges. The eight members use Service native individual
cut stone v24.001, with unchanged512x512 foundation_panel_06 pixels. All200 UV
triangles have finite area: four44-triangle posts and four6-triangle caps. There
is no zero-UV repair implied. Each member samples one of four native stone crops:
64x35,84x44,53x43 or61x27 source pixels before rounding. These quiet fields suit
individual retaining blocks but give a whole tall post little directional wear
or edge definition. Pixel count alone does not establish the visual gap.

The original2048 common_gravestones atlas already used by native headstones has
more compatible chipped grey stone faces and vertical weathering. It is the
first material study candidate. Original Altar of Hope2048 is a warmer secondary
reference. No new extraction or generation is justified before fitting and
comparing these existing fields on the actual retained posts/caps. Preserve
their bounds, contact with the apron, headstones, stairs/retaining wall and
other services. Keep the muted service family; don't paste a complete inscribed
headstone image onto a post. Inspect four model views, then the native town if a
useful candidate survives. Detailed texture must work at the service's scale.

Native inventory also found common_cathedral_prefab exports zero parts/materials.
Do not treat that empty record as usable extraction evidence. city_node_gate and
coast_node_gate have complete atlases but no reviewed replacement field yet.
Diagnostic atlas sheets are source comparisons, not approved asset crops.

Evidence: art/services-review.jpg and eight original800px PNGs;
art/graveyard-post-mapping-audit.json; art/graveyard-stone-source-review.jpg;
art/graveyard-source-extents.json; art/native-stone-candidates.jpg;
art/native-candidate-inventory.json. GLB image coordinates are top-left; the
source-sheet helper accounts for the exporter's V flip. Initial diagnostic
opposite-V crop was corrected before interpreting source fields. No asset paint
or model was edited by these diagnostic crops.

No game launch, deployment, save access, new gameplay/Core test or art acceptance
occurred. Native acceptance remains325, including255 renderers/zero reallocations.
All new images live on D:. Newest art source remains v82, not this review folder.
