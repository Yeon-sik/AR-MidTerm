using System;
using System.IO;
using System.Reflection;
using MidtermAR.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace MidtermAR.Tests
{
    public sealed class UiP0ReviewTests
    {
        [Test]
        public void VideoLineWidthConstantsMatchTheRequestedStartingValues()
        {
            Assert.That(ProjectBuilder.OutlineWidthMetres, Is.EqualTo(.0025f));
            Assert.That(ProjectBuilder.ConnectorWidthMetres, Is.EqualTo(.002f));
        }

        [Test]
        public void GeneratedSceneConnectsTwoRoundedLinesAndKeepsOutlineCentersOnCropBoundary()
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var presenter = Object.FindFirstObjectByType<TrackedImageProductPanel>();
            var outline = Field<LineRenderer>(presenter, "cropOutline");
            var connector = Field<LineRenderer>(presenter, "connector");
            Assert.That(outline, Is.Not.Null);
            Assert.That(connector, Is.Not.Null.And.Not.EqualTo(outline));
            Assert.That(presenter.GetComponentsInChildren<LineRenderer>(true).Length, Is.EqualTo(2));
            foreach (var line in new[] { outline, connector })
            {
                var width = line == outline ? ProjectBuilder.OutlineWidthMetres : ProjectBuilder.ConnectorWidthMetres;
                Assert.That(line.startWidth, Is.EqualTo(width).Within(.000001f));
                Assert.That(line.endWidth, Is.EqualTo(width).Within(.000001f));
                Assert.That(line.alignment, Is.EqualTo(LineAlignment.View));
                Assert.That(line.numCornerVertices, Is.EqualTo(4));
                Assert.That(line.numCapVertices, Is.EqualTo(line == outline ? 0 : 4));
                Assert.That(line.sharedMaterial, Is.Not.Null);
                Assert.That(line.useWorldSpace, Is.True);
                Assert.That(line.enabled, Is.False);
            }
            Assert.That(outline.loop, Is.True);
            Assert.That(connector.loop, Is.False);
            Assert.That(outline.positionCount, Is.EqualTo(4));
            Assert.That(connector.positionCount, Is.EqualTo(2));

            var products = new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
            var images = new ProductImageCatalog(File.ReadAllText("Assets/Data/product-images.json"), products);
            var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(ProjectBuilder.ImageLibraryPath);
            var pose = new GameObject("Synthetic outline pose");
            try
            {
                pose.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(20, 10, -15));
                var testedCrops = 0;
                for (var index = 0; index < library.count; index++)
                {
                    var crop = library[index];
                    Assert.That(images.TryFindByImage(crop.name, out var binding), Is.True);
                    if (binding.developmentOnly) continue;
                    Assert.That(products.TryFindById(binding.catalogProductId, out var product), Is.True);
                    presenter.SetProduct(product, binding);
                    Assert.That(presenter.TryApplyPose(pose.transform, crop.size), Is.True);
                    for (var corner = 0; corner < outline.positionCount; corner++)
                    {
                        var local = pose.transform.InverseTransformPoint(outline.GetPosition(corner));
                        Assert.That(Mathf.Abs(local.x), Is.EqualTo(crop.size.x * .5f).Within(.000001f));
                        Assert.That(Mathf.Abs(local.z), Is.EqualTo(crop.size.y * .5f).Within(.000001f));
                        Assert.That(local.y, Is.EqualTo(.001f).Within(.000001f));
                    }
                    // 정면 기하의 경계 안쪽 침범은 선 폭의 절반(1.25mm)이다.
                    // 인쇄 가림·실제 영상 픽셀 폭의 합격 판정은 실물 시험에서 한다.
                    var inwardBorder = outline.startWidth * .5f;
                    Assert.That(inwardBorder, Is.EqualTo(.00125f).Within(.000001f));
                    Assert.That(inwardBorder * 2 / crop.size.x, Is.LessThan(.05f));
                    Assert.That(inwardBorder * 2 / crop.size.y, Is.LessThan(.05f));
                    testedCrops++;
                }
                Assert.That(testedCrops, Is.EqualTo(2));
            }
            finally { DestroyPanel(presenter); Object.DestroyImmediate(pose); }
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void MissingLineReferencesAllowPresentationPoseAndHideWithoutException(bool missingOutline, bool missingConnector)
        {
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var presenter = Object.FindFirstObjectByType<TrackedImageProductPanel>();
            var products = new ProductCatalog(File.ReadAllText("Assets/Data/demo-products.json"));
            var images = new ProductImageCatalog(File.ReadAllText("Assets/Data/product-images.json"), products);
            var state = new ProductImageState(products, images);
            var tracked = new GameObject("Synthetic tracked crop", typeof(ARTrackedImage)).GetComponent<ARTrackedImage>();
            try
            {
                var data = new XRTrackedImage(new TrackableId(7, 8), Guid.Empty, Pose.identity,
                    new Vector2(.058f, .058f), TrackingState.Tracking, IntPtr.Zero);
                typeof(ARTrackable<XRTrackedImage, ARTrackedImage>).GetProperty("sessionRelativeData", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tracked, data);
                Set(presenter, "state", state); Set(presenter, "selectedImage", tracked);
                if (missingOutline) Set(presenter, "cropOutline", null);
                if (missingConnector) Set(presenter, "connector", null);
                state.BeginFrame(true); state.ObserveImage("theanine-top", TrackingState.Tracking, 1); state.EndFrame();
                Assert.That(state.CanDisplay, Is.True);
                Assert.DoesNotThrow(() => presenter.Present(true));
                Assert.That(presenter.HasVisibleTarget, Is.True);
                if (!missingOutline) Assert.That(Field<LineRenderer>(presenter, "cropOutline").enabled, Is.True);
                if (!missingConnector) Assert.That(Field<LineRenderer>(presenter, "connector").enabled, Is.True);
                Assert.DoesNotThrow(() => presenter.Present(false));
                Assert.That(presenter.HasVisibleTarget, Is.False);
                if (!missingOutline) Assert.That(Field<LineRenderer>(presenter, "cropOutline").enabled, Is.False);
                if (!missingConnector) Assert.That(Field<LineRenderer>(presenter, "connector").enabled, Is.False);
                Assert.DoesNotThrow(presenter.InvalidatePose);
                Assert.DoesNotThrow(presenter.ResetSelection);
            }
            finally { DestroyPanel(presenter); Object.DestroyImmediate(tracked.gameObject); }
        }

        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void DestroyPanel(TrackedImageProductPanel presenter)
        {
            var panel = Field<WorldProductPanel>(presenter, "panel");
            if (panel != null) Object.DestroyImmediate(panel.gameObject);
        }
    }
}
