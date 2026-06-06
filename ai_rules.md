# AI Coding Rules & Guidelines

Welcome, AI agent! Before writing, refactoring, or reviewing any code in this project, you **MUST** read and adhere to the architectural decisions and coding guidelines defined in the reference documents below:

1. **[onboarding_doc.md](file:///mnt/ssd1/All%20projects/Unity/Fit-Me-2026/AI%20Artifacts/onboarding_doc.md)**: Details the coding standards, patterns, and framework usage (VContainer, R3, UniTask, MVVM).
2. **[architecture_comparison.md](file:///mnt/ssd1/All%20projects/Unity/Fit-Me-2026/AI%20Artifacts/architecture_comparison.md)**: Highlights how our modern stack differs from traditional Unity approaches and why we avoid standard Unity patterns (e.g. singletons, direct Coroutines, scene-level configurations).
3. **[agent_instructions.md](file:///mnt/ssd1/All%20projects/Unity/Fit-Me-2026/AI%20Artifacts/agent_instructions.md)**: Outlines git workflow, pre-commit rules, review-first confirmation protocols, and commit consolidation guidelines.
4. **[token_efficiency_guide.md](file:///mnt/ssd1/All%20projects/Unity/Fit-Me-2026/AI%20Artifacts/token_efficiency_guide.md)**: Outlines token optimization guidelines and layered context strategy to keep AI agents context-efficient.

---

## 🚀 Key Stack & Architectural Pillars

Always conform to the following technologies and architectural design patterns implemented in this codebase:

- **Dependency Injection**: Use **VContainer**. Avoid service locators (`GetComponent`, `FindObjectOfType`) or global Singletons. Inject dependencies via constructors or `[Inject]` in `LifetimeScope` contexts.
- **Reactive Programming**: Use **R3** (Rx for Unity) for data streams, event handling, and data binding. Do not use standard C# events or poll values in `Update()`. Ensure all subscriptions are disposed properly using `.AddTo(this)` or `DisposableBuilder`.
- **UI Architecture**: Follow a strict **MVVM (Model-View-ViewModel)** pattern.
  - Views ([PanelView](file:///mnt/ssd1/All%20projects/Unity/Fit-Me-2026/Assets/QuackUp/Scripts/Panel/Panel/PanelView.cs)) must only control visual presentation and binding logic.
  - ViewModels ([PanelViewModel](file:///mnt/ssd1/All%20projects/Unity/Fit-Me-2026/Assets/QuackUp/Scripts/Panel/Panel/PanelViewModel.cs)) maintain UI states/commands and must not reference Unity visual components directly.
- **Asynchronous Operations**: Use **UniTask** for all asynchronous logic. Do not write Unity `Coroutine`s. Always pass `CancellationToken`s and manage `CancellationTokenSource` lifecycles properly.
- **Config Management**: Use **ScriptableObjects** for gameplay and system parameters. Register them inside `LifetimeScope` and inject them, rather than storing state in scene GameObjects.
- **Centralized Scene Loading**: Use `LoadSceneManager` and `ITransitionable` instead of hardcoded `SceneManager.LoadScene` calls.
- **State Machines**: Use `ExtendedStateMachine` powered by UniTask for asynchronous transitions and back-tracking history.
- **Save System**: Use `MessagePackSaveObject` combined with BFS migration resolvers to upgrade save data schemas safely.

---

## 🛠️ General Instructions for AI Agents
- Maintain the codebase integrity: never introduce raw singletons, Coroutines, or manual update-loop polling.
- Always check the onboarding guide before implementing new features.
