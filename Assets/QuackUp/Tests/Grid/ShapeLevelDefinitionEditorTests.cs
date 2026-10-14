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

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Expected private method {methodName}.");
            method.Invoke(target, null);
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
