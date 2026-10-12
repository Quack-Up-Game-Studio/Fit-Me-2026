# Shape Level Generation: Editor and Runtime Integration Plan

**Status:** Implementation in progress. The user authorized implementation in chat on 2026-10-12; each Git add/commit/push still requires review and explicit approval for its exact scope.
**Worktree:** `.worktrees/shape-playset-generator`
**Branch / baseline:** `feature/shape-playset-generator`; implementation continues from the previously approved generator/data/runtime checkpoints.
**Related contract:** [`shape_level_data_contract.md`](shape_level_data_contract.md)

## Implementation status — 2026-10-12

- [x] Deterministic generator and existing manual/preset compatibility retained.
- [x] Odin-backed `ShapeLevelDefinition`, mixed ordered player-block entries, validation, and normalized Shape Mode runtime consumers implemented in prior approved checkpoints.
- [x] Level/catalog ScriptableObject script mapping and asset save/reload behavior verified.
- [x] Separate generation-profile asset implemented and save/reload-tested (`fc4c7e21`).
- [x] Generated-result conversion and serialized origin-only provenance implemented; generated masks, mixed block order, preset references, inline footprints, config snapshot, and provisional metrics survive asset save/reload (`e1bf936c`).
- [ ] Editor-only asset creator and generator window are implemented in the worktree; asset creation is EditMode-tested, and the window compiles in the Editor assembly. The last full Grid run before the new Inspector contract probes passed 119/119. The latest expanded run passed 121/122: `InlineFootprintInspectorUsesOdinMatrixAndPreservingResizeAction` exposed a resize-preservation defect that remains to be fixed.
- [ ] Final broader EditMode/PlayMode verification and final review remain.
- [ ] Manual Editor and gameplay smoke checks remain: Editor GUI capture was unavailable in this environment (`computer_use` reported no `DISPLAY`), so do not treat the visual/interactive checks below as passed.

## Goal

Let designers generate a Shape Mode playset in the Unity Editor, inspect it, save it as an editable `ShapeLevelDefinition`, and play it through the existing Shape Mode flow. Preserve `GridPreset` and `BlockPreset` for authored content and existing non-Shape systems. Generated levels should not create new preset assets by default.

## Approved design decisions

- Keep `GridPreset`; do not deprecate it as part of this work. It remains the Manual board source and continues to serve Classic/endless/tutorial and other existing consumers.
- `ShapeLevelDefinition` exposes an explicit board-source dropdown with **Manual** and **Generated** choices. Manual selects a `GridPreset`; Generated uses inline board/obstacle data. Switching modes hides the inactive data but does not erase it.
- Generated board data is editable in the level asset after generation. Generator provenance is separate from the current edit mode and must not imply the edited level still matches its original generated solution.
- Use Odin's `SerializedScriptableObject` for `ShapeLevelDefinition` and Odin matrix drawing/serialization for inline grid, obstacle, and footprint data. Inline footprints should use the same cell-toggle `TableMatrix` editing experience as `BlockPreset.BlockSchema`, including width/height controls and a resize/refresh action. Do not add UnityEditor references to runtime assemblies.
- Keep one ordered player-block entry collection. Each entry can reference a handcrafted `BlockPreset` or hold an inline footprint. Preserve entry order as runtime spawn order and retain the existing preset/count authoring behavior.
- Normalize both Manual and Generated board sources, and both preset-backed and inline-footprint blocks, into one Shape Mode runtime contract.
- The Editor is the generation entry point for this scope. The game loads saved level data; it does not procedurally generate a level at runtime.

## Proposed data model

### ShapeLevelDefinition

- Change the asset base class from `ScriptableObject` to Odin `SerializedScriptableObject`.
- Add a serialized board-source enum whose zero/default value means **Manual** so older assets with no serialized enum value continue to use their existing `_board` reference.
- Keep the existing `GridPreset` reference and existing serialized field names wherever possible.
- Add inline board data for Generated mode: playable-cell mask, obstacle-cell data, dimensions, and any obstacle appearance fields required by current Shape Mode runtime behavior. Use the generator's board/obstacle masks as the initial content; current generated boards are rectangular, but the representation should preserve a playable mask for future custom shapes.
- Provide a shared Odin matrix-editing control for the inline board/obstacle matrices. Reuse the existing visual behavior where practical rather than cloning all unrelated `GridPreset` inspector features.
- Preserve both board representations when the source dropdown changes; only the selected representation is active for validation/runtime conversion.

### Ordered player-block entries

- Extend the existing serializable `ShapePlayerBlockEntry` rather than replacing the ordered collection.
- Keep `_preset` and `_count` intact for old/manual entries; make the default source kind Preset so older serialized entries retain their meaning.
- Add a source kind (`Preset` or `InlineFootprint`) and an Odin-serialized editable footprint for InlineFootprint entries. Store dimensions and a binary matrix; occupied cells must be non-empty and 4-connected.
- Reuse the existing `BlockSchema` `TableMatrix` renderer/cell-toggle behavior if nested Odin serialization round-trips correctly. Otherwise, extract a shared footprint matrix drawer used by both `BlockPreset` and inline entries. Give inline entries BlockPreset-like width/height fields and a Refresh/Resize action that preserves cells where possible; do not copy BlockPreset-only identity/audio/preset asset behavior.
- Preset-backed entries retain their `BlockPreset` reference and count. Inline footprints carry their own count. Generated output may initially write count-one entries in solver order to preserve the exact sequence; grouping can be added only when it does not change ordering.
- The generator's solved placement and placement rotation are a solution witness/provenance, not a required gameplay instruction. Keep them in generation metadata if needed for review/reproduction; gameplay blocks remain freely placeable/rotatable per the existing Shape Mode rules.

### Generation provenance

Store an optional record on the level containing the seed, attempt index, generator version, effective config snapshot, and provisional metrics. Do not rely on a profile asset alone as a reproduction record: profiles can be edited later. Clearly label difficulty metrics provisional. If the inline board or blocks are edited after generation, mark the provenance as originating data rather than claiming the current content is unchanged; a content fingerprint/stale indicator is a candidate implementation detail.

## Editor workflow

Implement an Editor-only window in an Editor-only assembly that references the runtime API. Keep the profile asset separate from the generated level asset.

1. Select or create a `ShapePlaysetGenerationProfile` containing board/obstacle ranges, fallback dimensions, region threshold, budgets, singleton cap, and preferred preset/count entries.
2. Enter a seed; offer Generate and deterministic seed-step controls.
3. Validate the profile and call the existing `ShapePlaysetGenerator` API. Display validation errors, candidate failure reasons/counts, attempts, and unproven feasibility distinctly.
4. Preview the board, obstacles, solved block placements, block footprints, and preset-vs-inline block sources. Show provisional metrics as estimates, not playtest results.
5. Allow repeat generation with the same seed for reproducibility.
6. **Save as New Shape Level** creates a new `ShapeLevelDefinition` in a user-selected folder, sets Generated board source, copies masks/inline footprints/provenance, and reuses preferred `BlockPreset` references. Do not generate `GridPreset` or `BlockPreset` assets for fallback shapes, overwrite existing levels, or mutate source profiles.
7. Open the saved level in its normal Inspector so designers can continue editing the inline matrices and ordered block entries.

The Editor window previews candidates but does not change scenes or gameplay state. Asset creation occurs only after an explicit save action.

## Runtime normalization and call-site work

Create a single runtime representation in the existing Grid-side runtime boundary, without making the Grid assembly depend on GameData:

- A normalized board record exposes dimensions, playable cells, and obstacle runtime data independent of whether the source was a `GridPreset` or inline level data.
- A normalized player-block record exposes the occupied schema plus an optional source `BlockPreset` for preset-specific metadata. Expand counts into individual runtime records in authored order.
- `ShapeLevelDefinition.ToRuntimeData()` validates and converts the active board source and ordered player-block entries into these runtime records.
- Update `ShapeLevelDataValidator` for both board sources and both block entry kinds, including schema validity, positive counts, 4-way block connectivity, obstacle-on-void rejection, and exact player-cell capacity.
- Update Shape Mode request/dispatch, grid setup/size/mask access, obstacle handling, and block queue/spawning to consume normalized data. Inspect these concrete current seams while implementing: `LevelSessionRequest`, `LevelManager`, `GridManager`, `GridManagerMessageHub`/`SpawnWithShapeLevelEvent`, `ObstacleHandler`, `ShapeLevelRuntimeData`, and `BlockManager`.
- Preserve the existing GridPreset path and behavior for non-Shape modes. Avoid temporary generated `GridPreset` objects for Generated Shape Mode unless implementation proves a narrow adapter is necessary; any such adapter must remain transient and must not be saved as a reusable preset asset.
- Verify `BlockFactory` and preset-specific behavior when a custom footprint has no `BlockPreset`. `BlockManager.InstantiateBlock` already accepts a schema override and optional preset override, but the complete no-preset path still needs an implementation-time audit.

## Implementation phases

### 0. Read-only preflight and compatibility inventory

- Reconfirm the worktree/branch and generator baseline.
- Inventory existing `ShapeLevelDefinition` assets and serialized field layouts before changing the Odin base class or entry serialization.
- Trace active Shape Mode and non-Shape GridPreset call paths, including board dimensions, masks, obstacle appearance, spawn order, and block metadata.
- Confirm the Editor/runtime asmdef boundaries and Odin serialization behavior for matrices nested in serializable entries.

### 1. Contract tests first

Add focused failing tests for:

- Old/manual ShapeLevelDefinition data remaining Manual and loading its existing GridPreset/preset entries.
- Board-source switching selecting the right representation while retaining inactive data.
- Inline board/obstacle matrix validation, dimensions/index order, and obstacle-on-void rejection.
- Ordered mixed preset and inline-footprint entries, including count expansion and capacity validation.
- Inline footprint binary/non-empty/4-way-connectivity rules and accepted internal empty cells where connected.
- Odin asset save/reload round-tripping board, obstacle, footprint, and provenance data.

### 2. Serialized authoring model and validator

- Implement the Odin-backed level asset and inline board/footprint records while preserving existing fields and defaults.
- Extend validation and add conversion tests. Keep generator mechanics unchanged.

### 3. Normalized runtime boundary

- Add normalized board/block runtime records and adapt `ToRuntimeData()`.
- Update Shape Mode consumers only; retain GridPreset behavior for Classic/endless/tutorial/manual paths.
- Add EditMode tests for manual and generated conversion, including optional preset metadata and exact ordering.

### 4. Editor profile, preview, and save

- Add a reusable generation-profile asset and Editor-only window.
- Preview candidates and diagnostics without persistent side effects.
- Add a save operation that creates a new editable ShapeLevelDefinition with no generated preset assets.
- Test the save/serialization round trip and profile immutability.

### 5. End-to-end verification

- Run focused Android-target EditMode tests, then the owning `FitMe.Grid.Tests` suite; inspect fresh XML and logs.
- Verify the Editor-only assembly and EditorWindow compile without leaking UnityEditor references into player/runtime assemblies.
- In Unity Editor, generate a candidate, save it, reopen it, edit its board/obstacles/inline footprint, and confirm the edits persist.
- Run a Play Mode smoke test loading both a legacy Manual level and a saved Generated level. Confirm grid sizing, obstacles, block spawning, rotation, placement, retry/continue/session paths, and no regression in non-Shape modes.
- Review exact diff, `git diff --check`, serialized asset diffs/metas, worktree status, and the final test reports before asking for commit approval.

## Acceptance criteria

- Existing Manual ShapeLevelDefinition assets load and behave as before, with no silent data loss.
- Manual mode uses its assigned GridPreset; Generated mode uses inline board and obstacle data. Hidden data is preserved when switching modes.
- Generated fallback shapes are stored inline in the level; no generated GridPreset/BlockPreset assets are required. Preferred handcrafted presets remain references.
- The Editor can preview deterministic results and explicitly save a new, editable level with reproducible provenance.
- Both board sources and both block sources normalize to the same runtime contract; Shape Mode does not branch on authoring representation after normalization.
- Classic/endless/tutorial and other current GridPreset consumers remain unchanged.
- Difficulty metrics remain explicitly provisional; no human-difficulty claim is made without playtesting.

## Risks and mitigations

- **Odin/base-class and existing-asset compatibility:** test real assets and save/reload before assuming a base-class change is transparent; retain serialized field names and Manual defaults; provide an explicit migration if evidence requires one.
- **Nested footprint serialization and Inspector parity:** verify an inline `BlockSchema`/matrix nested in a player entry draws, edits, resizes, and survives save/reload. Keep the matrix UI shared so the two editors do not drift.
- **Runtime assumptions about BlockPreset:** trace factory/model metadata access and define safe defaults for inline footprints with no preset before changing the queue.
- **Grid coordinate orientation:** consistently use `[row, column]` / `[height, width]` in stored matrices and test against `GridSize` (`columns, rows`).
- **Obstacle display data:** the generator outputs an obstacle mask, not all obstacle appearance settings. Define current Shape Mode defaults for generated obstacles; do not invent new gameplay semantics in this slice.
- **Stale provenance after manual edits:** retain origin metadata but distinguish it from a current solver guarantee; consider a fingerprint/stale indicator.
- **Scope creep into GridPreset retirement:** global deprecation/migration is explicitly excluded; revisit only as a separate project after all consumers and authored assets are inventoried.

## Non-goals

- Deprecating/removing `GridPreset` or migrating Classic/endless/tutorial authoring.
- Creating runtime procedural generation or generating levels during a game session.
- Automatically creating reusable `GridPreset`/`BlockPreset` assets for Generated levels.
- Reworking level selection/progression UI, migrating authored content wholesale, or validating human difficulty through playtesting.
