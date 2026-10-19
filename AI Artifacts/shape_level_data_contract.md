# Shape Mode player-block authoring contract

## Shape level catalog authoring

`ShapeLevelCatalog` stores its progression as a Unity-serialized `List<ShapeLevelCatalogEntry>`. Each entry pairs a non-empty string key with a `ShapeLevelDefinition` reference. The list's current order is the progression order; do not add a separate order field or derive progression from dictionary enumeration.

Entry keys must be non-empty and unique using ordinal, case-sensitive comparison. `OnValidate` refreshes a red validation summary in the catalog Inspector and reports missing or duplicate keys in the Unity Console. The **Validate Catalog** Inspector button runs the same validation on demand. Runtime catalog validation rejects duplicate, missing, or null entries. Lookup and order resolution fail closed when a key is ambiguous.

When migrating an existing dictionary-backed catalog, preserve its prior enumeration order when populating the list. The Inspector list is draggable so authors can change progression order directly.

## Shape world progression and service lifetime

`ShapeLevelWorldCatalog` stores the ordered Shape Mode worlds. Its list order defines world progression, and each entry pairs a permanent, non-empty, unique, ordinal/case-sensitive `WorldKey` with a valid `ShapeLevelCatalog`. Keep world keys stable after progress can be saved. Each level catalog's entry order and stable IDs define progression only within that world.

Persist completion and unlocks per world key. The first world starts unlocked; completing every current level ID in a valid world unlocks the next catalog world. A world's first level is available while that world is unlocked; passing a level unlocks only the next level in that same world, and completed levels remain replayable. Adding a level does not relock a world, but that world becomes incomplete until the new level ID is completed. The selected world is transient navigation/session context and is not save data. Shape Mode level scores are not part of this progression save model.

At runtime, `ShapeLevelWorldCatalogService` is the shared catalog lookup/progression service for both gameplay and selection. Register one instance in the root `QuackUp.Core.ProjectLifetimeScope` through `FitMe.Scene.ShapeLevelWorldCatalogInstaller`, with the `ShapeLevelWorldCatalog` asset assigned there. Child scene scopes resolve the service from the project scope. Do not register another catalog service or keep duplicate catalog asset references on scene installers or `LevelManagerConfig`.

## Authoring model

`ShapeLevelDefinition` stores its ordered player supply as `List<ShapePlayerBlockEntry>`. Each serializable entry has a `BlockPreset` reference and a positive `Count`, so repeated presets can be authored once with a quantity instead of one list element per block. The entry is an inline Unity-serializable class; do not replace it with a tuple, which is not a reliable Inspector-serialized field type.

The list order remains the spawn order. A single block is represented by `Count = 1`; identical presets may be grouped into one entry. The Inspector minimum is a convenience only—validation must still reject a missing entry, missing preset, or `Count < 1`.

## Shape level Inspector design feedback

`ShapeLevelDefinition` exposes read-only design metrics derived from the current board and ordered player-block entries. Playable cells are cells the current board permits blocks to occupy after custom obstacles are removed. Metrics include playable, obstacle, void, and player-block cell counts plus the capacity difference. **Refresh Design Metrics** forces a recomputation; metrics are not serialized or treated as authoritative state.

**Validate Tiling** runs a bounded exact-cover search using the current block list and the game's four rotational orientations. It counts distinct board tilings, treating permutations of identical blocks and equivalent rotations as one tiling. A completed search reports an exact count; budget exhaustion reports “at least N,” and exhaustion before a solution is found remains inconclusive rather than infeasible. The project-wide editor-only settings in `ProjectSettings/FitMeShapeTilingValidationSettings.asset` independently bound search nodes and retained preview layouts; the generation profile's candidate-optimization budget is unchanged. The custom Inspector exposes navigation over retained solution previews and displays the generator's provisional difficulty heuristic with its score-model version and supporting placement metrics. Solution count influences that heuristic; a lower-bound count is explicitly provisional. The heuristic is recomputed from the current playable board mask and expanded block list only after a feasible result; saved generation provenance is not used. Both the heuristic and preview are derived editor state, not serialized into the level asset; any detected definition edit clears them, and rerunning validation computes fresh results. If the budget expires before any tiling is found, the inconclusive result does not show a solved layout or difficulty score; a found tiling remains valid evidence of feasibility even when enumeration is incomplete. Infeasible and invalid results do not show a solved layout or difficulty score. Structural level errors block the search and are shown separately. These settings do not affect runtime gameplay.

## Validation and capacity

For each entry, count the occupied atoms in the referenced preset schema and multiply by `Count`. The total must exactly equal the board's playable cells minus custom obstacle cells:

`sum(preset atom count × entry count) == playable cell count − obstacle cell count`

Reject empty/invalid preset schemas as before. Use a wide accumulator for the multiplied total to avoid intermediate integer overflow.

## Runtime boundary

`ShapeLevelDefinition.ToRuntimeData()` expands each entry into `Count` individual `BlockPreset` references, preserving entry order. `ShapeLevelRuntimeData.PlayerBlocks` and gameplay consumers continue to receive individual blocks; grouped counts are an authoring-only representation. The existing spawn path handles each expanded item separately, so its per-instance randomized color and rotation behavior remains unchanged.

Keep future generators compatible with this boundary: they may group repeated presets when writing level definitions, but solver/runtime data must use individual block instances.

## Shape playset generator void cells

`ShapePlaysetGenerationProfile` configures an inclusive `MinVoidCells`–`MaxVoidCells` range. Both values default to `0`, preserving the existing full-rectangle board for profiles that do not opt into void generation. The generator samples that exact number of unique cells into the existing `BoardMask` as zeroes; it does not introduce a second void mask.

Obstacle sampling and any obstacle-repair relocation are restricted to cells where `BoardMask == 1`. Region detection, repair paths, and exact-cover tiling treat zero-valued board cells as permanently unavailable. Generation provenance records both configured bounds and the resulting void-cell count; changing seeded output behavior advances the generator version.

## Serialized asset changes

Changing the list element type is a serialized-data schema change. Before shipping authored `ShapeLevelDefinition` assets that use the old `List<BlockPreset>` layout, migrate each prior list item into an entry with the same preset and `Count = 1`, then verify the asset in Unity Editor. Do not assume Unity will automatically convert the old references into nested serializable entries.
