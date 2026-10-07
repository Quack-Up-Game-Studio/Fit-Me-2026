# Pass 3 Preservation Checklist

Use this checklist **before any P3 cleanup** to identify code, utilities, assets, or architecture that should be preserved for future use.

## How to use

- Check **Keep** for anything that must remain in the repository.
- Check **Remove** only when you are certain it is obsolete.
- Check **Investigate** when the item may be useful but needs a future feature decision.
- Add a short reason in the **Decision / rationale** column.
- If an item is kept, P3 cleanup must not delete it or remove its required assembly references.

> Default policy: unchecked items are **not approved for removal**. The P3 implementation should wait until the relevant decisions below are made.

---

## P3-A — Dead code and repository hygiene

| Keep | Remove | Investigate | Item | Location / symbol | Decision / rationale |
|---|---|---|---|---|---|
| [ ] | [ ] | [ ] | Reload-scene helper | `ReloadScene` references | |
| [ ] | [ ] | [ ] | Fade tween state | `_fadeTween` | |
| [ ] | [ ] | [ ] | Count-off timing | `CountOffDuration` | |
| [ ] | [ ] | [ ] | Legacy game-record save types | `GameRecordSave*` | |
| [ ] | [ ] | [ ] | Grid dead members | GridManager-related unused fields/methods | |
| [ ] | [ ] | [ ] | Grid configuration switches | Unused grid config flags | |
| [ ] | [ ] | [ ] | Factory current properties | Factory `Current` properties | |
| [ ] | [ ] | [ ] | Pausable timer utility | `PausableTimer` | |
| [ ] | [ ] | [ ] | Event filter utility | `EventFilters` | |
| [ ] | [ ] | [ ] | VSync/framerate wiring | `VsyncAndFramerateController` wiring | |
| [ ] | [ ] | [ ] | R3 helper utilities | `R3Utils` | |
| [ ] | [ ] | [ ] | Floating UI manager | `FloatingUIManager` | |
| [ ] | [ ] | [ ] | UI title placeholders | `_title` fields used only as inspector labels | |
| [ ] | [ ] | [ ] | Input-blocked property | `IsInputBlocked` | |
| [ ] | [ ] | [ ] | Commented-out implementation blocks | Comments containing retired code | |
| [ ] | [ ] | [ ] | Debug save asset | `Assets/QuackUp/Resources/DebugSaveManager.asset` | |

### Unused assembly references

| Keep | Remove | Investigate | Assembly reference | Location | Decision / rationale |
|---|---|---|---|---|---|
| [ ] | [ ] | [ ] | `FitMe.Entity` references | Fit-Me asmdefs | |
| [ ] | [ ] | [ ] | `FitMe.Achievement` references | Fit-Me asmdefs | |
| [ ] | [ ] | [ ] | `FitMe.Tutorial` references | Fit-Me asmdefs | |
| [ ] | [ ] | [ ] | `FMODUnity` reference | `QuackUp.Save` asmdef | |
| [ ] | [ ] | [ ] | `QuackUp.SceneManagement` reference | `FitMe.GameData` asmdef | |

---

## P3-B — Duplication reduction

These are candidates for refactoring rather than automatic deletion.

| Keep as-is | Refactor | Investigate | Candidate | Scope | Decision / rationale |
|---|---|---|---|---|---|
| [ ] | [ ] | [ ] | Settings core duplication | Settings / `UniversalSettings` (~150 LOC) | |
| [ ] | [ ] | [ ] | Ad layout shift duplication | Repeated ad-layout shift blocks | |
| [ ] | [ ] | [ ] | Tutorial hint-state duplication | Repeated tutorial hint state logic | |
| [ ] | [ ] | [ ] | Cumulative stat achievement duplication | `CumulativeStatAchievement` family | |
| [ ] | [ ] | [ ] | Solution provider prologue duplication | `SolutionProvider` implementations | |

---

## P3-C — Architecture decisions

| Preserve | Retire | Grow / implement | Decision item | Scope | Decision / rationale |
|---|---|---|---|---|---|
| [ ] | [ ] | [ ] | Entity module direction | `FitMe.Entity` scaffolding | |
| [ ] | [ ] | [ ] | Grid preset factory | Optional S1 phase 2: `IGridPresetFactory` | |
| [ ] | [ ] | [ ] | Grid render orderer | Optional S1 phase 3: `GridRenderOrderer` | |
| [ ] | [ ] | [ ] | Debug logging enforcement | Analyzer / `.editorconfig` ban on direct `UnityEngine.Debug` | |

---

## Additional items discovered during P3

| Keep | Remove | Investigate | Item | Location / symbol | Decision / rationale |
|---|---|---|---|---|---|
| [ ] | [ ] | [ ] | | | |
| [ ] | [ ] | [ ] | | | |
| [ ] | [ ] | [ ] | | | |
| [ ] | [ ] | [ ] | | | |
| [ ] | [ ] | [ ] | | | |

---

## Approval summary

- [ ] All P3-A deletion candidates reviewed.
- [ ] All unused assembly references reviewed.
- [ ] P3-B refactor candidates classified as keep, refactor, or investigate.
- [ ] P3-C architecture decisions recorded.
- [ ] No cleanup commit may remove an unchecked item.

**Reviewed by:**  
**Review date:**  
**Notes:**  

---

# Decision Guide with Examples

This section explains what each candidate generally means in the current codebase and what choosing each option would authorize.

## P3-A examples

### `ReloadScene`

**Typical shape:**

```csharp
public void ReloadScene()
{
    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
}
```

**Why it may be obsolete:** the project now uses the injected `LoadSceneManager` and its cancellation/recovery contract. Keeping this may be useful only if a future debug tool or legacy scene needs a simple restart action.

- **Keep** if you want a debug/restart shortcut.
- **Remove** if every caller should use `LoadSceneManager`.
- **Investigate** if it is only referenced by editor/debug code.

### `_fadeTween`

**Typical shape:**

```csharp
private Tween _fadeTween;

_fadeTween = fadePanel.FadeTo(1f);
```

**Decision question:** is this field used to cancel/replace an active animation, or is it assigned but never read?

- **Keep** if it is required to stop a previous fade before starting another.
- **Remove** if the transition object already owns cancellation and the field has no reads.
- **Investigate** if it is only needed during rapid scene re-entry.

### `PausableTimer`

**Typical usage:**

```csharp
_timer = new PausableTimer(duration);
_timer.Pause();
_timer.Resume();
```

**Decision question:** will a future feature need pause/resume semantics, such as timed boosters, energy recharge, or tutorial countdowns?

- **Keep** if pause/resume is a likely reusable gameplay requirement.
- **Remove** if the project standard is now UniTask plus explicit elapsed-time tracking.
- **Investigate** if no current caller exists but the API is well-tested and generic.

### `R3Utils`

**Typical shape:**

```csharp
var stream = source
    .Select(x => x.Value)
    .Subscribe(OnChanged);
```

A helper may wrap subscription creation, disposal, or conversion between `IObservable<T>` and R3 `Observable<T>`.

- **Keep** if it prevents repeated subscription/disposal mistakes.
- **Remove** if it only aliases a one-line R3 operation with no safety benefit.
- **Investigate** if it bridges a package boundary and removing it would spread adapter code.

### `IsInputBlocked`

**Typical usage:**

```csharp
if (IsInputBlocked)
    return;
```

- **Keep** if gameplay input still reads it during transitions or modal UI.
- **Remove** only if input blocking is fully centralized in the current transition/input service.
- **Investigate** if the property has no current callers but represents a useful future modal-input contract.

### `DebugSaveManager.asset`

This is an editor-oriented asset rather than ordinary runtime code.

- **Keep** if you still use the editor save/load tools for debugging or QA.
- **Remove/untrack** only if the debug workflow is no longer used and the asset should never ship.
- **Investigate** if the code is editor-only but the asset is still useful to designers.

## Unused assembly-reference examples

Removing an asmdef reference changes what the assembly is allowed to compile against. For example:

```text
FitMe.GameData.asmdef
    references:
    - QuackUp.SceneManagement
```

If no source file uses a type from that assembly, removing the reference reduces coupling. However, keep it when:

- an assembly uses types through generated code;
- a future feature is already planned in that module;
- removing it would force a public API redesign;
- the reference is required by editor-only code in the same assembly.

**Recommended choice:** mark **Investigate** when you have not checked both runtime and editor-only files.

## P3-B duplication examples

### Settings duplication

Before a refactor, two settings classes may repeat the same fields:

```csharp
public class Settings
{
    public float MusicVolume;
    public float EffectsVolume;
}

public class UniversalSettings
{
    public float MusicVolume;
    public float EffectsVolume;
}
```

Choose **Refactor** only if both classes represent the same concept and should share one source of truth. Choose **Keep as-is** if they intentionally represent different scopes or save formats.

### Tutorial hint-state duplication

Repeated states may contain the same lifecycle pattern:

```csharp
protected override async UniTask Enter(CancellationToken token)
{
    ShowHint().Forget();
    await WaitForCompletion(token);
}

protected override async UniTask Exit(CancellationToken token)
{
    await HideHint(token);
}
```

Choose **Refactor** if the repeated code has identical cancellation and disposal semantics. Choose **Keep as-is** if each state has materially different timing or ownership.

### `SolutionProvider` prologue duplication

Several providers may repeat setup such as:

```csharp
var schema = gridManager.CreateVacantSchema();
var candidates = GetCandidates(schema);
if (candidates.Count == 0)
    return null;
```

Choose **Refactor** if the setup and failure behavior are identical. Choose **Keep as-is** if provider-specific heuristics depend on different preparation steps.

## P3-C architecture examples

### Entity module

- **Preserve:** leave the scaffolding available for a planned entity-based feature.
- **Retire:** remove the unused assembly and types after confirming no planned feature depends on them.
- **Grow / implement:** define a real use case first, then add only the required domain model and systems.

### `IGridPresetFactory`

A possible future boundary would look like:

```csharp
public interface IGridPresetFactory
{
    GridPreset Create(GridPresetRequest request);
}
```

Choose **Grow / implement** only if the game needs runtime-generated or mode-specific presets. If all presets remain authored ScriptableObjects, **Preserve** or leave deferred is safer.

### `GridRenderOrderer`

A possible future service would centralize visual ordering:

```csharp
public interface IGridRenderOrderer
{
    void ApplyOrder(IReadOnlyList<CellView> cells);
}
```

Choose **Grow / implement** if render-order rules are becoming complex or shared across scenes. Otherwise, preserve the idea without adding an abstraction yet.

### Debug logging enforcement

The proposed rule would reject direct Unity logging in gameplay code:

```csharp
// Disallowed by a future analyzer
UnityEngine.Debug.Log("Placed block");

// Preferred project path
DebugUtils.Log("Placed block");
```

Choose **Grow / implement** only after the naming and logging policy is stable. Otherwise, **Investigate** to avoid introducing analyzer noise during active refactoring.

## Practical decision examples

| Situation | Recommended choice |
|---|---|
| You expect to reuse the utility in a planned feature within the next few months | **Keep** |
| The symbol has no callers, no tests, and duplicates a newer project service | **Remove** |
| The symbol has no callers, but its API could support a plausible future feature | **Investigate** |
| An asmdef reference is unused but the module boundary is still evolving | **Investigate** |
| Two implementations are similar but have different cancellation or save semantics | **Keep as-is** |
| A proposed abstraction has no current consumer | **Preserve the decision as deferred; do not implement yet** |

