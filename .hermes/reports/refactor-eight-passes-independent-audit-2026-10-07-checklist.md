# Refactor Passes 1–8 — Independent Audit Checklist

**Source report:** `.hermes/reports/refactor-eight-passes-independent-audit-2026-10-07.md`
**Audit baseline:** `ac108e65af59ade1d863a8fda0efdde82ad915e9`
**Integration checkpoint:** `Dev`, baseline `1efe8763` plus the pending IAP correction and checklist update
**Follow-up date:** October 7, 2026
**Purpose:** Record integration of the three priority remediations, final integrated-root verification, and remaining deferred gates.

## Integration summary

- [x] Confirm all three priority branches were based on checkpoint `bc56b93116f0632895508a4a3ba5d226258ca0b1`.
- [x] Discard the two pre-existing modified root IAP files and untracked `AI Artifacts/refactor-audit-report-p0-p3.md`, as explicitly authorized by the user.
- [x] Integrate Priority 0: `71b7e60f` (`fix(iap): complete priority zero ownership refactor`), fast-forwarded into `Dev`.
- [x] Integrate Priority 1: `18e01fe5` (`fix(panel): harden transition ownership and cancellation`), merged into `Dev` by merge commit `5407f0ed`.
- [x] Integrate Priority 2: `22eba349` (`fix(refactor): complete priority 2 audit remediations`), merged into `Dev` by merge commit `1efe8763`.
- [x] Preserve the checklist's already-deferred Gameplay Entity GUID/Odin serialization work; no manual smoke/deferred gates were run.
- [x] Integrated Android-target EditMode compilation succeeds after the Priority 0 IAP adapter was corrected from `Order` to `PendingOrder` in `IStorePurchaseConfirmation`, `StorePurchaseConfirmation`, and the IAP test spy (`InAppPurchaseManager.cs`, `InAppPurchaseManagerTests.cs`).
- [x] Full integrated EditMode suite rerun: Unity 6000.3.9f1, Android target; **145 passed, 0 failed, 0 skipped**. Result: `~/.hermes/profiles/fit-me-development-assistant/cache/scratch/iap-fix-rerun.xml`.
- [x] The immediately preceding full-suite run compiled and executed 145 tests but had one failure: `FailedPendingDeliveryLeavesPurchaseUnconfirmedForRedelivery` did not expect its intentional logged error. Added an explicit `LogAssert.Expect`; the rerun passed 145/145.

## Priority 0 — IAP ownership and durability

- [x] Branch `refactor/priority-0-20261007`, commit `71b7e60f`, integrated.
- [x] Changed files: `InAppPurchaseInstaller.cs`, `InAppPurchaseManager.cs`, `InAppPurchaseManagerTests.cs`.
- [x] Integrated-root Android-target EditMode verification passes after correcting the shared IAP confirmation seam. Result: 145 passed, 0 failed, 0 skipped; see integrated verification below. The earlier failure was `InAppPurchaseManager.cs(35,96)` CS1503 (`Order` passed where Unity IAP 5.3 requires `PendingOrder`).

## Priority 1 — Panel ownership and serialized scene cleanup

- [x] Branch `refactor/audit-priority1-20261007`, commit `18e01fe5`, integrated.
- [x] Changed scene component anchors and panel/promise code and regression tests integrated.
- [x] Gameplay retired Entity GUID and Odin installer serialization remained untouched, per explicit deferral.
- [x] The integrated-root Android-target EditMode suite passes 145/145, covering the merged Priority 1 changes. Isolated worktree result is not required for this integration handoff.

## Priority 2 — Focused owner fixes

- [x] Branch `refactor/priority-2-20261007`, commit `22eba349`, integrated.
- [x] Save/player, purchase effects, save applicability, scene reveal recovery, and focused tests integrated.
- [x] The integrated-root Android-target EditMode suite passes 145/145, covering the merged Priority 2 changes. Isolated worktree result is not required for this integration handoff.

## Integrated verification and repository state

- [x] Post-integration root `Dev` Android-target EditMode suite: Unity 6000.3.9f1; **145 passed, 0 failed, 0 skipped**. The initial attempt was blocked by Bee's Unix socket path under the long Hermes `TMPDIR`; using `$HOME/.fitme-unity-tmp` resolved that environmental issue and exposed CS1503 at the IAP confirmation seam. Corrected `IStorePurchaseConfirmation`, its Unity adapter, and the test spy to accept `PendingOrder`, matching Unity IAP 5.3 `ConfirmPurchase`.
- [x] First full run after compilation: 144 passed, 1 failed because `FailedPendingDeliveryLeavesPurchaseUnconfirmedForRedelivery` intentionally logs an error without registering it with `LogAssert`. Added the expected-log assertion; final rerun passed **145/145**. XML: `~/.hermes/profiles/fit-me-development-assistant/cache/scratch/iap-fix-rerun.xml`; log: `iap-fix-rerun.log` in the same scratch directory.

- `Dev` integration baseline is `1efe8763`; the IAP source/test fix and this checklist update are pending commit. The branch was 59 commits ahead of `origin/Dev` before this follow-up commit. Nothing has been pushed.
- `git diff --check ac108e65..HEAD` reports trailing whitespace in the newly changed `Assets/QuackUp/Prefabs/ProjectLifetimeScope.prefab` serialization data (`SerializedBytes`, empty `Name`/`Data` fields). These appear as Odin/Unity serialized blank values, so they were not manually normalized or reserialized. The focused code/scene/test diff check passed.
- Integration includes changes already present in checkpoint `bc56b931` (which predates these priority merges), including a large `ProjectLifetimeScope.prefab` Odin serialization rewrite, Android Gradle/ABI settings, Android resolver dependencies, a material dimension change, and deletion of an AAR meta. These checkpoint changes were not re-reviewed or modified as part of priority integration.
- The integrated-root 145/145 run is the verification evidence for all three merged priorities; separate isolated worktree results are superseded for this integration handoff. The independent audit report's earlier 134/134 run is historical evidence at its stated revision, not a substitute for the current 145/145 run.
- Not run: full integrated EditMode suite, native scene PlayMode suite, Android player build, root gameplay smoke, store/ads/auth/cloud device checks. Deferred and smoke tests were intentionally not performed per user direction.

## Handoff / audit disposition

All three priority commits are present in local `Dev`. The initial test failure had two sequential causes: Bee could not create its socket under the overlong scratch `TMPDIR`; the short-TMPDIR invocation removed that blocker, then C# compilation exposed the IAP `Order`→`PendingOrder` mismatch. The mismatch is fixed and the complete integrated-root Android-target EditMode suite passes 145/145 after adding the expected-log assertion. The follow-up whitespace check additionally flags blank-valued fields in the Odin-serialized `ProjectLifetimeScope.prefab`; do not hand-edit that serialization. The inherited checkpoint contains other unrelated-looking Unity/editor/config changes as itemized above.

The checklist's previously deferred Gameplay GUID/Odin reserialization task remains open. Manual smoke/release gates remain deferred and were not performed. No changes were pushed.
