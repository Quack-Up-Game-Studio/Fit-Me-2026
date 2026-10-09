# Shape Playset Generator Redesign — Implementation Plan

**Status:** Draft for review; no redesign implementation changes are authorized by this plan alone.
**Worktree:** `.worktrees/shape-playset-generator`
**Branch/base:** `feature/shape-playset-generator`, current implementation commit `61efe71c`
**Scope:** Replace the current corner-rectangle obstacle construction and fixed-rectangle fallback with seeded arbitrary obstacle candidates, undersized-region repair, and bounded mixed preset/generated exact-cover search. Keep this work isolated from Pass 8 and do not integrate or push without separate approval.

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

## Implementation phases

### 0. Establish baseline and freeze the contract

- Recheck worktree, branch, HEAD, and status; preserve the current implementation as a save point before source edits.
- Trace current config/result contracts, preset schema validation, solver APIs, rotation behavior, and all usages of `BlockPreset` validation.
- Resolve the open policy questions below before changing the public config/error contract.
- Keep the implementation pure/deterministic and separate from scene/UI/runtime integration.

### 1. Write tests for the new contracts (RED)

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

### 2. Implement deterministic obstacle candidate generation

- Use separate deterministic random streams for dimensions/count, obstacle sampling, per-candidate retry, and any randomized tie-breaks; prefer stable ordering over randomness within repair/search.
- Select the obstacle count uniformly from the configured inclusive range, then sample unique cells without replacement.
- Record seed, attempt index, generator version, and complete effective config in the result/provenance data.

### 3. Implement region analysis and repair

- Find all playable components using 4-way BFS and compute sizes.
- If every component is below threshold, target the largest; otherwise target the largest component (which is necessarily threshold-sized).
- For each undersized component, find a deterministic minimum-obstacle path to the target. Clear path obstacles, then relocate the same number of obstacles to cells whose removal does not create an under-threshold component.
- Recompute components after each repair/relocation because one operation may merge or split regions.
- Charge path/relocation exploration to the obstacle-repair node budget. Exhaustion or no legal relocation rejects this candidate; it does not by itself prove the overall config impossible.

### 4. Implement bounded mixed exact-cover fallback

- Validate preset footprints and configured counts; audit shared `BlockPreset` validation so malformed or non-4-connected assets are caught at the correct authoring/config boundary.
- Search placements with the objective: maximize preset-covered cells, then preset-piece count, then minimize singleton fallbacks; use stable tie-breaks.
- At uncovered cells, lazily enumerate generated fallback footprints from remaining playable cells. Require 4-connectivity, configured tight-box min/max dimensions in generated orientation, and only playable/uncovered cells. Permit internal zeroes only when the occupied footprint stays 4-connected. Include 1x1 candidates only up to the independent singleton cap.
- Let placement search apply the supported rotations after a footprint passes generated-orientation bounds.
- Use pruning (remaining area, legal-candidate checks, most-constrained-cell/block ordering, symmetry/canonical-shape deduplication) and charge search nodes to the tiling budget. Distinguish exhaustive no-cover from budget exhaustion.

### 5. Integrate result/error contracts and metrics

- Add explicit outcomes for invalid configuration, feasibility unproven, candidate repair failure, proven no-tiling candidate, tiling-budget exceeded, and candidate-retry exhaustion (including per-stage counts/reasons).
- Ensure bounded attempts are deterministic for a fixed seed/config/budget and no exhausted candidate is misreported as a config error.
- Update config/output snapshots to include all settings and budget/version inputs required to reproduce the run. Confirm whether preset identity alone is sufficient or whether footprint/schema fingerprints are needed.
- Keep metric definitions transparent and increment the metric model version if their meaning changes.

### 6. Verification and review

- Run focused generator/solver EditMode tests with the project's Unity Android-target headless runbook; require fresh parseable NUnit XML and inspect matching logs.
- Run the owning `FitMe.Grid.Tests` suite after the focused tests.
- Exercise representative boundary seeds and deterministic retries; add bounded property-style loops over seeds/configurations where practical.
- Run `git diff --check`, inspect the exact diff, and report tests plus unverified limits.
- Present the final diff for interactive approval before the implementation commit. No push or integration is part of this plan.

## Open policy questions

1. **Config-feasibility preflight budget:** the user selected a distinct feasibility-unproven result when bounded preflight cannot decide. Define whether preflight has its own node budget or uses cheap/provable checks only; do not convert preflight exhaustion into a claim of impossibility.
2. **Malformed preferred presets:** decide whether an invalid configured preset makes the config invalid, or is excluded while generated fallback continues. Prefer an explicit validation error rather than silently accepting malformed authored content; inspect existing asset validation before locking this in.
3. **Retry-budget convention:** define whether the configured value counts retries after the initial attempt or total attempts. Recommended: name/configure `MaxCandidateAttempts` to include the initial attempt and avoid off-by-one ambiguity.
4. **Repair target semantics:** current proposal targets the largest component for all repairs; confirm if an undersized region should instead connect to the nearest component already meeting the threshold when one exists.
5. **Preset objective details:** the agreed primary/tie-break objectives are preset cells, preset-piece count, then singleton count. Define subsequent tie-breakers (e.g. fewer total generated pieces, then stable shape/placement order).
6. **Preset snapshot reproducibility:** decide whether stable preset IDs/counts suffice or whether the snapshot should also store a footprint/schema fingerprint so edits to an asset do not silently change reproduction.

## Explicit non-goals

- No scene, panel, prefab, or runtime generation integration in this slice.
- No claim that proxy metrics correspond to human difficulty; playtesting remains necessary.
- No changes to Classic/endless obstacle generation or hand-authored content flow.
- No integration into the shared Pass 8/root worktree and no push.
