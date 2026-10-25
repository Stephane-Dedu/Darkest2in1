# Curios must belong to their surroundings

Owner direction, 2026-10-25: region and setting take priority over copying the
DD1 object's literal silhouette. A freestanding display cabinet in an outdoor
DD2 area breaks immersion even when its painting is detailed. This supersedes
the earlier blanket preference for faithful DD1 silhouettes.

Keep the existing curio ID, encounter placement, outcomes, RNG, supplies, used
flag and save data. Adapt its visible object and presentation name to the
region. Keep item affordances readable: a key opens a lock, a shovel can force
a container, herbs treat contamination. Do not change encounter probabilities
or remove curios to hide an art problem. DD1 destinations and Faded Memory keep
their intended classic presentation.

| Region | Suitable context and materials | Locked container direction |
|---|---|---|
| Sprawl | Burned streets, fallen shop fittings, watch stores, soot, scorched oak and iron | Weathered street cache or damaged shop strongbox |
| Tangle | Abandoned military stores, encampments, overgrown field equipment, damp oak and canvas | Locked field stores or military supply coffer |
| Foetor | Farm stores, butcher equipment, grain, spoiled provisions, stained timber and dull iron | Locked farm/tool coffer |
| Shroud | Wreckage, docks, fishing equipment, salt, wet timber, ropes and barnacles | Sealed dock cargo or ship's strongbox |

Neither a room tile nor a battle implies an indoor location. Use the actual
scenery/context when available. A coffer can sit indoors or outdoors; a tall
glass cabinet, confessional or standing bookshelf needs a convincing structure
or a different outdoor interpretation.

First repair the cabinet with an existing weathered closed/used coffer and
region-specific naming, preserving its underlying interaction. Then replace
that shared interim painting with distinct native regional fittings. Do not
claim four new paintings when they share one existing body.

Audit priorities: cabinet, bookshelf, confessional, display armor and other
interior furniture. Then check fountains, torture devices and loose books for
regional placement/material cues. Ordinary sacks, crates, remains, stumps and
field equipment can work outdoors when their condition and ground contact fit.
The Old Tree, Moonshine Barrel, Knife Rack and native Dinner Cart draft batch
from round347 is offline reviewed, but native QA ended early. It remains
unaccepted and uninstalled; it is not a reason to continue literal replicas.

Acceptance must include actual region selection, both used states, matching
presentation names, correct masks/grounding and stable supplies/outcomes. A
registry entry, offline sheet or correct isolated texture is insufficient.

Round348 installs only the interim cabinet binding and the regional lookup
support. Schema 3 selects art and names by canonical curio ID plus region;
schema 2 and classic defaults remain supported. The four DD2 labels are Locked
street cache, Locked field stores, Locked farm coffer and Sealed dock cargo.
All four use the previously accepted locked-strongbox painting. Isolated
software tests and offline composites pass; in-game acceptance is pending.
The owner currently requires DD2 remain closed until explicit newer permission.

## Context audit, round349

Check the installed pack before preparing new art. The installed bookshelf
already uses the complete native ruined Sprawl bookcase, with its searched
state from round342. The proposed archive remake duplicated it and was parked.
Native source studies and DD1 reference sheets are not the installed registry.

| Curio | Current finding | Next action |
|---|---|---|
| Cabinet | Outdoor coffer interim installed348, with four region names | Distinct regional fittings; check original result text for indoor wording |
| Bookshelf | Installed native splintered Sprawl frame already fits street debris | Keep pair and scale; contextual name can change separately |
| Confession booth | Intact double-bay timber furniture and curtain still read as an indoor booth | Develop a small wayside prayer/votive place; preserve holy-water and reflection affordances |
| Suit of armor | Armored sentinel on a weathered plinth can plausibly stand beside ruined city gates | Review guard-post surroundings and native armor paint before replacing the body |
| Alchemy table | Laboratory identity needs an outdoor explanation | Adapt field-chemist supplies; keep herb/torch interactions readable |
| Loose books and scrolls | Existing native piles suit fallen Sprawl archives; other regions need a reason for records | Field journals or spoiled farm ledgers where appropriate; no blanket bookshelf swap |
| Iron maiden and torture devices | Plausible in occupied Sprawl streets, weaker in ordinary countryside | Preserve distinct dangerous affordances and review actual backdrop |
| Fountains and tombs | Outdoor civic or graveyard masonry can fit | Check backdrop, scale and materials before declaring them indoor-only |
| Dinner cart | The parked347 whole native butcher cart fits Foetor well | Keep its provenance and wait for native QA; do not regenerate it |
| DLC and legacy designs | Design scope does not establish a live encounter location | Keep inactive designs separate; do not invent spawns |

The native Altar of Hope prayer study keeps its geometry, UVs and paint. Its
floating emblem and broad offering bowl are not yet a convincing finished
confessional substitute. It needs a searched-state design and comparison with
other prayer/roadside assets. No installation349 and no game launch. Contextual
result text also needs review: a matching heading alone cannot fix a result
that still describes a cabinet or booth.
