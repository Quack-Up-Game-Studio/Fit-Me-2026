# Scene Loading Lifecycle Contract

**Status:** implemented and tested on `refactor/scene-loading-lifecycle`; not yet integrated into root `Dev`.

This note documents the stage-aware cancellation and ownership contract for `LoadSceneManager`. See the associated 3a checklist and saved scene plan for scope and checkpoint evidence.

## Ownership and serialization

- `LoadSceneManager` owns one request at a time. A new request is ignored while another request is covering, loading, recovering, or revealing; the request lock is released only by its terminal cleanup.
- Each request owns its cancellation source and native-operation subscriptions. `Release` detaches subscriptions, disposes the source, and clears the active request only when identities match.
- `ISceneLoadBackend` isolates Unity scene operations for controlled manager tests. `UnitySceneLoadBackend` remains the production adapter; native PlayMode tests separately exercise Unity's actual Single/Additive scene behavior.
- Cancellation of the request is not permission to abandon a borrowed transition view. Before native loading, cancellation reveals the source scene; after commitment, the loader completes the native operation and recovery/reveal before releasing ownership.

## Commit boundary and cancellation

The irreversible boundary is `ISceneLoadBackend.BeginLoad`:

1. **Before `BeginLoad`:** cancellation/failure stops the request before destination loading. The source transition is restored, and `CancelledBeforeLoad` is published with both scene fields set to the previous/source scene. No destination success stages are published. Source-scene consumers use this event to restore effects that `StartOut` stopped (for example gameplay music and the Main Menu banner).
2. **After `BeginLoad`:** cancellation skips the remaining minimum-cover wait, but it must always allow scene activation and await native completion. Do not attempt to roll back the committed scene switch or leave activation disabled. Complete Additive source cleanup and reveal/recovery before accepting another request.
3. Cancellation does not refund energy or roll back caller-owned gameplay decisions. The existing `useLoadingScene` behavior and broader scene-flow redesign are outside this lifecycle slice.

## Stage and callback behavior

- Successful stages remain `StartOut`, `FinishOut`, `StartLoading`, `FinishLoading`, `StartIn`, `FinishIn`. `CancelledBeforeLoad` is a separate terminal outcome, not a successful destination stage.
- Unity's loaded/active-scene callbacks can arrive in mode-dependent order. Subscribe before beginning native loading; identify the destination by mode and path; do not assume one universal callback order.
- Native callback and stage-subscriber exceptions are transferred to the owning request. Drain the committed engine operation and restore a live owner's input/reveal path before reporting the error. Do not strand an Additive request by releasing the source or activation handlers prematurely.
- Stage publication is synchronous and can re-enter manager disposal. Re-check owner lifetime after publication before touching the transition view.
- The public `LoadScene` task retains its established boundary: it completes after the request is handed to the background loader, not after the final reveal. Tests must preserve both that behavior and mode-specific native stage order.

## Validation boundaries

- Fake-backend EditMode tests cover request serialization, cancellation boundaries, callback/publication faults, source recovery, and teardown/reentrancy.
- Native PlayMode tests cover real Single/Additive loading, activation, callback order, and fixture cleanup. They complement rather than replace fake-backend tests.
- Worktree test results do not establish root-editor gameplay smoke, player-build validation, or integration. Those remain separately gated.
- Keep caller energy policy, loading-scene behavior, global transition queueing, and unrelated transition redesign out of this slice.
