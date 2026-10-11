# Native 3D Hamlet feasibility

Investigated 2026-10-11 after the owner proposed using DD2 road encounters as a perspective Hamlet.
This initial investigation established feasibility while another agent owned the painted Hamlet overhaul.
The owner subsequently authorized a reversible runtime implementation and DD1 building-texture details.
See `tools/hamlet_3d.md` for the feature flag, implementation, native checks and remaining art scope.

## Verified locally

The private DD2 native kit contains mesh geometry, transforms and materials for these installed assets:

| Native asset | Verified parts | Possible Hamlet use |
|---|---|---|
| `common_hoarder_prefab` | Merchant, cart, banners, ground and stock piles | Nomad Wagon or provisioner |
| `common_field_hospital_prefab` | Nurse, hospital, hill, graves, planks, rocks and torches | Sanitarium |
| `inn_lvl_01_prefab`, `inn_lvl_02_prefab`, `inn_lvl_03_prefab` | Inn buildings, signs, bridge and flags across variants | Tavern and possible upgrade states |

Native prefab addresses include
`Assets/Data/Biome/Common/Map/NodeTiles/common_hoarder_prefab.prefab` and
`Assets/Data/Biome/Common/Map/NodeTiles/common_field_hospital_prefab.prefab`.
The kit also contains regional buildings and props for filling a shared town. Exact source paths and bundles
are in `C:\Users\Piral\DarkestDungeon3\local-art\dd2-native-kit\catalog.json`.
The Hoarder and Hospital three-quarter reference renders were inspected under that kit's `sprites` folder.
Those images are offline references; they do not establish runtime lighting or animation acceptance.

`HamletUi.DrawTown` currently draws flat town art. `HamletUi.Open(string building)` already dispatches
building IDs to existing town panels and handles locked buildings. A new 3D town renderer can call the same
service dispatch, retaining DD1 treatment, stock, upgrades, costs and campaign state.

Installed `TriggerHospitalBhv.Execute` and `TriggerStoreBhv.Execute` assume road-node state, including a parent
`TileNodeBhv`, the driving UI, entry screens and native inventories. Reusing their scenery does not require
running those interaction scripts. A prototype should isolate visual objects and use Hamlet-owned hit targets.
The exact component exclusions must be checked before activating a clone.

## Recommended first proof

Build an isolated, unsaved stage with the Hoarder and Hospital on one shared ground surface. Use an elevated
three-quarter perspective camera, restrained pan/zoom, hover labels and a short camera move toward the selected
service. Connect each target to the existing Nomad Wagon/Sanitarium panel, then return to the overview on close.
This would establish actual depth and perspective before building the full town.

Load original installed prefabs through Addressables rather than shipping exported game models. The existing
`HeroStage` demonstrates a dedicated camera/render-texture approach in this mod, but its character-layer and
lighting setup must not be assumed suitable for scenery. Check the native renderer, materials, fog, lighting,
VFX, valid viewing angles, async preparation, repeated entry/exit cleanup and input blocking while panels open.
The private kit's unlit/bind-pose exports do not prove that an NPC has reusable idle animation. Missing building
shapes may need composed native parts or custom models. Incomplete offline bundle exports do not prove that the
installed runtime prefab is incomplete.

## Sources and limits

- Installed DD2 decomp, private: `Assets.Code.Map.Triggers/TriggerHospitalBhv.cs` and `TriggerStoreBhv.cs`.
- Mod implementation: `src/DarkestDungeon3/Ui/HamletUi.cs` and `src/DarkestDungeon3/Dd2/HeroStage.cs`.
- Private kit `README.md`, `catalog.json` and the listed assets' `exp/<name>/scene.json`.
- [Unity Addressables runtime instantiation](https://docs.unity3d.com/Packages/com.unity.addressables@1.21/api/UnityEngine.AddressableAssets.Addressables.InstantiateAsync.html).
- [Unity 2022.3 cameras](https://docs.unity3d.com/2022.3/Documentation/Manual/CamerasOverview.html).

Local searches used `hoarder`, `hospital`, `nurse`, `merchant`, `shopkeeper`, `Prefab`, `Instantiate` and
`DrawTown`. Web query checked official Unity Addressables instantiation and camera documentation. Engine API
support and local asset geometry are verified; the proposed shared Hamlet stage remains untested. No game
process, save file, runtime code or private art was changed.
