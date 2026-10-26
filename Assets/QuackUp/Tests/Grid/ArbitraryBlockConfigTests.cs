using System.Reflection;
using FitMe.Shared;
using NUnit.Framework;
using QuackUp.Utils;
using R3;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

namespace FitMe.Grid.Tests
{
    public class ArbitraryBlockConfigTests
    {
        private const string ArbitraryConfigPath =
            "Assets/QuackUp/Settings/Grid/Block/BlockConfig/ArbitraryBlockConfig.asset";

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
        public void ArbitraryAtom_UseAtomSpriteDisablesSpineAndEnablesPlaceholder()
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
