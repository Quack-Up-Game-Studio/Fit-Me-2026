# Fit-Me 2026 Developer Onboarding Guide

A scalable, flexible, and technical debt-free application always comes with a carefully planned software architecture and design philosophy. While this might create more steps for us, the benefits of having a proper, decoupled framework are insurmountable. 

This document defines the rules, patterns, and guidelines that must be followed when contributing to **Fit-Me 2026**.

---

## 1. Dependency Injection (DI)

Dependency Injection (DI) allows for the complete detachment of logic from `MonoBehaviour`s, making our code testable outside of play mode and cleanly managing lifetimes. In this project, we use **VContainer** as our DI framework.

### Guidelines
- **Avoid Service Locator patterns** such as `GetComponent`, `GetComponentInChildren`, or `FindFirstObjectByType<T>()` in general logic. Use DI or `[SerializeField]` instead.
  
  ```csharp
  // Unpreferable
  var gridManager = FindFirstObjectByType<GridManager>();

  // Preferable (MonoBehaviour View)
  [Inject] private GridManager _gridManager;
  // or
  [SerializeField] private GridManager gridManager;
  ```
  
- **Avoid Global Singletons** (e.g., `GameManager.Instance`). Inject dependencies via constructor injection or VContainer.
- **Single Entry Points**: Organize your context using `LifetimeScope` subclasses (e.g., [ProjectLifetimeScope](file:///mnt/ssd1/All%20projects/Unity/Fit-Me-2026/Assets/QuackUp/Scripts/Core/LifetimeScope/ProjectLifetimeScope.cs) for global services, and feature-specific scopes like `GridLifetimeScope` or `GameplayPanelLifetimeScope` for local contexts).
- **Pure C# Business Logic**: Shift logic away from MonoBehaviours. Use pure C# classes registered to VContainer with **Constructor Injection** as the default.
- **Factory Pattern**: Use factory classes (e.g., `CellFactory`) to instantiate dynamically generated objects instead of calling `Instantiate` directly inside business logic.
- **Injection Attribute**: Always mark constructors intended for injection with `[Inject]`.

---

## 2. Event-driven Programming & Deep Dive: Reactive Programming (RP) with R3

Instead of standard C# events or checking values in `Update()`, we use **R3** (the successor to UniRx) for data binding and event handling.

### Why Reactive Programming?
Imperative programming is pull-based: your code frequently queries for status updates (e.g., reading score in `Update()`) or directly makes method calls across systems (coupling them tightly). Reactive programming is push-based: systems declare **what** they want to react to, and data changes flow through streams of events automatically. This yields:
1. **Low Coupling**: The source of data does not need to know who is listening or how they update.
2. **No Polling**: Completely avoids checking values in `Update()`, saving performance.
3. **Declarative UI**: Binding UI text/sliders to data models becomes a simple declaration.

### Thinking Reactively vs. Imperatively
* **Imperative approach (How)**: "When the player placements a block, fetch the ScoreManager, increment the score, then find the ScoreText component and set its text property."
* **Reactive approach (What)**: "The ScoreText *is* a representation of the Score data stream. Whenever the Score emits a new value, update the text."

### Working with Data Streams
In R3, a stream is represented by `Observable<T>`. You can filter, map, and combine these streams using operators:
```csharp
// Filtering a stream: Only trigger when value is greater than 10
_score
    .Where(x => x > 10)
    .Subscribe(x => Debug.Log($"High score: {x}"));

// Mapping (Transforming) a stream: Convert int stream to string stream
_score
    .Select(x => $"Score: {x}")
    .Subscribe(text => scoreText.text = text);

// Combining streams: Combine current and max values to get percentage
_currentHealth
    .CombineLatest(_maxHealth, (current, max) => (float)current / max)
    .Subscribe(percent => healthSlider.value = percent);
```

### Key Caveats & Specificities in Fit-Me 2026
1. **Subscription Lifetimes**: If you subscribe to a stream, the subscription stays in memory forever unless explicitly disposed. **Never leave floating subscriptions**. Always chain `.AddTo(this)` for MonoBehaviours, or use `disposableBuilder` inside ViewModels.
2. **ReactiveProperty vs. Observable**: Use `ReactiveProperty<T>` for state variables that hold a current value (you can query `CurrentValue` at any time). Use `Observable<T>` (or `Subject<T>`) for fire-and-forget events that don't represent state (like a button click or a game-over signal).
3. **Thread / Context Safety**: In Unity, UI updates must happen on the main thread. Ensure your streams run on the main thread when updating Views (R3 handles this by default in Unity, but be careful when mixing with thread-based async tasks).

### Basic Code Pattern
Expose fields as `ReactiveProperty<T>` or `ReadOnlyReactiveProperty<T>`. Make sure you manage their disposal properly:

```csharp
// View: Reacting to score updates
public class ScoreView : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    private ScoreViewModel _viewModel;
    private IDisposable _bindings;

    [Inject]
    public void Construct(ScoreViewModel viewModel)
    {
        _viewModel = viewModel;
        Bind();
    }

    private void Bind()
    {
        var disposableBuilder = Disposable.CreateBuilder();

        // Bind Score property change to text update
        _viewModel.Score
            .Subscribe(newScore => scoreText.text = $"Score: {newScore}")
            .AddTo(ref disposableBuilder);

        _bindings = disposableBuilder.Build();
    }

    private void OnDestroy()
    {
        // Always dispose subscriptions to clean up memory
        _bindings?.Dispose();
    }
}
```

---

## 3. Asynchronicity with UniTask

We use **UniTask** for high-performance, allocation-free async/await operations, entirely replacing coroutines.

### Guidelines
- **No Coroutines**: All asynchronous tasks (animations, network calls, asset loading) must use UniTask.
- **Cancellation Tokens**: Always pass a `CancellationToken` to tasks that can be aborted mid-way, and bubble it down to nested calls.
  
  ```csharp
  private async UniTask LoadAssetsAsync(CancellationToken cancellationToken)
  {
      await addressableHandle.ToUniTask(cancellationToken: cancellationToken);
  }
  ```

- **Manage CTS Lifecycles**: Always keep, cancel, and dispose of your `CancellationTokenSource`.
  
  ```csharp
  private CancellationTokenSource _taskCts;

  private void StartTask()
  {
      _taskCts?.Cancel();
      _taskCts = new CancellationTokenSource();
      DoWorkAsync(_taskCts.Token).Forget();
  }

  private void OnDestroy()
  {
      _taskCts?.Cancel();
      _taskCts?.Dispose();
  }
  ```

- **Fire-and-Forget**: Use `UniTaskVoid` and `.Forget()` for entry point async methods that are not intended to be awaited.
- **PrimeTween Integration**: Convert PrimeTween sequences and tweens into awaitable UniTasks:
  
  ```csharp
  await Tween.Scale(gameObject, scaleSettings).ToYieldInstruction().ToUniTask(cancellationToken: token);
  ```

---

## 4. GUI Architecture: MVVM (Model-View-ViewModel)

We structure our UI Panels using a strict MVVM pattern connected to the game's Managers/Models.

```
       +------------------+
       |   PanelManager   | (Controls panel transitions/crossfading)
       +--------+---------+
                |
                v
       +------------------+
       |    PanelView     | (Receives input, handles visual effects)
       +--------+---------+
                | [Binds R3 Observables]
                v
       +------------------+
       |  PanelViewModel  | (Maintains UI state, triggers commands)
       +--------+---------+
                |
                v
       +------------------+
       |  Managers/Models | (Contains core state, e.g., Score, Level)
       +------------------+
```

### 1. Model / Domain
Maintains the core game logic and persistent data (e.g. `IScoreManager`, `IGameStateManager`). Often exposes data reactively.

### 2. View Model
Derives from [PanelViewModel](file:///mnt/ssd1/All%20projects/Unity/Fit-Me-2026/Assets/QuackUp/Scripts/Panel/Panel/PanelViewModel.cs). Binds state changes and exposes commands:

```csharp
public class GameplayPanelViewModel : PanelViewModel
{
    public ReactiveCommand PauseCommand { get; } = new();
    private readonly IGameStateManager _gameStateManager;

    [Inject]
    public GameplayPanelViewModel(
        PanelManager panelManager,
        IGameStateManager gameStateManager) : base(panelManager)
    {
        _gameStateManager = gameStateManager;
        PauseCommand.Subscribe(_ => OnPause());
    }

    private void OnPause()
    {
        _gameStateManager.SetPause(true);
    }
}
```

### 3. View
Derives from [PanelView](file:///mnt/ssd1/All%20projects/Unity/Fit-Me-2026/Assets/QuackUp/Scripts/Panel/Panel/PanelView.cs). Binds to ViewModel fields and has **no business logic**:

```csharp
public class GameplayPanelView : PanelView
{
    [SerializeField] private Button pauseButton;
    [SerializeField] private string pausePanelId = "Pause";

    private GameplayPanelViewModel ViewModel => (GameplayPanelViewModel)BaseViewModel;
    private IDisposable _bindings;

    [Inject]
    public override void Construct(IPanelViewModel viewModel)
    {
        base.Construct(viewModel);
        Bind();
    }

    private void Bind()
    {
        var builder = Disposable.CreateBuilder();
        
        pauseButton.onClick.AsObservable()
            .Subscribe(_ => OnPauseClicked())
            .AddTo(ref builder);
            
        _bindings = builder.Build();
    }

    private void OnPauseClicked()
    {
        ViewModel.PauseCommand.Execute(Unit.Default);
        if (TryGetCrossfadeRule(pausePanelId, out var rule))
        {
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData(pausePanelId, rule.crossfadeSettings));
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        _bindings?.Dispose();
    }
}
```

---

## 5. Project-Specific Conventions (Unique to Fit-Me)

### A. Centralized Event Routing: The Custom `MessageHub`
While we use `MessagePipe` under the hood, we wrap it in a custom class called [MessageHub](file:///mnt/ssd1/All%20projects/Unity/Fit-Me-2026/Assets/QuackUp/Scripts/Utils/Other/MessageHub.cs). 

> [!WARNING]
> Do NOT publish or subscribe using direct `IPublisher<T>` or `ISubscriber<T>` across systems. Always route them through an implementation of `IMessageHub`.

#### Enforcing Centralized Declarations
Each domain has a dedicated hub subclass (e.g., `GridManagerMessageHub`). The hub's constructor resolves concrete publishers/subscribers from VContainer and registers them to a lookup table, keeping event declarations centralized:

```csharp
public class GridManagerMessageHub : MessageHub
{
    [Inject]
    public GridManagerMessageHub(
        ISubscriber<SpawnWithGridPresetEvent> spawnSubscriber,
        IPublisher<SpawnWithGridPresetEvent> spawnPublisher)
    {
        // Enforce registration in the constructor
        MessageWrappers[typeof(SpawnWithGridPresetEvent)] = 
            new MessageWrapper<SpawnWithGridPresetEvent>(spawnPublisher, spawnSubscriber);
    }
}
```

#### Emitting & Subscribing via Hub
```csharp
// Emitting:
_messageHub.Publish(new SpawnWithGridPresetEvent(preset));

// Subscribing:
_messageHub.Subscribe<SpawnWithGridPresetEvent>(evt => DrawGrid(evt.GridPreset));
```

### B. Panel Transitions and Crossfading
The transition between UI windows is handled globally by [PanelManager](file:///mnt/ssd1/All%20projects/Unity/Fit-Me-2026/Assets/QuackUp/Scripts/Panel/PanelManager.cs).
* In the Inspector of your `PanelView` prefab, you must populate the `crossfadeRules` dictionary.
* This dictionary configures the `ITransitionGroup` (fading CanvasGroups, slide effects, scale twining) to run on transition In or Out.
* Do not manually trigger transitions in custom View classes. Always use the `CrossfadeCommand` of your `PanelViewModel` or request it through `PanelManager.Crossfade()`.

### C. Game Saving and Odin Integration
Our local save system leverages high-performance serialization using contractless standard resolvers:
* Save data classes implement `IMessagePackSaveData` and are marked with `[MessagePackObject(AllowPrivate = true)]`.
* ScriptableObjects representing saving profiles inherit from `MessagePackSaveObject<T>` (e.g. `PlayerRecordSaveObject`). Since `MessagePackSaveObject` inherits from `SerializedScriptableObject`, Odin can display and serialize complex, polymorphic types in the Inspector.
* Odin Serializer is used inside lifetime scopes to serialize interfaces and custom lists inside inspector panels cleanly. Ensure you inherit from `SerializedMonoBehaviour` or `SerializedLifetimeScope` if you need Odin's features.
* **Schema Migration Resolvers**: To prevent breaking player save files when data schemas change between game updates, implement the `ISaveMigrationResolver<T>` interface. The save system automatically parses version numbers, constructs a version graph, and finds the shortest migration path using a Breadth-First Search (BFS) algorithm. It processes the migration steps via dynamic `ExpandoObject` mapping before returning the final updated save type.

### D. ScriptableObjects for Configuration
Instead of declaring settings (such as speeds, scores, or prefabs) directly on MonoBehaviours inside the scene hierarchy, we store them inside `ScriptableObject` assets (e.g., `GridManagerConfig`, `LevelManagerConfig`).
* **Registration**: The configuration asset is referenced in the installer/LifetimeScope and registered to VContainer as an instance (`builder.RegisterInstance(config)`).
* **Injection**: The manager classes inject the configuration directly:
  ```csharp
  [Inject]
  public LevelManager(LevelManagerConfig config)
  {
      _config = config;
  }
  ```

### E. Centralized Scene Loading and Transitions (`LoadSceneManager`)
To keep individual scenes completely decoupled, never call Unity's raw `SceneManager.LoadScene` directly.
* **Event-driven Loading**: Trigger scene loads by publishing a `LoadSceneEvent` (specifying target `SceneType`, mode, and whether to use a loading screen).
* **Centralized Transitions**: The `LoadSceneManager` processes the request, plays transition SFX via `IAudioManager`, and runs screen-fade animations using the active `ITransitionable` (e.g., `FadeToBlackTransitionPanel` or `BlockCascadeScreen`).
* **Unified Lifecycle Broadcasts**: Throughout the load process, it publishes `LoadSceneStageEvent` events (`StartOut`, `FinishOut`, `StartLoading`, `FinishLoading`, `StartIn`, `FinishIn`) allowing other systems to react dynamically (e.g., pausing gameplay, cleaning up cache, or resetting inputs).

### F. UniTask-Powered State Machine (`ExtendedStateMachine`)
For systems requiring complex, multi-step sequential logic (such as `TutorialStateMachine`), we use a custom state machine.
* **Modular States**: Create individual state classes by inheriting from `State`. Override the asynchronous `Enter()` and `Exit()` UniTask tasks to handle entries/exits (e.g., awaiting animations or pop-ups) cleanly.
* **Navigation Utilities**: The `ExtendedStateMachine<T>` handles async state transitions and provides out-of-the-box methods for linear navigation (`Next()`, `Previous()`), direct jumps (`JumpTo(key)`), offsets (`NextBy(offset)`), and state history backtracking (`Revert()`).

---

## 6. Assembly Management

We use Assembly Definitions (`.asmdef`) to split code compilation, minimizing build times and forcing clean boundary interfaces.
* **Engine Framework Code**: Named under the **`QuackUp.<Module>`** format (e.g. `QuackUp.Save`, `QuackUp.Audio`, `QuackUp.Input`).
* **Game-Specific Code**: Named under the **`FitMe.<Module>`** format (e.g. `FitMe.Grid`, `FitMe.Panel`, `FitMe.GameData`).
* Avoid direct references between distinct domain assemblies unless necessary. Rely on interfaces defined in `FitMe.Shared` or event hubs to bridge systems.

---

## 7. Addressables & Assets

Assets that are likely to change post-release (prefabs, level presets, themes) must be packed into Addressable groups.
* Always release addressable handles using `handle.Release()` once you are done using the loaded asset to clear memory.
* Use type-restricted references (e.g. `AssetReferenceGameObject`, `AssetReferenceSprite`) rather than a generic `AssetReference` to avoid mistakes in the Inspector.

---

## 8. Code Styling Conventions

Having a consistent style is critical for debugging and readability:

* **Casing Rules**:
  * **`camelCase`**: Use for local variables, fields decorated with `[SerializeField]` or `[OdinSerialize]`, and public fields.
  * **`_camelCase`**: Use for private or protected backing fields (e.g. `private readonly IMessageHub _messageHub;`).
  * **`PascalCase`**: Use for properties, methods, classes, records, structs, and namespaces.
  
* **Inspector Attribute Arrangement**:
  Arrange field attributes in this order:
  1. Heading: `[Title("...")], [Header("...")]`
  2. Condition/Visibility: `[ShowIf("...")], [HideIf("...")]`
  3. Cosmetic/Appearance: `[ReadOnly], [HideLabel]`
  4. Dependency/Serialization: `[Inject], [SerializeField], [OdinSerialize]`
  
* **Properties over Fields**:
  Avoid simple backing properties. Use auto-properties with private setters:
  
  ```csharp
  // Preferable
  [field: SerializeField] public int MaxRunDataCount { get; private set; } = 3;
  ```

* **Guard Clauses**:
  Reduce nesting using guard cases:
  
  ```csharp
  // Preferable
  if (!evt.IsOver) return;
  if (IsTutorial) return;
  GameOver();
  ```

* **MonoBehaviour Null Checking**:
  Use `!` or implicit boolean conversion instead of `== null` when checking MonoBehaviour classes (to respect Unity's underlying C++ lifetime wrapper):
  
  ```csharp
  // Preferable
  if (!recordBlock) return;
  ```

* **Logging**:
  Always use **`DebugUtils.Log`** and **`DebugUtils.LogError`** instead of Unity's native `Debug.Log`. This allows us to disable logging outputs globally for release builds.
