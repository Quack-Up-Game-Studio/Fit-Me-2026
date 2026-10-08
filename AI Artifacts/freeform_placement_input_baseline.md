# Placement and Input Baseline

## Coordinate and custom-mask contract

- `GridPreset.customGrid` is stored as `[row, column]` with dimensions `[GridSize.y, GridSize.x]` (`Assets/QuackUp/Scripts/Grid/GridManager/GridPreset.cs`).
- `GridManager.CreateCells()` iterates row then column, skips mask values of zero, stores the array index as `(row, column)`, and derives Unity grid coordinates through `GridUtils.ArrayToGridIndex` (`Assets/QuackUp/Scripts/Grid/GridManager/GridManager.cs`).
- `GridUtils.ArrayToGridIndex((row, column), offset) = (column + offset.x, offset.y - row)`; `GridToArrayIndex` is its inverse (`Assets/QuackUp/Scripts/Grid/GridManager/GridUtils.cs`).
- Direct conversion and round-trip coverage is in `Assets/QuackUp/Tests/Grid/GridCoordinateContractTests.cs`. Its zero/one mask assertion documents mask meaning; actual scene cell omission remains covered by `GridManager.CreateCells()` implementation and should be exercised by an integration fixture if a safe scene-free seam is added.

## Existing pickup/drag configuration (read-only baseline)

Source fields: `Assets/QuackUp/Scripts/Grid/BlockManager/BlockManagerConfig.cs` and `Assets/QuackUp/Scripts/Grid/Block/BlockController.cs`. Serialized gameplay asset inspected: `Assets/QuackUp/Settings/Grid/Block/BlockManagerConfig.asset`.

- Long tap duration: `0.2` seconds (`LongTapDuration`).
- Dynamic drag offset enabled (`UseDynamicDragOffset: true`); curve is linear from 0 at time 0 to 1 at time 1.
- Dynamic X offset range: `(0, 2)`; dynamic Y offset range: `(0, 5)` (`DynamicOffsetX`, `DynamicOffsetY`).
- Drag tween disabled (`UseBlockDragTween: false`); configured duration `0.25` seconds, default ease, no inertia (`BlockDragInertia: 0`).
- Scale tween disabled (`UseBlockScaleTween: false`); pickup scale multiplier `1.2`.
- Placed-block pickup disabled (`AllowPickUpAfterPlacement: false`).

No input timing, easing, offset, or scale settings were changed. Compare these values/behaviors manually in Editor/device after freeform rendering is available: short tap vs long press, pickup scale, drag pointer offset during movement, and snap/return pacing.
