# Shape Mode player-block authoring contract

## Authoring model

`ShapeLevelDefinition` stores its ordered player supply as `List<ShapePlayerBlockEntry>`. Each serializable entry has a `BlockPreset` reference and a positive `Count`, so repeated presets can be authored once with a quantity instead of one list element per block. The entry is an inline Unity-serializable class; do not replace it with a tuple, which is not a reliable Inspector-serialized field type.

The list order remains the spawn order. A single block is represented by `Count = 1`; identical presets may be grouped into one entry. The Inspector minimum is a convenience only—validation must still reject a missing entry, missing preset, or `Count < 1`.

## Validation and capacity

For each entry, count the occupied atoms in the referenced preset schema and multiply by `Count`. The total must exactly equal the board's playable cells minus custom obstacle cells:

`sum(preset atom count × entry count) == playable cell count − obstacle cell count`

Reject empty/invalid preset schemas as before. Use a wide accumulator for the multiplied total to avoid intermediate integer overflow.

## Runtime boundary

`ShapeLevelDefinition.ToRuntimeData()` expands each entry into `Count` individual `BlockPreset` references, preserving entry order. `ShapeLevelRuntimeData.PlayerBlocks` and gameplay consumers continue to receive individual blocks; grouped counts are an authoring-only representation. The existing spawn path handles each expanded item separately, so its per-instance randomized color and rotation behavior remains unchanged.

Keep future generators compatible with this boundary: they may group repeated presets when writing level definitions, but solver/runtime data must use individual block instances.

## Serialized asset changes

Changing the list element type is a serialized-data schema change. Before shipping authored `ShapeLevelDefinition` assets that use the old `List<BlockPreset>` layout, migrate each prior list item into an entry with the same preset and `Count = 1`, then verify the asset in Unity Editor. Do not assume Unity will automatically convert the old references into nested serializable entries.
