# Unity Architecture Comparison: Common Stack vs. Modern Decoupled Stack

If you are trying to convince other Unity developers to move away from traditional "Unity-jank" and adopt a structured, reactive, and dependency-injected design, this document is a tool to help you make your case. 

Here is how our architectural stack compares to standard Unity conventions, and the exact real-world engineering problems it solves.

---

## 1. Quick Comparison Table

| Architectural Pillar | The Common Unity Stack | Our Stack (Fit-Me 2026) |
| :--- | :--- | :--- |
| **Dependency Management** | Singletons (`Instance.Property`), manual Inspector dragging, `GetComponent()`, `FindObjectOfType()` | **VContainer** (Hierarchical `LifetimeScopes`, constructor & method injection, interface binding) |
| **Event Handling & Data Flow** | Imperative polling in `Update()`, standard C# `Action` events, coroutines | **R3 (Reactive Extensions)** (Push-based data streams, `ReactiveProperty`, declarative bindings) |
| **GUI Architecture** | Monolithic `UIManager` scripts mixing animations, game logic, sound triggers, and data | **MVVM (Model-View-ViewModel)** (`PanelView` for visuals, `PanelViewModel` for state/commands, pure C# Models) |
| **Asynchronous Logic** | Unity `Coroutine` (`IEnumerator`), yielding, or multi-threading | **UniTask** (Allocation-free `async/await`, `CancellationTokenSource`) |
| **Configuration Management** | Storing parameters (speeds, scoring, prefabs) inside MonoBehaviour variables on scene GameObjects | **ScriptableObjects** (Injected via VContainer as read-only configuration profiles, allowing easy duplicate testing) |
| **Save & Migration System** | Unity `PlayerPrefs` or raw JSON helper classes; data formats break easily when changing schemas | **MessagePackSaveObject + Odin** (Odin-serializable binary data, schema migration resolvers solved via BFS) |
| **Scene Loading & Transitions** | Direct `SceneManager.LoadScene()` calls, local loading screens, and scattered fading scripts | **LoadSceneManager + ITransitionable** (Decoupled scene loading events with stages and abstracted transition panels) |
| **State Machines** | Monolithic `Update()` switches or basic coroutine-based state loops | **UniTask-powered `ExtendedStateMachine`** (Asynchronous `Enter`/`Exit` tasks, linear navigation, and backtracking history) |
| **Code compilation** | All scripts compiled inside a single `Assembly-CSharp.dll` | **Assembly Definitions (`.asmdef`)** (`QuackUp.*` and `FitMe.*` modules) |

---

## 2. Deep Dive: What Problems Do We Fix?

### A. Dependency Injection (VContainer) vs. Singletons & Inspector Dragging

#### The Common Way Problems:
1. **The Singleton Spaghetti**: Overusing `GameManager.Instance` or `SoundManager.Instance` makes code tightly coupled. You cannot easily test or reuse a class in a different scene without bringing the entire game manager setup with it.
2. **Silent runtime crashes**: If someone forgets to drag a prefab reference into an inspector slot, the game compiles fine but crashes at runtime with a `NullReferenceException`.
3. **Fragile Scene Transitions**: Singletons marked with `DontDestroyOnLoad` frequently cause memory leaks or duplicate instances when reloading scenes.

#### How VContainer Fixes It:
* **Compile-Time / Initialization Safety**: VContainer builds and verifies the dependency graph at startup. If a dependency is missing, it fails immediately on start, pointing you exactly to the missing type.
* **Separation of Scopes**: You can declare scene-specific and popup-specific dependency scopes. When a scene or popup is closed, VContainer automatically destroys and disposes of all dependencies in that scope, preventing memory leaks.
* **Mockability**: By injecting interfaces (e.g., `IAudioManager`) instead of concrete classes, you can inject a mock audio manager during unit tests to test game logic without hearing sound or loading FMOD components.

---

### B. Reactive Programming (R3) vs. Polling & Standard C# Events

#### The Common Way Problems:
1. **Update-loop overhead**: Checking `if (player.Score != lastScore)` inside `Update()` consumes CPU cycles every frame on every active script.
2. **Event listener leaks (Memory leaks)**: In C#, standard events hold a strong reference to the listening object. If you subscribe to a global event and forget to unsubscribe on `OnDestroy()`, the garbage collector **cannot** unload the object, leading to severe memory leaks.
3. **Race Conditions**: Setting a UI text component in a script's `Start()` before the data manager initializes its values in `Awake()` leads to empty text or bugs.

#### How R3 Fixes It:
* **Push-based updates**: UI elements sleep until the data stream pushes a change. Zero performance overhead when variables are static.
* **Disposal Safety**: By using `.AddTo(this)` or a `disposableBuilder`, cleaning up subscriptions is standardized. No more dangling event listeners.
* **Stream Operators**: You can filter (`.Where()`), transform (`.Select()`), and combine (`.CombineLatest()`) data streams on the fly in one line of code, removing messy callback logic.

---

### C. GUI Architecture: MVVM vs. Spaghetti MonoBehaviour UI

#### The Common Way Problems:
1. **God Scripts**: A `MenuScreen.cs` script ends up containing UI layout, tweening animations, button click listeners, score calculation, sound triggers, and save file management all in one 1000-line file.
2. **UI Redesigns Break Code**: If a UI layout changes (e.g. replacing a Slider with a Text field), the code referencing the Slider breaks, requiring rewriting business logic.

#### How MVVM Fixes It:
* **Strict Separation of Concerns**:
  * The `View` only controls what is seen (visual elements and UI animations).
  * The `ViewModel` holds the data structure of the screen.
  * The `Model` runs the game logic.
* **UI Redesign Immunity**: Because the ViewModel doesn't know the View exists (it only exposes raw streams/commands), you can completely redesign, replace, or delete the `PanelView` prefab without changing a single line of code in the ViewModel or Model.

---

### D. Asynchronicity: UniTask vs. Coroutines

#### The Common Way Problems:
1. **Garbage Collection (GC) Spikes**: Yielding in coroutines (`yield return new WaitForSeconds(1)`) allocates garbage memory on the heap every time it runs. This leads to frame-rate stutters when Unity's garbage collector runs.
2. **No Return Values**: Coroutines are `void` processes. If you want a coroutine to fetch data and return it, you have to write complex action callbacks.
3. **Swallowed Exceptions**: If an error occurs inside a coroutine, the exception is logged to the console but does not bubble up, making try-catch error handling impossible.

#### How UniTask Fixes It:
* **Zero Garbage**: UniTask uses C# struct-based tasks, meaning zero heap allocation for asynchronous wait routines.
* **Standard Async/Await**: Writing asynchronous code feels like synchronous code:
  ```csharp
  var data = await DownloadPlayerDataAsync(cancellationToken);
  ```
* **Full Exception & Cancellation Support**: Supports standard `try-catch-finally` blocks and cooperative cancellation using `CancellationTokenSource`.

---

### E. Assembly definitions (.asmdef) vs. Assembly-CSharp.dll

#### The Common Way Problems:
1. **Long compile times**: Every time you change one comment in a script, Unity has to compile the entire project, leading to 10-30 second wait times on larger projects.
2. **Spaghetti Dependencies**: Any script in the project can access any other script, leading to messy cross-dependencies.

#### How Assemblies Fix It:
* **Blazing Fast Compile Times**: When you edit a script in `FitMe.Panel`, Unity only recompiles the panel assembly, which takes less than a second.
* **Forced Clean Architecture**: By defining what assemblies can reference what, you prevent developers from making architectural mistakes (e.g. preventing the save manager assembly from accessing UI scripts).

---

### F. ScriptableObject Configurations vs. GameObject Inspector Variables

#### The Common Way Problems:
1. **Accidental Loss of Data**: Storing values like movement speed, scoring scales, or prefabs directly on scene GameObjects makes them fragile. If a GameObject is deleted, or a component is reset by accident, all fine-tuned variables are lost.
2. **Git Merge Conflicts in Scenes/Prefabs**: When multiple developers adjust parameters on the same scene-level GameObject or prefab, it results in complex YAML conflicts in `.unity` or `.prefab` files, which are notoriously difficult to merge.
3. **No Support for Testing Profiles**: Swapping configurations for testing (e.g., trying out a fast-paced speed preset vs. a slow-paced one) requires duplicating entire scenes/prefabs, or manually modifying Inspector values back and forth.

#### How ScriptableObjects Fix It:
* **Isolated Configuration Assets**: Configuration lives in its own dedicated ScriptableObject asset file in the project folder. The scene/prefab only needs to resolve the asset at startup.
* **VContainer Registration & Injection**: The configuration asset is referenced in a `LifetimeScope` once and registered as an instance (`builder.RegisterInstance(config)`). The classes that need these settings receive them via constructor injection, eliminating scene lookup logic.
* **Easy Profile Hot-Swapping**: You can duplicate a configuration asset, change a few numbers (e.g., creating a `HardModeConfig` vs. `NormalModeConfig`), and easily swap which instance is referenced in the `LifetimeScope` to test it immediately.
* **Cleaner Git History**: Parameter updates only modify the specific `.asset` file, meaning zero noise or merge conflicts in scene or prefab files.

---

### G. Centralized Scene Transitions vs. Hardcoded Scene Loading

#### The Common Way Problems:
1. **Coupled Scene Loading**: Hardcoding `SceneManager.LoadScene("Gameplay")` in multiple buttons or game controllers makes scenes tightly coupled. If you change a scene path or name, references break.
2. **Scattered Transition Logic**: Setting up black screen fades or loading UI panels in every single scene creates massive code duplication and inconsistent visual styles.
3. **No Loading Lifecycle**: Checking if a scene has finished loading in an ad-hoc manner makes it difficult for other independent systems (like audio players or input managers) to prepare for the new scene.

#### How LoadSceneManager & ITransitionable Fix It:
* **Event-Driven Loading**: Systems only publish a simple `LoadSceneEvent`. The centralized `LoadSceneManager` listens to this event and manages the entire loading sequence.
* **Abstracted Transitions**: Visual transitions are separated from scene loading code. Any screen transition panel implements `ITransitionable`, allowing you to swap a simple fade effect with a complex screen pattern slide seamlessly.
* **Stage Lifecycles**: Throughout loading, the manager publishes stage events (`StartOut`, `FinishOut`, `StartLoading`, `FinishLoading`, `StartIn`, `FinishIn`) through VContainer, allowing independent modules to hook into specific loading milestones easily.

---

### H. UniTask Asynchronous State Machines vs. Update-loop Switch Statements

#### The Common Way Problems:
1. **Update-loop Overhead**: Writing a large switch-statement (`switch(currentState)`) inside `Update()` means checking conditions on every single frame, even when no state changes are happening.
2. **Blocking Transitions**: Implementing transitions that require waiting for animations or popups is extremely difficult in standard switch architectures, often leading to messy coroutine timing checks.
3. **Lack of Navigation History**: Standard state machines have no built-in knowledge of previous states, making it very tedious to build step-by-step game elements (like tutorials) where you might need to go backwards or skip steps.

#### How ExtendedStateMachine Fixes It:
* **UniTask Integration**: State entries and exits are asynchronous (`async UniTask Enter()`, `async UniTask Exit()`). This allows states to wait for animations, fades, or user interactions before completing transitions, keeping timing code clean.
* **Linear & History Navigation**: Supports linear movements (`Next()`, `Previous()`) and state history backtracking (`Revert()`) natively, making it perfect for UI wizards, tutorials, or narrative sequences.
* **Odin Inspector Visualization**: Integrates with Odin to display registered states, index, and the current active state in real-time in the Unity Editor, facilitating debugging.

---

### I. MessagePack Save Objects & BFS Migrations vs. PlayerPrefs / Manual Serialization

#### The Common Way Problems:
1. **Fragile Data Schemas**: When you update your game and change saving classes (adding/removing variables), old player save files break or corrupt on loading.
2. **Slow, Bulky Formats**: Using standard `PlayerPrefs` is limited to primitives, and manual JSON serialization results in large, unoptimized text files.
3. **Poor Polymorphism**: Serializing complex polymorphic types (like interface lists) in Unity's standard systems requires writing custom serialization converters, which are prone to bugs.

#### How MessagePackSaveObject & BFS Migrations Fix It:
* **Binary Serialization with Odin**: Combines Odin's serialization power (allowing interface and polymorphic fields in ScriptableObjects) with fast MessagePack binary resolvers for clean, high-performance saving.
* **BFS Migration Resolvers**: Implement `ISaveMigrationResolver<T>` to describe version transformations. The system parses version numbers, builds a graph of available resolvers, finds the shortest path of migration steps using a Breadth-First Search (BFS) traversal, and applies transformations on dynamic `ExpandoObject`s automatically.

---

## 3. How to pitch this to other programmers:
* **"Write code, not Unity-glue"**: Explain that this stack lets you write pure C# logic that doesn't care about Unity's scene hierarchy, making it clean, modular, and fast.
* **"No more scene conflict headaches"**: Moving configuration parameters to ScriptableObjects injected via VContainer keeps scene and prefab files untouched, virtually eliminating Git merge conflicts.
* **"Seamless save data migrations"**: Explain how schema migration resolvers automatically upgrade old players' save files to the newest structure, avoiding data corruption and versioning bugs.
* **"No more memory leaks"**: Emphasize how `AddTo()` and scoped lifetimes completely remove the paranoia of dangling event listeners.
* **"Zero GC Stutter"**: Highlight that replacing coroutines with `UniTask` and `R3` eliminates the random frame-rate drops that plague mobile game releases.
