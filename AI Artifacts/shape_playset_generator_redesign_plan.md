# Shape Playset Generator Redesign — Implementation Plan

**Status:** Implemented and committed on `feature/shape-playset-generator` through `a5bbcff5`; merged into the Pass 10 integration branch, with integrated verification still pending.
**Worktree:** `.worktrees/shape-playset-generator`
**Branch/checkpoint:** `feature/shape-playset-generator`, based on `034c9c260756eed2af8d8486c6fc93de67e28da3`; plan save point `528f87aa`; implementation save point `f9e022de`. A reviewed preflight correction remains uncommitted.
**Scope:** Replace the prototype's corner-rectangle obstacle construction and fixed-rectangle fallback with seeded arbitrary obstacle candidates, undersized-region repair, and bounded mixed preset/generated exact-cover search. Keep this work isolated from Pass 8 and do not integrate or push without separate approval.

## Current implementation checkpoint

- Arbitrary obstacles, deterministic repair/relocation, 4-connected playzones and block footprints, bounded mixed preset/fallback exact cover, retry provenance, stage failure counts, and output/config snapshots are implemented.
- Shape Mode preferred presets are validated for binary, non-empty, 4-connected schemas both during generation preflight and authored level validation; shared `BlockPreset` behavior outside Shape Mode is unchanged.
- Generated blocks report their clockwise placement rotation separately from their footprint orientation.
- A regression test proved and then verified the fix for rejecting obstacle counts that fit a larger configured board but not the smallest one. The final `FitMe.Grid.Tests.dll` run passed 110/110 with zero failures or skips.

## Agreed behavior

- Obstacle count is selected uniformly from the configured inclusive min/max range; then that many distinct cells are sampled using a deterministic seeded stream. This is uniform over counts, not uniform over every possible mask.
- Obstacles may have arbitrary shapes and may be disconnected. Playzone regions are measured with 4-neighbor connectivity.
- Minimum playzone-region size is configurable, default 10 cells. Regions at or above the threshold may remain separate. If all regions are below threshold, the largest is the repair target; use deterministic tie-breaking.
- Repair a too-small region by finding a deterministic low-cost path to the largest/eligible region, converting obstacle cells on that path to playable cells, then relocating the removed obstacles to safe cells. Preserve obstacle-count bounds and recheck all regions after changes.
- Try preferred presets first, retaining the maximum total preset-covered cell count; break ties by retaining more preset pieces, then using fewer singleton fallbacks.
- Block footprints must be 4-connected: every occupied cell must be reachable from every other through side-adjacent occupied cells. Diagonal-only contact does not connect a block. This is a separate check from playzone connectivity, even though both use four-directional adjacency. Empty cells inside a footprint's tight bounding box are allowed if the occupied cells remain 4-connected.
- Fallback pieces have configurable min/max tight-box width and height. Check dimensions in the generated orientation; placement rotations are a separate step and can swap width/height.
- The singleton fallback cap is independent and explicitly permits up to that many 1x1 pieces even when the fallback minimum box is larger.
- Obstacle repair, tiling search, and candidate retries have separate deterministic budgets. Budget exhaustion rejects a candidate; retrying uses a deterministic stream derived from the master seed and attempt index.
- Configuration errors are reserved for proven-invalid/impossible settings. Bounded config preflight that cannot decide returns a distinct feasibility-unproven result. A mask rejected by repair/tiling, or a search that exceeds its budget, is a candidate failure. Retry exhaustion returns a candidate-retries-exhausted result with per-stage failure counts.
- Difficulty outputs remain algorithmic proxies, versioned separately and not presented as human-validated difficulty.

## Shape-character controls (generator v6)

- The generation profile exposes `Rectangular Preference` and `Repetition Penalty`, each on `[0, 1]` with defaults `0.35` and `0.5`. Rectangular preference `0.5` is neutral; values below it penalize and values above it reward solid rectangles. Repetition penalty `0` ignores duplicates and `1` strongly discourages repeated shapes.
- A solid multi-cell rectangle includes horizontal and vertical lines; singletons are excluded from rectangle scoring because singleton fallbacks have their own objective. Repetition identity is the exact normalized footprint in its stored orientation; rotations are distinct because Shape Mode blocks do not rotate.
- Complete playable-cell coverage remains mandatory. The bounded tiling search ranks complete layouts first by fewer singleton fallbacks, then by greater style score (`(2 * rectangularPreference - 1) * rectangleRate - repetitionPenalty * duplicatePairRate`), followed by preferred-preset cell coverage and the existing preset-balance penalty. Preferred presets are preferences and may be traded for style.
- The generator keeps the best complete solution found within the existing tiling-node budget. If further optimization reaches the cap, it returns that valid solution with `BudgetLimited`; a budget exhaustion before any complete solution remains a tiling-budget failure. No exhaustive solution list is retained.
- Search orders each anchor's preferred and fallback placements by their immediate style contribution, retaining preset balance and seeded deterministic tie-breaks where style is tied. Reaching the provable upper bound (zero singletons, maximum style score, maximum preferred coverage, and minimum preset-balance penalty) stops search early as `ProvenOptimal`; this bound is safe for every score term and does not claim optimality when any term can still improve.
- Each generated playset and its provenance record the optimizer's actual node count. The editor labels these separately from solution-analysis nodes; the default 100,000-node budget is unchanged pending broader measurements.
- The optimization objective changes deterministic outputs, so new generated playsets use generator version `6`; existing serialized provenance is preserved as authored.

## Implementation phases

### 0. Establish baseline and freeze the contract — COMPLETE

- Recheck worktree, branch, HEAD, and status; preserve the current implementation as a save point before source edits.
- Trace current config/result contracts, preset schema validation, solver APIs, rotation behavior, and all usages of `BlockPreset` validation.
- Resolve the open policy questions below before changing the public config/error contract.
- Keep the implementation pure/deterministic and separate from scene/UI/runtime integration.

### 1. Write tests for the new contracts (RED/GREEN)

Add focused tests for:

- Uniform obstacle-count bounds, exact selected cell count, deterministic mask for a fixed seed, and distinct candidate attempts from one master seed.
- Four-way playzone component detection, threshold boundary (`T-1` versus `T`), largest-target behavior when all regions are undersized, path clearing, safe obstacle relocation, and preserved obstacle bounds.
- Candidate rejection when repair cannot satisfy region/obstacle constraints within budget.
- 4-connected block acceptance; diagonal-only/disconnected block rejection; empty interior cells accepted only when the remaining occupied cells stay 4-connected; tight-box min/max width/height enforcement.
- Generated-orientation dimension validation followed by rotated placement, including non-square footprints.
- Preset retention objective, mixed preset/generated completion, singleton exception and cap.
- Exact-once playable-cell coverage, no overlap, deterministic placements, and independent obstacle/tiling/retry budgets.
- Proven configuration error versus feasibility-unproven, candidate rejection, and retry exhaustion.

Run the narrow relevant test filters and verify the expected RED is behavioral, not an assembly/setup failure.

### 2. Implement deterministic obstacle candidate generation — COMPLETE

- Use separate deterministic random streams for dimensions/count, obstacle sampling, per-candidate retry, and any randomized tie-breaks; prefer stable ordering over randomness within repair/search.
- Select the obstacle count uniformly from the configured inclusive range, then sample unique cells without replacement.
- Record seed, attempt index, generator version, and complete effective config in the result/provenance data.

### 3. Implement region analysis and repair — COMPLETE

- Find all playable components using 4-way BFS and compute sizes.
- If every component is below threshold, target the largest; otherwise target the largest component (which is necessarily threshold-sized).
- For each undersized component, find a deterministic minimum-obstacle path to the target. Clear path obstacles, then relocate the same number of obstacles to cells whose removal does not create an under-threshold component.
- Recompute components after each repair/relocation because one operation may merge or split regions.
- Charge path/relocation exploration to the obstacle-repair node budget. Exhaustion or no legal relocation rejects this candidate; it does not by itself prove the overall config impossible.

### 4. Implement bounded mixed exact-cover fallback — COMPLETE

- Validate binary preset schemas, 4-connected footprints, and configured counts at the Shape Mode config/authored-level boundaries; leave shared `BlockPreset` behavior outside Shape Mode unchanged.
- Search placements with the objective: maximize preset-covered cells, then preset-piece count, then minimize singleton fallbacks; use stable tie-breaks.
- At uncovered cells, lazily enumerate generated fallback footprints from remaining playable cells. Require 4-connectivity, configured tight-box min/max dimensions in generated orientation, and only playable/uncovered cells. Permit internal zeroes only when the occupied footprint stays 4-connected. Include 1x1 candidates only up to the independent singleton cap.
- Let placement search apply the supported rotations after a footprint passes generated-orientation bounds.
- Use pruning (remaining area, legal-candidate checks, most-constrained-cell/block ordering, symmetry/canonical-shape deduplication) and charge search nodes to the tiling budget. Distinguish exhaustive no-cover from budget exhaustion.

### 5. Integrate result/error contracts and metrics — COMPLETE

- Add explicit outcomes for invalid configuration, feasibility unproven, candidate repair failure, proven no-tiling candidate, tiling-budget exceeded, and candidate-retry exhaustion (including per-stage counts/reasons).
- Ensure bounded attempts are deterministic for a fixed seed/config/budget and no exhausted candidate is misreported as a config error.
- Update config/output snapshots with all settings, retry attempt, budgets/version, preset IDs/counts, and cloned preset schemas so asset edits are visible when reproducing a run.
- Keep metric definitions transparent and increment the metric model version if their meaning changes.

### 6. Verification and review — TESTS COMPLETE; APPROVAL PENDING

- Focused Android-target EditMode regression test: RED before the fix (expected `InvalidObstacleRange`), GREEN after the fix (1/1).
- Final owning `FitMe.Grid.Tests.dll` Android-target EditMode suite: 110/110 passed, zero failures/skips; report `shape-generator-grid-suite-finalfix-20261009.xml` and matching log are in profile scratch.
- Boundary cases, deterministic retries, and bounded seed loops are covered by the focused generator tests.
- `git diff --check` passes; final post-checkpoint diff reviewed and limited to the obstacle-capacity preflight correction, its regression test, and this status update.
- Present the final diff for interactive approval before the implementation commit. No push or integration is part of this plan.

## Resolved policy decisions

- Config preflight uses cheap structural/proven checks only; otherwise valid configs are `Unproven` until a candidate succeeds. Bounded search exhaustion is never called impossible.
- A malformed, empty, dimension-incompatible, or non-4-connected preferred Shape Mode preset makes the config invalid.
- `MaxCandidateAttempts` includes the initial attempt.
- Repair targets the largest component; threshold-sized disconnected playzones may remain.
- The tiling objective is preset-covered cells, then preset-piece count, then fewer singleton fallbacks; remaining exact ties keep the first stable row-major/key-ordered solution.
- Result failure counts are separated by obstacle-repair failure/budget, tiling failure/budget, and invariant failure. Output snapshots preserve full preset schema in addition to ID/count.

## Explicit non-goals

- No scene, panel, prefab, or runtime generation integration in this slice.
- No claim that proxy metrics correspond to human difficulty; playtesting remains necessary.
- No changes to Classic/endless generation and no authored-content migration; Shape Mode validation only rejects block schemas that violate the agreed connectivity/schema rules.
- No integration into the shared Pass 8/root worktree and no push.
