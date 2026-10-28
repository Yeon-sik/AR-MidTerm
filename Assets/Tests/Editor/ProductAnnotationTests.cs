using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MidtermAR.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace MidtermAR.Tests
{
    public sealed class ProductAnnotationTests
    {
        private const string Id = "18e83c5c-86a4-4e95-a131-411381789687";
        private static ProductCatalog Products() => new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
        private static ProductImageCatalog Images() => new ProductImageCatalog(File.ReadAllText("Assets/Data/product-images.json"), Products());
        private static ProductSnapshot Product() { Products().TryFindById(Id, out var p); return p; }

        [TestCase("x")]
        [TestCase("z")]
        [TestCase("kind")]
        [TestCase("side")]
        [TestCase("identity")]
        [TestCase("duplicate-id")]
        [TestCase("evidence")]
        [TestCase("height-evidence")]
        [TestCase("height-mismatch")]
        public void InvalidAnchorDefinitionsFailClosed(string problem)
        {
            var json = JObject.Parse(File.ReadAllText("Assets/Data/product-images.json"));
            var annotation = json["productAnnotations"][0];
            if (problem == "x") annotation["anchorXMetres"] = .03;
            if (problem == "z") annotation["anchorZMetres"] = .06;
            if (problem == "kind") annotation["kind"] = "unregistered";
            if (problem == "side") annotation["side"] = "right";
            if (problem == "identity") annotation["catalogProductId"] = "11111111-1111-1111-1111-111111111111";
            if (problem == "duplicate-id") json["productAnnotations"][1]["id"] = annotation["id"];
            if (problem == "evidence") annotation["pixelEvidence"] = "";
            if (problem == "height-evidence") json["images"][1]["productHeightSource"] = null;
            if (problem == "height-mismatch") json["images"][2]["productHeightMetres"] = .12;
            Assert.Throws<FormatException>(() => new ProductImageCatalog(json.ToString(), Products()));
        }

        [TestCase("theanine-top")]
        [TestCase("theanine-bottom")]
        public void LabelsSitOutsidePackageAndDoNotOverlapActualProjectedBasicPanelOrConnector(string imageName)
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var presenter = Object.FindFirstObjectByType<TrackedImageProductPanel>();
            var images = Images(); images.TryFindByImage(imageName, out var binding);
            var input = new GameObject("Synthetic image pose");
            try
            {
                input.transform.SetPositionAndRotation(new Vector3(.1f, .2f, .8f), Quaternion.Euler(20, 35, -12));
                presenter.SetProduct(Product(), binding);
                Assert.That(presenter.TryApplyPose(input.transform, new Vector2(.058f, .058f)), Is.True);
                var panel = (RectTransform)Field<WorldProductPanel>(presenter, "panel").transform;
                var worldCorners = new Vector3[4]; panel.GetWorldCorners(worldCorners);
                var origin = new Vector3(binding.offsetXMetres, 0, binding.offsetZMetres);
                var points = worldCorners.Select(p => input.transform.InverseTransformPoint(p) - origin).ToArray();
                var basic = Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.z), points.Max(p => p.x), points.Max(p => p.z));
                var package = new Rect(-.029f, -.0582f, .058f, .1164f);
                var connector = new Rect(.029f, -.001f, .012f, .002f);
                foreach (var annotation in images.AnnotationsFor(Id))
                {
                    Assert.That(ProductAnnotationLayout.TryCreate(binding, annotation, basic, out var layout), Is.True);
                    Assert.That(layout.Bounds.Overlaps(package), Is.False);
                    Assert.That(layout.Bounds.Overlaps(basic), Is.False);
                    Assert.That(layout.Bounds.Overlaps(connector), Is.False);
                    Assert.That(layout.Anchor, Is.EqualTo(new Vector3(annotation.anchorXMetres, .001f, annotation.anchorZMetres)));
                    if (annotation.side == "left") Assert.That(layout.Bounds.xMax, Is.EqualTo(-.029f - .012f).Within(.000001f));
                    else Assert.That(layout.Bounds.yMax, Is.EqualTo(-.0582f - .012f).Within(.000001f));
                }
            }
            finally { DestroyPanel(presenter); Object.DestroyImmediate(input); }
        }

        [Test]
        public void CropSwitchPreservesCommonLabelPositionsAndExactPrintedAnchorEndpoints()
        {
            var root = PrefabUtility.LoadPrefabContents(ProjectBuilder.AnnotationPath);
            var input = new GameObject("Synthetic crop pose");
            try
            {
                var widget = root.GetComponent<WorldProductAnnotation>(); var images = Images();
                var annotation = images.AnnotationsFor(Id)[0];
                var common = new Vector3(.2f, .3f, .9f); var rotation = Quaternion.Euler(17, 42, 8);
                Vector3? previous = null;
                foreach (var imageName in new[] { "theanine-top", "theanine-bottom" })
                {
                    images.TryFindByImage(imageName, out var binding);
                    var offset = new Vector3(binding.offsetXMetres, 0, binding.offsetZMetres);
                    input.transform.SetPositionAndRotation(common - rotation * offset, rotation);
                    Assert.That(ProductAnnotationLayout.TryCreate(binding, annotation, new Rect(.041f, -.04f, .09f, .08f), out var layout), Is.True);
                    widget.SetContent(ProductInsightCalculator.AnnotationText(Product(), annotation.kind));
                    widget.ApplyPose(input.transform, binding, layout); widget.SetVisible(true);
                    var position = Field<RectTransform>(widget, "labelPanel").position;
                    if (previous.HasValue) Assert.That(Vector3.Distance(position, previous.Value), Is.LessThan(.000001f));
                    previous = position;
                    Assert.That(Vector3.Distance(Field<LineRenderer>(widget, "leader").GetPosition(0), common + rotation * layout.Anchor), Is.LessThan(.000001f));
                    Assert.That(Vector3.Distance(Field<LineRenderer>(widget, "leader").GetPosition(1), common + rotation * layout.LabelEdge), Is.LessThan(.000001f));
                }
            }
            finally { Object.DestroyImmediate(input); PrefabUtility.UnloadPrefabContents(root); }
        }

        [TestCase("lost")]
        [TestCase("limited")]
        [TestCase("last-known")]
        [TestCase("not-ready")]
        [TestCase("conflict")]
        [TestCase("denied")]
        [TestCase("pause")]
        [TestCase("rescan")]
        public void BothAnnotationsShareEveryBasicPanelHideGate(string reason)
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var presenter = Object.FindFirstObjectByType<TrackedImageProductPanel>();
            // 보류한 주석의 독립 회귀 시험에서만 명시적으로 켠다. 제출 씬의 기본값은 꺼짐이다.
            Set(presenter, "showAnnotations", true);
            var products = Products(); var images = new ProductImageCatalog(File.ReadAllText("Assets/Data/product-images.json"), products);
            presenter.Configure(products, images);
            var state = Field<ProductImageState>(presenter, "state");
            var input = new GameObject("Synthetic tracked image", typeof(ARTrackedImage)).GetComponent<ARTrackedImage>();
            try
            {
                SetTrackedData(input); Set(presenter, "selectedImage", input);
                state.BeginFrame(true); state.ObserveImage("theanine-top", TrackingState.Tracking, 1); state.EndFrame(); presenter.Present(true);
                var annotations = Field<ProductAnnotationPresenter>(presenter, "annotations");
                Assert.That(annotations.Count, Is.EqualTo(2));
                Assert.That(annotations.GetComponentsInChildren<WorldProductAnnotation>(true).All(a => a.IsVisible), Is.True);
                if (reason == "pause") Call(presenter, "OnApplicationPause", true);
                else if (reason == "rescan") presenter.ResetSelection();
                else
                {
                    state.BeginFrame(reason != "not-ready");
                    if (reason != "lost") state.ObserveImage("theanine-top", reason == "limited" ? TrackingState.Limited : TrackingState.Tracking, reason == "last-known" ? 2 : 1);
                    if (reason == "conflict") state.ObserveImage("test-card", TrackingState.Tracking, 1);
                    state.EndFrame(); presenter.Present(reason != "denied");
                }
                Assert.That(presenter.HasVisibleTarget, Is.False);
                Assert.That(annotations.GetComponentsInChildren<WorldProductAnnotation>(true).Any(a => a.IsVisible), Is.False);
                if (reason == "rescan") Assert.That(annotations.Count, Is.Zero);
            }
            finally { DestroyPanel(presenter); Object.DestroyImmediate(input.gameObject); }
        }

        [TestCase(false, false, 2)]
        [TestCase(true, false, 1)]
        [TestCase(false, true, 0)]
        public void NullCalculationsDoNotCreateObjectsAndFlagOffKeepsBasicPanel(bool missingCount, bool flagOff, int expected)
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var presenter = Object.FindFirstObjectByType<TrackedImageProductPanel>();
            var products = Products(); products.TryFindById(Id, out var product);
            if (missingCount) { product.servingCount = null; product.servingUnit = product.servingCountSource = null; }
            presenter.Configure(products, new ProductImageCatalog(File.ReadAllText("Assets/Data/product-images.json"), products));
            Set(presenter, "showAnnotations", !flagOff);
            var state = Field<ProductImageState>(presenter, "state");
            var input = new GameObject("Synthetic tracked image", typeof(ARTrackedImage)).GetComponent<ARTrackedImage>();
            try
            {
                SetTrackedData(input); Set(presenter, "selectedImage", input);
                state.BeginFrame(true); state.ObserveImage("theanine-top", TrackingState.Tracking, 1); state.EndFrame(); presenter.Present(true);
                var annotations = Field<ProductAnnotationPresenter>(presenter, "annotations");
                Assert.That(annotations.Count, Is.EqualTo(expected));
                Assert.That(annotations.GetComponentsInChildren<WorldProductAnnotation>(true).Length, Is.EqualTo(expected));
                Assert.That(presenter.HasVisibleTarget, Is.True);
                Set(presenter, "showAnnotations", false); presenter.Present(true);
                Assert.That(annotations.Count, Is.Zero);
                Assert.That(presenter.HasVisibleTarget, Is.True);
            }
            finally { DestroyPanel(presenter); Object.DestroyImmediate(input.gameObject); }
        }

        [Test]
        public void AnnotationPrefabFitsRealCopyAndLeaderDoesNotInheritTinyCanvasScale()
        {
            var root = PrefabUtility.LoadPrefabContents(ProjectBuilder.AnnotationPath);
            var input = new GameObject("Synthetic pose");
            try
            {
                var widget = root.GetComponent<WorldProductAnnotation>(); var images = Images(); images.TryFindByImage("theanine-top", out var binding);
                foreach (var annotation in images.AnnotationsFor(Id))
                {
                    var text = ProductInsightCalculator.AnnotationText(Product(), annotation.kind); widget.SetContent(text);
                    Assert.That(text, Does.Not.Contain("최저가").And.Not.Contain("PX"));
                    Assert.That(ProductAnnotationLayout.TryCreate(binding, annotation, new Rect(.041f, -.04f, .09f, .08f), out var layout), Is.True);
                    widget.ApplyPose(input.transform, binding, layout); widget.SetVisible(true);
                    var label = Field<TMP_Text>(widget, "label"); label.ForceMeshUpdate(true, true);
                    foreach (var c in text.Where(c => !char.IsWhiteSpace(c))) Assert.That(label.font.HasCharacter(c, true, true), Is.True, "Missing glyph " + c);
                    Assert.That(label.isTextOverflowing, Is.False, text);
                    Assert.That(label.fontSize * label.transform.lossyScale.x, Is.InRange(.004f, .00501f));
                    Assert.That(Field<LineRenderer>(widget, "leader").transform.lossyScale.x, Is.EqualTo(1).Within(.000001f));
                    Assert.That(Field<LineRenderer>(widget, "leader").startWidth, Is.EqualTo(ProjectBuilder.ConnectorWidthMetres));
                    Assert.That(Field<LineRenderer>(widget, "leader").numCapVertices, Is.Zero);
                }
                Assert.That(root.GetComponentsInChildren<Button>(true), Is.Empty);
            }
            finally { Object.DestroyImmediate(input); PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void SetTrackedData(ARTrackedImage input)
        {
            var data = new XRTrackedImage(new TrackableId(12, 13), Guid.Empty, Pose.identity, new Vector2(.058f, .058f), TrackingState.Tracking, IntPtr.Zero);
            typeof(ARTrackable<XRTrackedImage, ARTrackedImage>).GetProperty("sessionRelativeData", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(input, data);
        }
        private static void DestroyPanel(TrackedImageProductPanel presenter)
        { var panel = Field<WorldProductPanel>(presenter, "panel"); if (panel != null) Object.DestroyImmediate(panel.gameObject); }
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
