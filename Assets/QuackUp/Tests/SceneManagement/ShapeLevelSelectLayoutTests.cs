
using FitMe.Panel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QuackUp.SceneManagement.Tests
{
    public sealed class ShapeLevelSelectLayoutTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.DestroyImmediate(_root);
        }

        [Test]
        public void TwentyLevelsUseFourColumnsAndFiveRows()
        {
            var content = CreateLayout(1124, 20);
            for (var row = 0; row < 5; row++)
            {
                var y = ((RectTransform)content.GetChild(row * 4)).anchoredPosition.y;
                for (var column = 0; column < 4; column++)
                    Assert.That(((RectTransform)content.GetChild(row * 4 + column)).anchoredPosition.y,
                        Is.EqualTo(y).Within(0.01f));
                if (row > 0)
                    Assert.That(y, Is.LessThan(((RectTransform)content.GetChild((row - 1) * 4)).anchoredPosition.y));
            }
        }

        private RectTransform CreateLayout(float width, int count)
        {
            _root = new GameObject("Level List", typeof(RectTransform));
            var content = _root.GetComponent<RectTransform>();
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(width, 0);
            var layout = _root.AddComponent<ShapeLevelSelectLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 20, 20);
            _root.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            for (var i = 0; i < count; i++)
                new GameObject("Level " + (i + 1), typeof(RectTransform)).transform.SetParent(content, false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            return content;
        }
    }
}
