using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using NUnit.Framework;
using QuackUp.Utils;
using R3;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FitMe.Grid.Tests
{
    public class ArbitraryBlockConfigTests
    {
        private const string ArbitraryConfigPath =
            "Assets/QuackUp/Settings/Grid/Block/BlockConfig/ArbitraryBlockConfig.asset";

        [Test]
        public void ShapeExplosionCommand_ProvidesCanonicalAbsoluteDeadline()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArbitraryBlockConfig>(ArbitraryConfigPath);
            Assert.That(config, Is.Not.Null);

            var originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            var model = new BlockModel(config, null);
            var viewModel = new BlockViewModel(model);
            ExplodeCommandData receivedData = default;
            var subscription = viewModel.ExplodeCommand.Subscribe(data => receivedData = data);
            try
            {
                var completion = new ChildExplosionAnimationCompletion(0);
                var executeExplosion = typeof(BlockViewModel).GetMethod("ExecuteExplosion",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(executeExplosion, Is.Not.Null);
                executeExplosion.Invoke(viewModel, new object[]
                {
                    new ExplodeCommandData(new Promise<Unit>(), FitType.FitMe, false, completion)
                });

                var startField = typeof(ExplodeCommandData).GetField("ExplosionStartTime");
                var deadlineField = typeof(ExplodeCommandData).GetField("ExplosionDeadline");
                Assert.That(startField, Is.Not.Null);
                Assert.That(deadlineField, Is.Not.Null);
                var startTime = (double)startField.GetValue(receivedData);
                var deadline = (double)deadlineField.GetValue(receivedData);
                Assert.That(deadline, Is.GreaterThan(startTime));
                Assert.That(deadline - startTime, Is.LessThanOrEqualTo(5d));

                Assert.That(config.TryGetAnimation(ArbitraryBlockAnimation.Explode, out var animationName), Is.True);
                var expectedDuration = 0f;
                foreach (var skeletonDataAsset in config.SkeletonDataAssets.Values)
                {
                    var animation = skeletonDataAsset.GetSkeletonData(true)?.FindAnimation(animationName);
                    if (animation != null) expectedDuration = Mathf.Max(expectedDuration, animation.Duration);
                }
                Assert.That(deadline - startTime, Is.EqualTo(Mathf.Min(expectedDuration / 2f, 5f)).Within(0.02f));
            }
            finally
            {
                subscription.Dispose();
                viewModel.Dispose();
                viewModel.BlockInteractionState.Dispose();
                viewModel.SetSortingLayerCommand.Dispose();
                viewModel.SetSortingOrderCommand.Dispose();
                viewModel.ExplodeCommand.Dispose();
                viewModel.ScaleInCommand.Dispose();
                viewModel.RotateCommand.Dispose();
                viewModel.DestroyCommand.Dispose();
                model.BlockState.Dispose();
                model.BlockColor.Dispose();
                model.UpdateGridCommand.Dispose();
                Time.timeScale = originalTimeScale;
            }
        }

        [Test]
        public void ShapeModeConfig_IsDedicatedFromClassicBlockConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ArbitraryConfigPath);
            var managerConfig = AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(
                "Assets/QuackUp/Settings/Grid/Block/BlockManagerConfig.asset");

            Assert.That(config, Is.Not.Null, "Shape Mode must use its dedicated ArbitraryBlockConfig asset.");
            Assert.That(config.GetType().Name, Is.EqualTo("ArbitraryBlockConfig"));
            Assert.That(config, Is.Not.InstanceOf<BlockConfig>(),
                "Shape Mode config must not inherit the Classic BlockConfig or share its serialized Spine settings.");
            Assert.That(managerConfig, Is.Not.Null);
            Assert.That(managerConfig.ArbitraryBlockConfig, Is.SameAs(config));
        }

        [Test]
        public void ShapeModeConfig_ContainsIndependentPerAtomSpineData()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArbitraryBlockConfig>(ArbitraryConfigPath);
            var classicConfig = AssetDatabase.LoadAssetAtPath<BlockConfig>(
                "Assets/QuackUp/Settings/Grid/Block/BlockConfig/1x1.asset");
            var legacyShapeConfig = AssetDatabase.LoadAssetAtPath<BlockConfig>(
                "Assets/QuackUp/Settings/Grid/Block/BlockConfig/ShapeModeBlockConfig.asset");

            Assert.That(config, Is.Not.Null);
            Assert.That(config.UseAtomSprite, Is.False,
                "Shape Mode should use per-atom Spine by default; the placeholder sprite is opt-in.");
            Assert.That(classicConfig, Is.Not.Null);
            Assert.That(legacyShapeConfig, Is.Not.Null);
            Assert.That(config.SkeletonDataAssets.Keys,
                Is.EquivalentTo(new[] { ArbitraryBlockDesign.D1, ArbitraryBlockDesign.D2, ArbitraryBlockDesign.D3, ArbitraryBlockDesign.D4 }));
            Assert.That(config.SkeletonDataAssets.Values, Has.All.Not.Null);
            Assert.That(config.SkinDictionary.Keys,
                Is.EquivalentTo(new[] { BlockColor.Blue, BlockColor.Green, BlockColor.Purple, BlockColor.Red, BlockColor.Yellow }));
            Assert.That(config.AnimationDictionary.Keys,
                Is.EquivalentTo(new[] { ArbitraryBlockAnimation.Idle1, ArbitraryBlockAnimation.Idle2,
                    ArbitraryBlockAnimation.Drag, ArbitraryBlockAnimation.Drop, ArbitraryBlockAnimation.Explode }));
            foreach (var skeletonDataAsset in config.SkeletonDataAssets.Values)
            {
                var skeletonData = skeletonDataAsset.GetSkeletonData(true);
                Assert.That(skeletonData, Is.Not.Null);
                foreach (var skinName in config.SkinDictionary.Values)
                    Assert.That(skeletonData.FindSkin(skinName), Is.Not.Null, "Missing Spine skin " + skinName);
                foreach (var animationName in config.AnimationDictionary.Values)
                    Assert.That(skeletonData.FindAnimation(animationName), Is.Not.Null,
                        "Missing Spine animation " + animationName);
            }
            Assert.That(ReferenceEquals(config.SkinDictionary, classicConfig.SkinDictionary), Is.False);
            Assert.That(config.SkinDictionary, Is.EquivalentTo(legacyShapeConfig.SkinDictionary));
            Assert.That(config.ExplodeVfx.Keys, Is.EquivalentTo(legacyShapeConfig.ExplodeVfx.Keys));
            Assert.That(config.PlaceVFX.Keys, Is.EquivalentTo(legacyShapeConfig.PlaceVFX.Keys));
            Assert.That(config.ObstacleSprite, Is.SameAs(legacyShapeConfig.ObstacleSprite));
            Assert.That(config.ObstacleAtomSpriteColor, Is.EqualTo(legacyShapeConfig.ObstacleAtomSpriteColor));
        }

        [Test]
        public void AtomPrefab_UsesDedicatedSpineChildAndSerializedReferences()
        {
            const string atomPrefabPath = "Assets/QuackUp/Prefabs/Atom/AtomView.prefab";
            var atomView = AssetDatabase.LoadAssetAtPath<AtomView>(atomPrefabPath);

            Assert.That(atomView, Is.Not.Null);
            var placeholderSpriteRenderer = atomView.GetComponent<SpriteRenderer>();
            var skeleton = atomView.GetComponentInChildren<SkeletonAnimation>(true);
            var meshRenderer = atomView.GetComponentInChildren<MeshRenderer>(true);
            Assert.That(placeholderSpriteRenderer, Is.Not.Null);
            Assert.That(placeholderSpriteRenderer.enabled, Is.False,
                "The placeholder sprite must be disabled on the prefab itself.");
            Assert.That(placeholderSpriteRenderer.sharedMaterial.HasProperty("_RendererColor"), Is.True,
                "The outline shader must honor SpriteRenderer.color so the placeholder fill can be hidden without losing its outline.");
            Assert.That(skeleton, Is.Not.Null);
            Assert.That(skeleton, Is.InstanceOf<AtomSkeletonAnimation>());
            Assert.That(meshRenderer, Is.Not.Null);
            Assert.That(skeleton.enabled, Is.False, "The child Spine component should remain dormant until a Spine mode config is selected.");
            Assert.That(meshRenderer.enabled, Is.False);
            Assert.That(skeleton.updateWhenInvisible, Is.EqualTo(Spine.Unity.UpdateMode.OnlyAnimationStatus));
            Assert.That(skeleton.gameObject, Is.Not.SameAs(atomView.gameObject));
            Assert.That(meshRenderer.gameObject, Is.SameAs(skeleton.gameObject));
            Assert.That(typeof(AtomView).GetField("skeletonAnimation", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(atomView), Is.SameAs(skeleton));
            Assert.That(typeof(AtomView).GetField("spineMeshRenderer", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(atomView), Is.SameAs(meshRenderer));
        }

        [Test]
        public void ArbitraryAtom_UsesSharedColorAndOneShotDragDropAnimations()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArbitraryBlockConfig>(ArbitraryConfigPath);
            var managerConfig = AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(
                "Assets/QuackUp/Settings/Grid/Block/BlockManagerConfig.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<AtomView>("Assets/QuackUp/Prefabs/Atom/AtomView.prefab");
            var atomObject = Object.Instantiate(prefab.gameObject);
            var secondAtomObject = Object.Instantiate(prefab.gameObject);
            var blockObject = new GameObject("Arbitrary animation test block");
            var blockModel = new BlockModel(config, null);
            var blockViewModel = new BlockViewModel(blockModel);
            var block = new BlockInstance(blockModel, blockViewModel, null, blockObject);
            var atomModel = new AtomModel();
            var atomViewModel = new AtomViewModel(atomModel);
            var atomView = atomObject.GetComponent<AtomView>();
            var secondAtomModel = new AtomModel();
            var secondAtomViewModel = new AtomViewModel(secondAtomModel);
            var secondAtomView = secondAtomObject.GetComponent<AtomView>();

            try
            {
                blockModel.ChangeColor(BlockColor.Blue, false);
                atomView.Construct(managerConfig, atomViewModel);
                atomModel.ParentBlock.Value = block;
                secondAtomView.Construct(managerConfig, secondAtomViewModel);
                secondAtomModel.ParentBlock.Value = block;

                var skeletonAnimation = atomObject.GetComponentInChildren<SkeletonAnimation>(true);
                var atomSkeletonAnimation = atomObject.GetComponentInChildren<AtomSkeletonAnimation>(true);
                var secondSkeletonAnimation = secondAtomObject.GetComponentInChildren<SkeletonAnimation>(true);
                var spineRenderer = skeletonAnimation.GetComponent<MeshRenderer>();
                var spriteRenderer = atomObject.GetComponent<SpriteRenderer>();
                var designAssets = config.SkeletonDataAssets.Values;
                Assert.That(designAssets, Does.Contain(skeletonAnimation.skeletonDataAsset));
                Assert.That(designAssets, Does.Contain(secondSkeletonAnimation.skeletonDataAsset));
                var rotation = skeletonAnimation.transform.localEulerAngles.z;
                var nearestQuarterTurn = Mathf.Round(rotation / 90f) * 90f;
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(rotation, nearestQuarterTurn)), Is.LessThan(0.01f));
                Assert.That(skeletonAnimation.Skeleton.Skin.Name, Is.EqualTo(config.SkinDictionary[BlockColor.Blue]));
                Assert.That(secondSkeletonAnimation.Skeleton.Skin.Name,
                    Is.EqualTo(skeletonAnimation.Skeleton.Skin.Name), "Atoms in one block must keep the same color skin.");
                blockModel.ChangeColor(BlockColor.Yellow, false);
                Assert.That(skeletonAnimation.Skeleton.Skin.Name, Is.EqualTo(config.SkinDictionary[BlockColor.Yellow]));
                Assert.That(secondSkeletonAnimation.Skeleton.Skin.Name, Is.EqualTo(config.SkinDictionary[BlockColor.Yellow]));
                Assert.That(spineRenderer.enabled, Is.True);
                Assert.That(atomSkeletonAnimation, Is.Not.Null);
                Assert.That(atomSkeletonAnimation.PhysicsSimulationEnabled, Is.True);
                Assert.That(skeletonAnimation.enabled, Is.True);
                Assert.That(spriteRenderer.enabled, Is.True, "Keep the SpriteRenderer enabled for the atom outline over Spine.");
                Assert.That(spriteRenderer.color.a, Is.EqualTo(0f), "Hide the placeholder fill while Spine is active.");
                Assert.That(skeletonAnimation.AnimationState.GetCurrent(0).Animation.Name,
                    Is.EqualTo(config.AnimationDictionary[ArbitraryBlockAnimation.Idle1]));
                Assert.That(typeof(AtomView).GetField("_idleTimer", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(atomView), Is.Not.Null, "Idle2 should be scheduled immediately after Idle1 starts.");

                blockViewModel.BlockInteractionState.Value = BlockInteractionState.PickUp;
                Assert.That(atomSkeletonAnimation.PhysicsSimulationEnabled, Is.True,
                    "Per-atom Spine physics should remain active while the block is dragged.");
                Assert.That(atomSkeletonAnimation.PhysicsPositionInheritanceFactor, Is.EqualTo(Vector2.one));
                Assert.That(atomSkeletonAnimation.PhysicsRotationInheritanceFactor, Is.EqualTo(1f));
                var dragTrack = skeletonAnimation.AnimationState.GetCurrent(0);
                Assert.That(dragTrack.Animation.Name, Is.EqualTo(config.AnimationDictionary[ArbitraryBlockAnimation.Drag]));
                Assert.That(dragTrack.Loop, Is.False);
                Assert.That(secondSkeletonAnimation.AnimationState.GetCurrent(0).Animation.Name,
                    Is.EqualTo(config.AnimationDictionary[ArbitraryBlockAnimation.Drag]));

                blockViewModel.BlockInteractionState.Value = BlockInteractionState.PlacedOnSpawn;
                Assert.That(atomSkeletonAnimation.PhysicsSimulationEnabled, Is.True,
                    "Per-atom Spine physics should resume when dragging ends.");
                Assert.That(atomSkeletonAnimation.PhysicsPositionInheritanceFactor, Is.EqualTo(Vector2.one));
                Assert.That(atomSkeletonAnimation.PhysicsRotationInheritanceFactor, Is.EqualTo(1f));
                var dropTrack = skeletonAnimation.AnimationState.GetCurrent(0);
                Assert.That(dropTrack.Animation.Name, Is.EqualTo(config.AnimationDictionary[ArbitraryBlockAnimation.Drop]));
                Assert.That(dropTrack.Loop, Is.False);
                Assert.That(secondSkeletonAnimation.AnimationState.GetCurrent(0).Animation.Name,
                    Is.EqualTo(config.AnimationDictionary[ArbitraryBlockAnimation.Drop]));

                var playIdleMethod = typeof(AtomView).GetMethod("PlayArbitraryIdle", BindingFlags.Instance | BindingFlags.NonPublic);
                var startIdleTimerMethod = typeof(AtomView).GetMethod("StartIdleTimer", BindingFlags.Instance | BindingFlags.NonPublic);
                playIdleMethod?.Invoke(atomView, new object[] { 0 });
                startIdleTimerMethod?.Invoke(atomView, null);
                playIdleMethod?.Invoke(secondAtomView, new object[] { 0 });
                startIdleTimerMethod?.Invoke(secondAtomView, null);
                Assert.That(typeof(AtomView).GetField("_idleTimer", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(atomView), Is.Not.Null);
                blockModel.BlockState.Value = BlockState.Exploding;
                Assert.That(typeof(AtomView).GetField("_idleTimer", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(atomView), Is.Null, "Exploding atoms must not retain a timer that can replace Explode with Idle2.");
                blockViewModel.ExplodeCommand.Execute(
                    new ExplodeCommandData(new Promise<Unit>(), FitType.FitMe, false));
                var explodeTrack = skeletonAnimation.AnimationState.GetCurrent(0);
                Assert.That(explodeTrack.Animation.Name,
                    Is.EqualTo(config.AnimationDictionary[ArbitraryBlockAnimation.Explode]));
                Assert.That(explodeTrack.Loop, Is.False);
                Assert.That(explodeTrack.TimeScale, Is.EqualTo(2f));
                Assert.That(explodeTrack.MixingFrom, Is.Null, "Explode should not blend from an interrupted one-shot or idle track.");
                Assert.That(secondSkeletonAnimation.AnimationState.GetCurrent(0).Animation.Name,
                    Is.EqualTo(config.AnimationDictionary[ArbitraryBlockAnimation.Explode]));
            }
            finally
            {
                atomView.Dispose();
                secondAtomView.Dispose();
                blockViewModel.Dispose();
                atomModel.ParentBlock.Dispose();
                atomModel.ArrayIndex.Dispose();
                secondAtomModel.ParentBlock.Dispose();
                secondAtomModel.ArrayIndex.Dispose();
                blockModel.BlockState.Dispose();
                Object.DestroyImmediate(atomObject);
                Object.DestroyImmediate(secondAtomObject);
                Object.DestroyImmediate(blockObject);
            }
        }

        [Test]
        public void ShapeExplodeAnimations_MatchBlockExplosionDuration()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArbitraryBlockConfig>(ArbitraryConfigPath);
            Assert.That(config, Is.Not.Null);

            var blockExplosion = config.SkeletonDataAsset.GetSkeletonData(true)
                .FindAnimation(config.ExplodeAnimation);
            Assert.That(blockExplosion, Is.Not.Null);

            foreach (var pair in config.SkeletonDataAssets)
            {
                var atomExplosion = pair.Value.GetSkeletonData(true)
                    .FindAnimation(config.AnimationDictionary[ArbitraryBlockAnimation.Explode]);
                Assert.That(atomExplosion, Is.Not.Null, $"Design {pair.Key} must provide an Explode clip.");
                Assert.That(atomExplosion.Duration, Is.EqualTo(blockExplosion.Duration).Within(0.001f),
                    $"Design {pair.Key} must preserve the current block-clear duration.");
            }
        }

        [Test]
        public void ShapeExplosionDuration_UsesChildDesignsWhenDefaultAssetIsMissing()
        {
            var sourceConfig = AssetDatabase.LoadAssetAtPath<ArbitraryBlockConfig>(ArbitraryConfigPath);
            var config = Object.Instantiate(sourceConfig);
            var assetsField = typeof(ArbitraryBlockConfig).GetField("_skeletonDataAssets",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var assets = (Dictionary<ArbitraryBlockDesign, SkeletonDataAsset>)assetsField?.GetValue(config);
            Assert.That(assets, Is.Not.Null);

            Assert.That(config.TryGetAnimation(ArbitraryBlockAnimation.Explode, out var animationName), Is.True);
            var expectedDuration = 0f;
            var validChildClipCount = 0;
            foreach (var (design, asset) in assets)
            {
                if (design == ArbitraryBlockDesign.D1 || !asset) continue;
                var childAnimation = asset.GetSkeletonData(true)?.FindAnimation(animationName);
                if (childAnimation == null || childAnimation.Duration <= 0f) continue;
                expectedDuration = Mathf.Max(expectedDuration, childAnimation.Duration);
                validChildClipCount++;
            }
            Assert.That(validChildClipCount, Is.GreaterThan(0));
            Assert.That(expectedDuration, Is.GreaterThan(0f));
            assets.Remove(ArbitraryBlockDesign.D1);
            Assert.That(sourceConfig.SkeletonDataAssets.ContainsKey(ArbitraryBlockDesign.D1), Is.True,
                "Removing D1 from the clone must not mutate the source asset.");

            try
            {
                var timingType = typeof(BlockViewModel).Assembly.GetType("FitMe.Grid.ShapeExplosionTiming");
                Assert.That(timingType, Is.Not.Null);
                var getDuration = timingType.GetMethod("GetAnimationDuration",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(getDuration, Is.Not.Null);
                Assert.That((float)getDuration.Invoke(null, new object[] { config }),
                    Is.EqualTo(expectedDuration).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ChildExplosionAnimationCompletion_EmptyChildListCompletesWithoutAnimation()
        {
            var completion = new ChildExplosionAnimationCompletion(0);

            Assert.That(completion.Task.GetAwaiter().IsCompleted, Is.True);
            Assert.That(completion.Task.GetAwaiter().GetResult(), Is.False);
        }

        [Test]
        public void ChildExplosionAnimationCompletion_WaitsForEveryDistinctChildAndPropagatesFailure()
        {
            var completion = new ChildExplosionAnimationCompletion(2);

            completion.Report(10, true);
            completion.Report(10, true);
            Assert.That(completion.Task.GetAwaiter().IsCompleted, Is.False);

            completion.Report(11, false);
            Assert.That(completion.Task.GetAwaiter().IsCompleted, Is.True);
            Assert.That(completion.Task.GetAwaiter().GetResult(), Is.False,
                "A failed or interrupted child requires the block's duration-preserving fallback.");
        }

        [Test]
        public async Task ChildExplosionAnimationWait_TimesOutAndCancelsPendingAnimationWait()
        {
            var cancellationRequested = false;
            var animationCompleted = await ChildExplosionAnimationWait.WaitForAnimationOrTimeout(
                cancellationToken =>
                {
                    cancellationToken.Register(() => cancellationRequested = true);
                    return UniTask.WaitUntil(() => false, cancellationToken: cancellationToken)
                        .ContinueWith(() => true);
                },
                System.TimeSpan.Zero,
                CancellationToken.None);

            Assert.That(animationCompleted, Is.False);
            Assert.That(cancellationRequested, Is.True);
        }

        [Test]
        public async Task ShapeExplosionFallback_UsesCanonicalChildRateAndWaitsOnlyTheRemainingTime()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArbitraryBlockConfig>(ArbitraryConfigPath);
            var managerConfig = AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(
                "Assets/QuackUp/Settings/Grid/Block/BlockManagerConfig.asset");
            var gridConfig = AssetDatabase.LoadAssetAtPath<GridManagerConfig>(
                "Assets/QuackUp/Settings/Grid/GridManagerConfig.asset");
            var blockPrefab = AssetDatabase.LoadAssetAtPath<BlockView>("Assets/QuackUp/Prefabs/Block/BlockView.prefab");
            Assert.That(config, Is.Not.Null);
            Assert.That(managerConfig, Is.Not.Null);
            Assert.That(gridConfig, Is.Not.Null);
            Assert.That(blockPrefab, Is.Not.Null);

            var blockObject = Object.Instantiate(blockPrefab.gameObject);
            var blockModel = new BlockModel(config, null);
            var blockViewModel = new BlockViewModel(blockModel);
            var blockView = blockObject.GetComponent<BlockView>();
            var originalGlobalTimeScale = Time.timeScale;
            SkeletonAnimation skeletonAnimation = null;
            var originalSkeletonTimeScale = 1f;
            var originalStateTimeScale = 1f;
            try
            {
                blockView.Construct(config, managerConfig, gridConfig, null, blockViewModel, null);
                skeletonAnimation = (SkeletonAnimation)typeof(BlockView)
                    .GetField("skeletonAnimation", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(blockView);
                Assert.That(skeletonAnimation, Is.Not.Null);
                var explodeAnimation = config.SkeletonDataAsset.GetSkeletonData(true)
                    .FindAnimation(config.ExplodeAnimation);
                const float speedMultiplier = 2f;
                originalSkeletonTimeScale = skeletonAnimation.timeScale;
                originalStateTimeScale = skeletonAnimation.AnimationState.TimeScale;
                Time.timeScale = 0.5f;
                skeletonAnimation.timeScale = 1.25f;
                skeletonAnimation.AnimationState.TimeScale = 1.6f;
                Assert.That(skeletonAnimation.timeScale * skeletonAnimation.AnimationState.TimeScale,
                    Is.Not.EqualTo(1f), "The hidden block rig deliberately differs from the canonical child rate.");
                var effectiveTimeScale = speedMultiplier * Time.timeScale;
                Assert.That(effectiveTimeScale, Is.GreaterThan(0f));
                var expectedDuration = explodeAnimation.Duration / effectiveTimeScale;
                Assert.That(expectedDuration, Is.GreaterThan(0f));
                var explosionStartTime = Time.realtimeSinceStartupAsDouble - expectedDuration * 0.5d;
                var wallClockStart = Time.realtimeSinceStartupAsDouble;
                var waitMethod = typeof(BlockView).GetMethod("WaitForExplosionAnimationDuration",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(waitMethod, Is.Not.Null);
                await (UniTask)waitMethod.Invoke(blockView,
                    new object[] { speedMultiplier, explosionStartTime, CancellationToken.None });

                var elapsedWallClock = Time.realtimeSinceStartupAsDouble - wallClockStart;
                Assert.That(elapsedWallClock, Is.GreaterThan(expectedDuration * 0.25d),
                    "A child failure before the configured deadline must leave fallback time to wait.");
                Assert.That(elapsedWallClock, Is.LessThan(expectedDuration * 0.85d),
                    "Fallback must preserve the original deadline, not restart a full animation delay.");
            }
            finally
            {
                Time.timeScale = originalGlobalTimeScale;
                if (skeletonAnimation)
                {
                    skeletonAnimation.timeScale = originalSkeletonTimeScale;
                    skeletonAnimation.AnimationState.TimeScale = originalStateTimeScale;
                }
                blockView.Dispose();
                blockViewModel.Dispose();
                blockModel.BlockState.Dispose();
                Object.DestroyImmediate(blockObject);
            }
        }

        [Test]
        public async Task ShapeExplosionFallback_UsesSafeDurationWhenClipMetadataIsUnavailable()
        {
            var sourceConfig = AssetDatabase.LoadAssetAtPath<ArbitraryBlockConfig>(ArbitraryConfigPath);
            var configWithoutClips = Object.Instantiate(sourceConfig);
            var assetsField = typeof(ArbitraryBlockConfig).GetField("_skeletonDataAssets",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var assets = (Dictionary<ArbitraryBlockDesign, SkeletonDataAsset>)assetsField?.GetValue(configWithoutClips);
            Assert.That(assets, Is.Not.Null);
            assets.Clear();

            var managerConfig = AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(
                "Assets/QuackUp/Settings/Grid/Block/BlockManagerConfig.asset");
            var gridConfig = AssetDatabase.LoadAssetAtPath<GridManagerConfig>(
                "Assets/QuackUp/Settings/Grid/GridManagerConfig.asset");
            var blockPrefab = AssetDatabase.LoadAssetAtPath<BlockView>("Assets/QuackUp/Prefabs/Block/BlockView.prefab");
            var blockObject = Object.Instantiate(blockPrefab.gameObject);
            var blockModel = new BlockModel(sourceConfig, null);
            var blockViewModel = new BlockViewModel(blockModel);
            var blockView = blockObject.GetComponent<BlockView>();
            try
            {
                blockView.Construct(sourceConfig, managerConfig, gridConfig, null, blockViewModel, null);
                typeof(BlockView).GetField("_blockConfig", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(blockView, configWithoutClips);
                Time.timeScale = 1f;
                const float speedMultiplier = 2f;
                var defaultDuration = 2f;
                var expectedDuration = defaultDuration / speedMultiplier;
                var explosionStartTime = Time.realtimeSinceStartupAsDouble - expectedDuration * 0.75d;
                var wallClockStart = Time.realtimeSinceStartupAsDouble;
                var waitMethod = typeof(BlockView).GetMethod("WaitForExplosionAnimationDuration",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(waitMethod, Is.Not.Null);
                await (UniTask)waitMethod.Invoke(blockView,
                    new object[] { speedMultiplier, explosionStartTime, CancellationToken.None });

                var elapsedWallClock = Time.realtimeSinceStartupAsDouble - wallClockStart;
                Assert.That(elapsedWallClock, Is.GreaterThan(0.1d),
                    "Missing authored clip metadata must not make the child-owned clear complete immediately.");
                Assert.That(elapsedWallClock, Is.LessThan(0.75d),
                    "The metadata fallback must still use the original explosion deadline.");
            }
            finally
            {
                blockView.Dispose();
                blockViewModel.Dispose();
                blockModel.BlockState.Dispose();
                Object.DestroyImmediate(blockObject);
                Object.DestroyImmediate(configWithoutClips);
            }
        }

        [Test]
        public async Task ShapeExplosionFallback_UsesNominalRateWhenAnimationTimeScaleIsZero()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArbitraryBlockConfig>(ArbitraryConfigPath);
            var managerConfig = AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(
                "Assets/QuackUp/Settings/Grid/Block/BlockManagerConfig.asset");
            var gridConfig = AssetDatabase.LoadAssetAtPath<GridManagerConfig>(
                "Assets/QuackUp/Settings/Grid/GridManagerConfig.asset");
            var blockPrefab = AssetDatabase.LoadAssetAtPath<BlockView>("Assets/QuackUp/Prefabs/Block/BlockView.prefab");
            Assert.That(config, Is.Not.Null);
            Assert.That(managerConfig, Is.Not.Null);
            Assert.That(gridConfig, Is.Not.Null);
            Assert.That(blockPrefab, Is.Not.Null);

            var blockObject = Object.Instantiate(blockPrefab.gameObject);
            var blockModel = new BlockModel(config, null);
            var blockViewModel = new BlockViewModel(blockModel);
            var blockView = blockObject.GetComponent<BlockView>();
            var originalGlobalTimeScale = Time.timeScale;
            SkeletonAnimation skeletonAnimation = null;
            var originalSkeletonTimeScale = 1f;
            var originalStateTimeScale = 1f;
            try
            {
                blockView.Construct(config, managerConfig, gridConfig, null, blockViewModel, null);
                skeletonAnimation = (SkeletonAnimation)typeof(BlockView)
                    .GetField("skeletonAnimation", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(blockView);
                Assert.That(skeletonAnimation, Is.Not.Null);
                originalSkeletonTimeScale = skeletonAnimation.timeScale;
                originalStateTimeScale = skeletonAnimation.AnimationState.TimeScale;
                Time.timeScale = 0f;
                skeletonAnimation.timeScale = 0f;
                skeletonAnimation.AnimationState.TimeScale = 0f;
                const float speedMultiplier = 2f;
                var animation = config.SkeletonDataAsset.GetSkeletonData(true).FindAnimation(config.ExplodeAnimation);
                var expectedDuration = animation.Duration / speedMultiplier;
                var explosionStartTime = Time.realtimeSinceStartupAsDouble - expectedDuration * 0.75d;
                var wallClockStart = Time.realtimeSinceStartupAsDouble;
                var waitMethod = typeof(BlockView).GetMethod("WaitForExplosionAnimationDuration",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(waitMethod, Is.Not.Null);
                await (UniTask)waitMethod.Invoke(blockView,
                    new object[] { speedMultiplier, explosionStartTime, CancellationToken.None });

                var elapsedWallClock = Time.realtimeSinceStartupAsDouble - wallClockStart;
                Assert.That(elapsedWallClock, Is.GreaterThan(0.1d),
                    "Zero animation time scale must use a bounded nominal-rate fallback.");
                Assert.That(elapsedWallClock, Is.LessThan(0.75d),
                    "The fallback must wait only until the original explosion deadline.");
            }
            finally
            {
                Time.timeScale = originalGlobalTimeScale;
                if (skeletonAnimation)
                {
                    skeletonAnimation.timeScale = originalSkeletonTimeScale;
                    skeletonAnimation.AnimationState.TimeScale = originalStateTimeScale;
                }
                blockView.Dispose();
                blockViewModel.Dispose();
                blockModel.BlockState.Dispose();
                Object.DestroyImmediate(blockObject);
            }
        }

        [Test]
        public async Task ShapeExplosion_OnDestroyDuringFallbackSettlesRemovalPromise()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArbitraryBlockConfig>(ArbitraryConfigPath);
            var managerConfig = AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(
                "Assets/QuackUp/Settings/Grid/Block/BlockManagerConfig.asset");
            var gridConfig = AssetDatabase.LoadAssetAtPath<GridManagerConfig>(
                "Assets/QuackUp/Settings/Grid/GridManagerConfig.asset");
            var blockPrefab = AssetDatabase.LoadAssetAtPath<BlockView>("Assets/QuackUp/Prefabs/Block/BlockView.prefab");
            Assert.That(config, Is.Not.Null);
            Assert.That(managerConfig, Is.Not.Null);
            Assert.That(gridConfig, Is.Not.Null);
            Assert.That(blockPrefab, Is.Not.Null);

            var blockObject = Object.Instantiate(blockPrefab.gameObject);
            var blockModel = new BlockModel(config, null);
            var blockViewModel = new BlockViewModel(blockModel);
            var blockView = blockObject.GetComponent<BlockView>();
            var completion = new ChildExplosionAnimationCompletion(1);
            completion.Report(1, false);
            var promise = new Promise<Unit>();
            try
            {
                blockModel.ChangeColor(BlockColor.Blue, false);
                blockView.Construct(config, managerConfig, gridConfig, null, blockViewModel, null);
                blockViewModel.ExplodeCommand.Execute(new ExplodeCommandData(
                    promise, FitType.FitMe, false, completion));
                await UniTask.Yield();
                var activePromiseField = typeof(BlockView).GetField("_activeExplosionPromise",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(activePromiseField?.GetValue(blockView), Is.SameAs(promise),
                    "The explosion handler must be awaiting its fallback before destruction.");
                Assert.That(promise.Task.GetAwaiter().IsCompleted, Is.False,
                    "The fallback should still be pending before the parent is destroyed.");

                // Invoke the Unity callback directly; EditMode DestroyImmediate does not run it reliably.
                var onDestroy = typeof(BlockView).GetMethod("OnDestroy",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(onDestroy, Is.Not.Null);
                onDestroy.Invoke(blockView, null);
                Object.DestroyImmediate(blockObject);

                Assert.That(promise.Task.GetAwaiter().IsCompleted, Is.True,
                    "Block destruction must release GridManager's removal wait.");
            }
            finally
            {
                if (blockObject) Object.DestroyImmediate(blockObject);
                blockViewModel.Dispose();
                blockModel.BlockState.Dispose();
                promise.Dispose();
            }
        }

        [Test]
        public async Task ShapeExplosion_ChildBarrierThatNeverReportsUsesBoundedFallback()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArbitraryBlockConfig>(ArbitraryConfigPath);
            var managerConfig = AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(
                "Assets/QuackUp/Settings/Grid/Block/BlockManagerConfig.asset");
            var gridConfig = AssetDatabase.LoadAssetAtPath<GridManagerConfig>(
                "Assets/QuackUp/Settings/Grid/GridManagerConfig.asset");
            var blockPrefab = AssetDatabase.LoadAssetAtPath<BlockView>("Assets/QuackUp/Prefabs/Block/BlockView.prefab");
            Assert.That(config, Is.Not.Null);
            Assert.That(managerConfig, Is.Not.Null);
            Assert.That(gridConfig, Is.Not.Null);
            Assert.That(blockPrefab, Is.Not.Null);

            var blockObject = Object.Instantiate(blockPrefab.gameObject);
            var blockModel = new BlockModel(config, null);
            var blockViewModel = new BlockViewModel(blockModel);
            var blockView = blockObject.GetComponent<BlockView>();
            var completion = new ChildExplosionAnimationCompletion(1);
            var promise = new Promise<Unit>();
            var originalTimeScale = Time.timeScale;
            Task completionTask = null;
            try
            {
                blockModel.ChangeColor(BlockColor.Blue, false);
                blockView.Construct(config, managerConfig, gridConfig, null, blockViewModel, null);
                Time.timeScale = 1f;
                blockViewModel.ExplodeCommand.Execute(new ExplodeCommandData(
                    promise, FitType.FitMe, false, completion));
                completionTask = WaitForRemovalPromise();

                var winner = await Task.WhenAny(completionTask, Task.Delay(TimeSpan.FromSeconds(2.5d)));

                Assert.That(winner, Is.SameAs(completionTask),
                    "A child that disappears before reporting must not strand the parent removal promise.");
            }
            finally
            {
                completion.Report(1, false);
                blockView.Dispose();
                if (completionTask != null) await completionTask;
                Time.timeScale = originalTimeScale;
                blockViewModel.Dispose();
                blockModel.BlockState.Dispose();
                promise.Dispose();
                if (blockObject) Object.DestroyImmediate(blockObject);
            }

            async Task WaitForRemovalPromise()
            {
                await promise.Task;
            }
        }

        [Test]
        public void AtomExplosion_OnDisableRestoresSpineUpdateModesSynchronously()
        {
            var atomPrefab = AssetDatabase.LoadAssetAtPath<AtomView>("Assets/QuackUp/Prefabs/Atom/AtomView.prefab");
            Assert.That(atomPrefab, Is.Not.Null);

            var atomObject = Object.Instantiate(atomPrefab.gameObject);
            var atomView = atomObject.GetComponent<AtomView>();
            var skeleton = atomObject.GetComponentInChildren<AtomSkeletonAnimation>(true);
            Assert.That(skeleton, Is.Not.Null);
            try
            {
                skeleton.UpdateMode = Spine.Unity.UpdateMode.OnlyAnimationStatus;
                skeleton.updateWhenInvisible = Spine.Unity.UpdateMode.OnlyAnimationStatus;

                var beginOverride = typeof(AtomView).GetMethod("BeginExplosionUpdateModeOverride",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(beginOverride, Is.Not.Null);
                beginOverride.Invoke(atomView, null);
                Assert.That(skeleton.UpdateMode, Is.EqualTo(Spine.Unity.UpdateMode.OnlyEventTimelines));

                var onDisable = typeof(AtomView).GetMethod("OnDisable",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(onDisable, Is.Not.Null);
                onDisable.Invoke(atomView, null);

                Assert.That(skeleton.UpdateMode, Is.EqualTo(Spine.Unity.UpdateMode.OnlyAnimationStatus),
                    "Disabling an atom must restore its original Spine update mode immediately.");
                Assert.That(skeleton.updateWhenInvisible, Is.EqualTo(Spine.Unity.UpdateMode.OnlyAnimationStatus),
                    "Disabling an atom must restore its original invisible update mode immediately.");
            }
            finally
            {
                Object.DestroyImmediate(atomObject);
            }
        }

        [Test]
        public async Task ShapeExplosionFallback_CapsTotalWaitFromOriginalStart()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArbitraryBlockConfig>(ArbitraryConfigPath);
            var managerConfig = AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(
                "Assets/QuackUp/Settings/Grid/Block/BlockManagerConfig.asset");
            var gridConfig = AssetDatabase.LoadAssetAtPath<GridManagerConfig>(
                "Assets/QuackUp/Settings/Grid/GridManagerConfig.asset");
            var blockPrefab = AssetDatabase.LoadAssetAtPath<BlockView>("Assets/QuackUp/Prefabs/Block/BlockView.prefab");
            Assert.That(config, Is.Not.Null);
            Assert.That(managerConfig, Is.Not.Null);
            Assert.That(gridConfig, Is.Not.Null);
            Assert.That(blockPrefab, Is.Not.Null);

            var blockObject = Object.Instantiate(blockPrefab.gameObject);
            var blockModel = new BlockModel(config, null);
            var blockViewModel = new BlockViewModel(blockModel);
            var blockView = blockObject.GetComponent<BlockView>();
            var originalGlobalTimeScale = Time.timeScale;
            try
            {
                blockView.Construct(config, managerConfig, gridConfig, null, blockViewModel, null);
                Time.timeScale = 0.0001f;
                const float speedMultiplier = 2f;
                const double elapsedSinceExplosionStart = 4.9d;
                var explosionStartTime = Time.realtimeSinceStartupAsDouble - elapsedSinceExplosionStart;
                var wallClockStart = Time.realtimeSinceStartupAsDouble;
                var waitMethod = typeof(BlockView).GetMethod("WaitForExplosionAnimationDuration",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(waitMethod, Is.Not.Null);
                await (UniTask)waitMethod.Invoke(blockView,
                    new object[] { speedMultiplier, explosionStartTime, CancellationToken.None });

                var waitedAfterInvocation = Time.realtimeSinceStartupAsDouble - wallClockStart;
                Assert.That(waitedAfterInvocation, Is.GreaterThan(0.05d));
                Assert.That(waitedAfterInvocation, Is.LessThan(0.75d),
                    "The five-second cap applies to total time since explosion start, not only the remaining wait.");
            }
            finally
            {
                Time.timeScale = originalGlobalTimeScale;
                blockView.Dispose();
                blockViewModel.Dispose();
                blockModel.BlockState.Dispose();
                Object.DestroyImmediate(blockObject);
            }
        }

        [Test]
        public void ShapeExplosion_WaitsForAtomSpineCompletionWithoutEnablingBlockSpine()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArbitraryBlockConfig>(ArbitraryConfigPath);
            var managerConfig = AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(
                "Assets/QuackUp/Settings/Grid/Block/BlockManagerConfig.asset");
            var gridConfig = AssetDatabase.LoadAssetAtPath<GridManagerConfig>(
                "Assets/QuackUp/Settings/Grid/GridManagerConfig.asset");
            var blockPrefab = AssetDatabase.LoadAssetAtPath<BlockView>("Assets/QuackUp/Prefabs/Block/BlockView.prefab");
            var atomPrefab = AssetDatabase.LoadAssetAtPath<AtomView>("Assets/QuackUp/Prefabs/Atom/AtomView.prefab");
            Assert.That(config, Is.Not.Null);
            Assert.That(managerConfig, Is.Not.Null);
            Assert.That(gridConfig, Is.Not.Null);
            Assert.That(blockPrefab, Is.Not.Null);
            Assert.That(atomPrefab, Is.Not.Null);

            var blockObject = Object.Instantiate(blockPrefab.gameObject);
            var blockModel = new BlockModel(config, null);
            var blockViewModel = new BlockViewModel(blockModel);
            var blockInstance = new BlockInstance(blockModel, blockViewModel, null, blockObject);
            var blockView = blockObject.GetComponent<BlockView>();
            var atomObjects = new GameObject[2];
            var atomModels = new AtomModel[2];
            var atomViewModels = new AtomViewModel[2];
            var atomViews = new AtomView[2];
            var animationCompletion = new ChildExplosionAnimationCompletion(atomViews.Length);
            var promise = new Promise<Unit>();

            try
            {
                blockModel.ChangeColor(BlockColor.Blue, false);
                blockView.Construct(config, managerConfig, gridConfig, null, blockViewModel, null);
                for (var i = 0; i < atomViews.Length; i++)
                {
                    atomObjects[i] = Object.Instantiate(atomPrefab.gameObject, blockObject.transform);
                    atomModels[i] = new AtomModel();
                    atomViewModels[i] = new AtomViewModel(atomModels[i]);
                    atomViews[i] = atomObjects[i].GetComponent<AtomView>();
                    atomViews[i].Construct(managerConfig, atomViewModels[i]);
                    atomModels[i].ParentBlock.Value = blockInstance;
                }

                blockModel.BlockState.Value = BlockState.Exploding;
                blockViewModel.ExplodeCommand.Execute(new ExplodeCommandData(
                    promise, FitType.FitMe, false, animationCompletion));

                var blockSkeleton = (SkeletonAnimation)typeof(BlockView)
                    .GetField("skeletonAnimation", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(blockView);
                Assert.That(blockSkeleton.enabled, Is.False,
                    "Shape Mode must not reactivate the hidden block-level Spine rig during explosion.");

                var atomSkeletons = new AtomSkeletonAnimation[atomViews.Length];
                for (var i = 0; i < atomViews.Length; i++)
                {
                    atomSkeletons[i] = atomObjects[i].GetComponentInChildren<AtomSkeletonAnimation>(true);
                    Assert.That(atomSkeletons[i].AnimationState.GetCurrent(0).Animation.Name,
                        Is.EqualTo(config.AnimationDictionary[ArbitraryBlockAnimation.Explode]));
                    Assert.That(atomSkeletons[i].UpdateMode, Is.EqualTo(Spine.Unity.UpdateMode.OnlyEventTimelines),
                        "An invisible atom must retain completion-event dispatch while its explosion is awaited.");
                }

                Assert.That(animationCompletion.Task.GetAwaiter().IsCompleted, Is.False,
                    "The block barrier must remain pending until every child animation completes.");
                atomSkeletons[0].Update(1.1f);
                Assert.That(animationCompletion.Task.GetAwaiter().IsCompleted, Is.False,
                    "One completed atom must not release the block while another atom is still animating.");
                Assert.That(promise.Task.GetAwaiter().IsCompleted, Is.False);

                atomSkeletons[1].Update(1.1f);
                Assert.That(animationCompletion.Task.GetAwaiter().GetResult(), Is.True);
                Assert.That(promise.Task.GetAwaiter().IsCompleted, Is.True,
                    "Block removal must complete after the child-owned animation barrier.");
            }
            finally
            {
                foreach (var atomView in atomViews) atomView?.Dispose();
                blockView.Dispose();
                blockViewModel.Dispose();
                foreach (var atomModel in atomModels)
                {
                    atomModel?.ParentBlock.Dispose();
                    atomModel?.ArrayIndex.Dispose();
                }
                blockModel.BlockState.Dispose();
                promise.Dispose();
                Object.DestroyImmediate(blockObject);
            }
        }

        [Test]
        public void ClassicExplosion_ContinuesToUseBlockSpineCompletion()
        {
            var config = AssetDatabase.LoadAssetAtPath<BlockConfig>(
                "Assets/QuackUp/Settings/Grid/Block/BlockConfig/1x1.asset");
            var managerConfig = AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(
                "Assets/QuackUp/Settings/Grid/Block/BlockManagerConfig.asset");
            var gridConfig = AssetDatabase.LoadAssetAtPath<GridManagerConfig>(
                "Assets/QuackUp/Settings/Grid/GridManagerConfig.asset");
            var blockPrefab = AssetDatabase.LoadAssetAtPath<BlockView>("Assets/QuackUp/Prefabs/Block/BlockView.prefab");
            Assert.That(config, Is.Not.Null);
            Assert.That(managerConfig, Is.Not.Null);
            Assert.That(gridConfig, Is.Not.Null);
            Assert.That(blockPrefab, Is.Not.Null);

            var blockObject = Object.Instantiate(blockPrefab.gameObject);
            var blockModel = new BlockModel(config, null);
            var blockViewModel = new BlockViewModel(blockModel);
            var blockView = blockObject.GetComponent<BlockView>();
            var promise = new Promise<Unit>();

            try
            {
                blockModel.ChangeColor(BlockColor.Blue, false);
                blockView.Construct(config, managerConfig, gridConfig, null, blockViewModel, null);
                var blockSkeleton = (SkeletonAnimation)typeof(BlockView)
                    .GetField("skeletonAnimation", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(blockView);
                blockSkeleton.UpdateMode = Spine.Unity.UpdateMode.FullUpdate;

                blockModel.BlockState.Value = BlockState.Exploding;
                blockViewModel.ExplodeCommand.Execute(new ExplodeCommandData(promise, FitType.FitMe, false));

                var track = blockSkeleton.AnimationState.GetCurrent(0);
                Assert.That(blockSkeleton.enabled, Is.True);
                Assert.That(track.Animation.Name, Is.EqualTo(config.ExplodeAnimation));
                Assert.That(promise.Task.GetAwaiter().IsCompleted, Is.False);

                blockSkeleton.Update(track.Animation.Duration / track.TimeScale + 0.1f);
                Assert.That(promise.Task.GetAwaiter().IsCompleted, Is.True,
                    "Classic Mode must still resolve removal from the block-level Spine completion.");
            }
            finally
            {
                blockView.Dispose();
                blockViewModel.Dispose();
                blockModel.BlockState.Dispose();
                promise.Dispose();
                Object.DestroyImmediate(blockObject);
            }
        }

        [Test]
        public void ArbitraryBlockConfig_UseAtomSpriteDisablesSpineAndEnablesPlaceholder()
        {
            var sourceConfig = AssetDatabase.LoadAssetAtPath<ArbitraryBlockConfig>(ArbitraryConfigPath);
            var config = Object.Instantiate(sourceConfig);
            SetUseAtomSprite(config, true);
            var managerConfig = AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(
                "Assets/QuackUp/Settings/Grid/Block/BlockManagerConfig.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<AtomView>("Assets/QuackUp/Prefabs/Atom/AtomView.prefab");
            var atomObject = Object.Instantiate(prefab.gameObject);
            var blockObject = new GameObject("Sprite-mode arbitrary block");
            var blockModel = new BlockModel(config, null);
            var blockViewModel = new BlockViewModel(blockModel);
            var block = new BlockInstance(blockModel, blockViewModel, null, blockObject);
            var atomModel = new AtomModel();
            var atomViewModel = new AtomViewModel(atomModel);
            var atomView = atomObject.GetComponent<AtomView>();

            try
            {
                atomView.Construct(managerConfig, atomViewModel);
                atomModel.ParentBlock.Value = block;

                var skeletonAnimation = atomObject.GetComponentInChildren<SkeletonAnimation>(true);
                var spineRenderer = skeletonAnimation.GetComponent<MeshRenderer>();
                var spriteRenderer = atomObject.GetComponent<SpriteRenderer>();
                Assert.That(config.UseAtomSprite, Is.True);
                Assert.That(skeletonAnimation.enabled, Is.False);
                Assert.That(spineRenderer.enabled, Is.False);
                Assert.That(spriteRenderer.enabled, Is.True);
            }
            finally
            {
                atomView.Dispose();
                blockViewModel.Dispose();
                atomModel.ParentBlock.Dispose();
                atomModel.ArrayIndex.Dispose();
                Object.DestroyImmediate(atomObject);
                Object.DestroyImmediate(blockObject);
                Object.DestroyImmediate(config);
            }
        }

        private static void SetUseAtomSprite(ArbitraryBlockConfig config, bool enabled)
        {
            var setter = typeof(ArbitraryBlockConfig).GetProperty(nameof(ArbitraryBlockConfig.UseAtomSprite))
                ?.GetSetMethod(true);
            Assert.That(setter, Is.Not.Null, "UseAtomSprite must be a configurable property.");
            setter.Invoke(config, new object[] { enabled });
        }
    }
}
