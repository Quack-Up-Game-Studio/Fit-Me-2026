using FitMe.Panel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QuackUp.SceneManagement.Tests
{
    public sealed class ShapeWorldSelectLayoutTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.DestroyImmediate(_root);
        }

        [Test]
        public void FiveWorldsUseTwoColumnsAndCenterTheLastWorld()
        {
            var content = CreateLayout(960, 5);

            var first = (RectTransform)content.GetChild(0);
            var second = (RectTransform)content.GetChild(1);
            var third = (RectTransform)content.GetChild(2);
            var fourth = (RectTransform)content.GetChild(3);
            var fifth = (RectTransform)content.GetChild(4);
            Assert.That(first.anchoredPosition.y, Is.EqualTo(second.anchoredPosition.y).Within(0.01f));
            Assert.That(third.anchoredPosition.y, Is.EqualTo(fourth.anchoredPosition.y).Within(0.01f));
            Assert.That(third.anchoredPosition.y, Is.LessThan(first.anchoredPosition.y));
            Assert.That(fifth.anchoredPosition.y, Is.LessThan(third.anchoredPosition.y));
            Assert.That(fifth.anchoredPosition.x, Is.EqualTo(content.rect.width * 0.5f).Within(0.01f));
        }

        private RectTransform CreateLayout(float width, int count)
        {
            _root = new GameObject("World List", typeof(RectTransform));
            var content = _root.GetComponent<RectTransform>();
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(width, 0);
            var layout = _root.AddComponent<ShapeWorldSelectLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 80, 80);
            var fitter = _root.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            for (var i = 0; i < count; i++)
            {
                var child = new GameObject("World " + (i + 1), typeof(RectTransform));
                child.transform.SetParent(content, false);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            return content;
        }

        [Test]
        public void TwoWorldsStayOnTheFirstRow()
        {
            var content = CreateLayout(960, 2);
            var first = (RectTransform)content.GetChild(0);
            var second = (RectTransform)content.GetChild(1);
            Assert.That(first.anchoredPosition.y, Is.EqualTo(second.anchoredPosition.y).Within(0.01f));
            Assert.That(first.anchoredPosition.x, Is.LessThan(second.anchoredPosition.x));
            Assert.That(first.anchoredPosition.x + second.anchoredPosition.x,
                Is.EqualTo(content.rect.width).Within(0.01f));
        }

        [Test]
        public void SingleWorldIsCentered()
        {
            var content = CreateLayout(960, 1);
            Assert.That(((RectTransform)content.GetChild(0)).anchoredPosition.x,
                Is.EqualTo(content.rect.width * 0.5f).Within(0.01f));
        }

        [Test]
        public void NarrowViewportScalesTheFixedSizeArtworkInsideTheContent()
        {
            var content = CreateLayout(320, 5);
            for (var i = 0; i < content.childCount; i++)
            {
                var child = (RectTransform)content.GetChild(i);
                var art = new GameObject("Fixed Artwork", typeof(RectTransform)).GetComponent<RectTransform>();
                art.SetParent(child, false);
                art.sizeDelta = new Vector2(400, 400);
                var corners = new Vector3[4];
                art.GetWorldCorners(corners);
                Assert.That(content.InverseTransformPoint(corners[0]).x,
                    Is.GreaterThanOrEqualTo(content.rect.xMin + 30 - 0.01f));
                Assert.That(content.InverseTransformPoint(corners[2]).x,
                    Is.LessThanOrEqualTo(content.rect.xMax - 30 + 0.01f));
                Assert.That(child.localScale.x, Is.LessThan(1));
            }
        }

        [Test]
        public void WideViewportEnlargesButtonsToFillBothColumnsWithoutOverflow()
        {
            var content = CreateLayout(1124, 5);
            var first = (RectTransform)content.GetChild(0);
            var second = (RectTransform)content.GetChild(1);
            Assert.That(first.localScale.x, Is.GreaterThan(1));
            var firstLeft = first.anchoredPosition.x - first.rect.width * first.pivot.x * first.localScale.x;
            var secondRight = second.anchoredPosition.x + second.rect.width * (1 - second.pivot.x) * second.localScale.x;
            Assert.That(firstLeft, Is.EqualTo(30).Within(0.01f));
            Assert.That(secondRight, Is.EqualTo(content.rect.width - 30).Within(0.01f));
        }

        [Test]
        public void ResizingViewportRecalculatesItemScaleAndContentHeight()
        {
            var content = CreateLayout(960, 5);
            var oldHeight = content.rect.height;
            var oldScale = content.GetChild(0).localScale.x;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 320);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Assert.That(content.rect.height, Is.LessThan(oldHeight));
            Assert.That(content.GetChild(0).localScale.x, Is.LessThan(oldScale));
            Assert.That(((RectTransform)content.GetChild(4)).anchoredPosition.x,
                Is.EqualTo(content.rect.width * 0.5f).Within(0.01f));
        }

        [Test]
        public void InactiveWorldDoesNotReserveAnEmptySlot()
        {
            var content = CreateLayout(960, 5);
            var oldHeight = content.rect.height;
            content.GetChild(1).gameObject.SetActive(false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Assert.That(((RectTransform)content.GetChild(3)).anchoredPosition.y,
                Is.EqualTo(((RectTransform)content.GetChild(4)).anchoredPosition.y).Within(0.01f));
            Assert.That(content.rect.height, Is.LessThan(oldHeight));
        }

        [Test]
        public void EmptyListHasOnlyPaddingAndNoPhantomRows()
        {
            var content = CreateLayout(960, 0);
            Assert.That(content.rect.height, Is.EqualTo(160).Within(0.01f));
        }

        [Test]
        public void OverflowCanScrollToTheLastWorldAndBackToTheTop()
        {
            var content = CreateLayout(640, 5);
            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(ScrollRect));
            var viewport = viewportObject.GetComponent<RectTransform>();
            viewport.sizeDelta = new Vector2(640, 400);
            content.SetParent(viewport, false);
            _root = viewportObject;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.sizeDelta = new Vector2(0, content.sizeDelta.y);
            content.anchoredPosition = Vector2.zero;
            var scroll = viewportObject.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Assert.That(content.rect.height, Is.GreaterThan(viewport.rect.height));

            scroll.verticalNormalizedPosition = 0;

            Assert.That(content.anchoredPosition.y, Is.GreaterThan(0));
            var corners = new Vector3[4];
            ((RectTransform)content.GetChild(4)).GetWorldCorners(corners);
            Assert.That(viewport.InverseTransformPoint(corners[0]).y,
                Is.GreaterThanOrEqualTo(viewport.rect.yMin - 0.01f));
            Assert.That(viewport.InverseTransformPoint(corners[2]).y,
                Is.LessThanOrEqualTo(viewport.rect.yMax + 0.01f));
            scroll.verticalNormalizedPosition = 1;
            Assert.That(content.anchoredPosition.y, Is.EqualTo(0).Within(0.01f));
        }
    }
}
