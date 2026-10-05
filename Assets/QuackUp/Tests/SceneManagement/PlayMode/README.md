# Native scene-loading characterization

These PlayMode tests exercise the real `LoadSceneManager` and Unity scene loader using temporary empty Source/Destination scenes. They verify actual activation, source unload, cover/reveal completion, and the public `LoadScene` return boundary in Single and Additive modes. No gameplay scenes are loaded.

## Baseline contract

With the reveal deliberately held open:

- Single: StartOut -> FinishOut -> StartLoading -> **LoadSceneReturned** -> StartIn -> FinishLoading -> FinishIn.
- Additive: StartOut -> FinishOut -> StartLoading -> **LoadSceneReturned** -> FinishLoading -> StartIn -> FinishIn.

Single delivers StartIn before FinishLoading; Additive waits for source unload first. Keep this observed distinction when refactoring. Public return is the background-worker handoff, not full-switch completion.

## Fixture safety

Prebuild setup backs up the editor Build Settings and preloaded asset identities before creating test-owned empty scenes. It temporarily removes VContainerSettings from preloaded assets so game ads, analytics, IAP and audio services do not bootstrap.

Each test restores Build Settings in `finally`. Postbuild cleanup restores the original preloaded assets only after PlayMode ends, restores Build Settings, then removes the temporary fixture folder. Restoring preloaded assets between tests can bootstrap the game and must be avoided.

The fixture refuses an existing TemporaryScenes directory rather than overwriting it. If Unity is interrupted, inspect its BuildSettingsBackup.json before removing any fixtures or restoring settings. Runner roots survive Single loads; waits have 30-second assertion bounds. `CreateManager` is the isolated constructor seam for future backend injection.

## Running

Use Unity's PlayMode Test Runner or batchmode with `-runTests -testPlatform PlayMode -testFilter QuackUp.SceneManagement.PlayModeTests` and fresh XML/log paths. For this project's headless environment, use the installed Unity 6000.3.9f1 editor, Android build target and a short TMPDIR alias pointing into the agent profile's scratch directory.

Before and after running, check unrelated tracked assets/settings for import churn. The verified isolated baseline passed 2/2 native tests with no tracked content changes and no remaining temporary fixture assets. The existing EditMode baseline separately passed 24/24. These are baseline results, not proof of the planned cancellation implementation or a full gameplay smoke test.
