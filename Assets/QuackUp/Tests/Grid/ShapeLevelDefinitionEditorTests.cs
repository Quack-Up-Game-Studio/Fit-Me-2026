using System.Reflection;
using FitMe.GameData;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FitMe.Grid.Tests
{
    public sealed class ShapeLevelDefinitionEditorTests
    {
        private ShapeLevelDefinition _level;
        private Editor _editor;

        [TearDown]
        public void TearDown()
        {
            if (_editor) Object.DestroyImmediate(_editor);
            if (_level) Object.DestroyImmediate(_level);
        }

        [Test]
        public void ValidateTiling_RefreshesPreviewAfterDefinitionEdit()
        {
            _level = ScriptableObject.CreateInstance<ShapeLevelDefinition>();
            _level.SetGeneratedContent(
                new[,] { { 1, 1 } },
                new[,] { { 0, 0 } },
                new[] { new ShapePlayerBlockEntry(new[,] { { 1 } }, 2) });
            _editor = Editor.CreateEditor(_level);

            InvokePrivate(_editor, "ValidateTiling");
            var firstResult = GetPrivateField<ShapeLevelTilingResult>(_editor, "_tilingResult");
            Assert.That(firstResult.Status, Is.EqualTo(ShapeLevelTilingStatus.Feasible));
            Assert.That(firstResult.Placements, Has.Count.EqualTo(2));

            _level.SetGeneratedContent(
                new[,] { { 1, 1 } },
                new[,] { { 0, 0 } },
                new[] { new ShapePlayerBlockEntry(new[,] { { 1, 1 } }, 1) });
            InvokePrivate(_editor, "RefreshDesignMetrics");

            Assert.That(GetPrivateField<ShapeLevelTilingResult>(_editor, "_tilingResult"), Is.Null);
            Assert.That(GetPrivateField<bool>(_editor, "_tilingNeedsValidation"), Is.True);

            InvokePrivate(_editor, "ValidateTiling");
            var refreshedResult = GetPrivateField<ShapeLevelTilingResult>(_editor, "_tilingResult");
            Assert.That(GetPrivateField<bool>(_editor, "_tilingNeedsValidation"), Is.False);
            Assert.That(refreshedResult.Status, Is.EqualTo(ShapeLevelTilingStatus.Feasible));
            Assert.That(refreshedResult.Placements, Has.Count.EqualTo(1));
            Assert.That(refreshedResult.Placements[0].BlockIndex, Is.Zero);
            Assert.That(refreshedResult.Placements[0].Cells, Has.Count.EqualTo(2));
            Assert.That(
                refreshedResult.Placements[0].Cells[0].x,
                Is.EqualTo(refreshedResult.Placements[0].Cells[1].x),
                "The preview must reflect the edited domino footprint.");
        }

        [Test]
        public void ValidateTiling_StoresMultipleSolutionsAndNavigationResetsAfterEdit()
        {
            _level = ScriptableObject.CreateInstance<ShapeLevelDefinition>();
            _level.SetGeneratedContent(
                new[,] { { 1, 1 }, { 1, 1 } },
                new[,] { { 0, 0 }, { 0, 0 } },
                new[] { new ShapePlayerBlockEntry(new[,] { { 1, 1 } }, 2) });
            _editor = Editor.CreateEditor(_level);

            InvokePrivate(_editor, "ValidateTiling");

            var result = GetPrivateField<ShapeLevelTilingResult>(_editor, "_tilingResult");
            Assert.That(result.SolutionCount, Is.EqualTo(2));
            Assert.That(result.Solutions, Has.Count.EqualTo(2));
            Assert.That(GetPrivateField<int>(_editor, "_selectedSolutionIndex"), Is.Zero);

            InvokePrivate(_editor, "StepSolution", 1);
            Assert.That(GetPrivateField<int>(_editor, "_selectedSolutionIndex"), Is.EqualTo(1));

            InvokePrivate(_editor, "RefreshDesignMetrics");
            Assert.That(GetPrivateField<int>(_editor, "_selectedSolutionIndex"), Is.Zero);
            Assert.That(GetPrivateField<ShapeLevelTilingResult>(_editor, "_tilingResult"), Is.Null);
        }

        [Test]
        public void ValidateTiling_InvalidatesCachedPreviewWhenDefinitionChangesExternally()
        {
            _level = ScriptableObject.CreateInstance<ShapeLevelDefinition>();
            _level.SetGeneratedContent(
                new[,] { { 1, 1 } },
                new[,] { { 0, 0 } },
                new[] { new ShapePlayerBlockEntry(new[,] { { 1 } }, 2) });
            _editor = Editor.CreateEditor(_level);
            InvokePrivate(_editor, "ValidateTiling");

            _level.SetGeneratedContent(
                new[,] { { 1, 0, 1 } },
                new[,] { { 0, 0, 0 } },
                new[] { new ShapePlayerBlockEntry(new[,] { { 1, 1 } }, 1) });
            InvokePrivate(_editor, "CheckDefinitionFingerprint");

            Assert.That(GetPrivateField<ShapeLevelTilingResult>(_editor, "_tilingResult"), Is.Null);
            Assert.That(GetPrivateField<ShapePlaysetDifficultyMetrics>(_editor, "_difficultyMetrics"), Is.Null);
            Assert.That(GetPrivateField<bool>(_editor, "_tilingNeedsValidation"), Is.True);
        }

        [Test]
        public void ValidateTiling_ShowsDifficultyMetricsOnlyForFeasibleResult()
        {
            _level = ScriptableObject.CreateInstance<ShapeLevelDefinition>();
            _level.SetGeneratedContent(
                new[,] { { 1, 1 } },
                new[,] { { 0, 0 } },
                new[] { new ShapePlayerBlockEntry(new[,] { { 1 } }, 2) });
            _editor = Editor.CreateEditor(_level);

            InvokePrivate(_editor, "ValidateTiling");

            var difficulty = GetPrivateField<ShapePlaysetDifficultyMetrics>(_editor, "_difficultyMetrics");
            Assert.That(difficulty, Is.Not.Null);
            Assert.That(difficulty.IsProvisional, Is.True);
            Assert.That(difficulty.PlayableCellCount, Is.EqualTo(2));
            Assert.That(difficulty.BlockCount, Is.EqualTo(2));
            Assert.That(difficulty.TotalLegalPlacements, Is.EqualTo(4));
            Assert.That(difficulty.ScoreModelVersion,
                Is.EqualTo(ShapePlaysetDifficultyEvaluator.CurrentScoreModelVersion));

            _level.SetGeneratedContent(
                new[,] { { 1, 0, 1 } },
                new[,] { { 0, 0, 0 } },
                new[] { new ShapePlayerBlockEntry(new[,] { { 1, 1 } }, 1) });
            InvokePrivate(_editor, "RefreshDesignMetrics");
            Assert.That(GetPrivateField<ShapePlaysetDifficultyMetrics>(_editor, "_difficultyMetrics"), Is.Null);

            InvokePrivate(_editor, "ValidateTiling");

            var result = GetPrivateField<ShapeLevelTilingResult>(_editor, "_tilingResult");
            Assert.That(result.Status, Is.EqualTo(ShapeLevelTilingStatus.Infeasible));
            Assert.That(GetPrivateField<ShapePlaysetDifficultyMetrics>(_editor, "_difficultyMetrics"), Is.Null);

            _level.SetGeneratedContent(
                new[,] { { 1, 1, 1, 1 } },
                new[,] { { 0, 0, 0, 0 } },
                new[] { new ShapePlayerBlockEntry(new[,] { { 1, 1 } }, 2) });
            InvokePrivate(_editor, "RefreshDesignMetrics");
            InvokePrivate(_editor, "ValidateTiling");

            result = GetPrivateField<ShapeLevelTilingResult>(_editor, "_tilingResult");
            var refreshedDifficulty = GetPrivateField<ShapePlaysetDifficultyMetrics>(_editor, "_difficultyMetrics");
            Assert.That(result.Status, Is.EqualTo(ShapeLevelTilingStatus.Feasible));
            Assert.That(refreshedDifficulty, Is.Not.Null);
            Assert.That(refreshedDifficulty.PlayableCellCount, Is.EqualTo(4));
            Assert.That(refreshedDifficulty.BlockCount, Is.EqualTo(2));
            Assert.That(refreshedDifficulty.TotalLegalPlacements, Is.EqualTo(6));
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Expected private method {methodName}.");
            method.Invoke(target, arguments);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected private field {fieldName}.");
            return (T)field.GetValue(target);
        }
    }
}
