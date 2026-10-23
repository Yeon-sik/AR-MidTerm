using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MidtermAR.Editor;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ZXing;
using FormatException = System.FormatException;

namespace MidtermAR.Tests
{
    public sealed class ProductARTests
    {
        private static string Json => File.ReadAllText("Assets/Data/demo-products.json");

        [TestCase(ARSessionState.Ready, false)]
        [TestCase(ARSessionState.Unsupported, false)]
        [TestCase(ARSessionState.SessionInitializing, true)]
        [TestCase(ARSessionState.SessionTracking, true)]
        public void BarcodeCameraDoesNotWaitForWorldTracking(ARSessionState state, bool expected)
        {
            Assert.That(ARReadiness.CanReadCameraInState(state), Is.EqualTo(expected));
        }

        [TestCase("8801104671507", true)]
        [TestCase("8 801104 671507", true)]
        [TestCase("0123456789012", true)]
        [TestCase("8801104671508", false)]
        [TestCase("880110467150", false)]
        [TestCase("８８０１１０４６７１５０７", false)]
        [TestCase(null, false)]
        public void Ean13ChecksLengthDigitsChecksumWithoutRemovingZero(string raw, bool expected)
        {
            Assert.That(BarcodeIdentity.TryNormalizeEan13(raw, out var normalized), Is.EqualTo(expected));
            if (raw == "0123456789012") Assert.That(normalized, Is.EqualTo(raw));
        }

        [Test]
        public void CatalogUsesExactIdentityAndPreservesUnknownVersusZero()
        {
            var catalog = new ProductCatalog(Json);
            Assert.That(catalog.TryFind("8801104671507", out var product), Is.True);
            Assert.That(product.catalogProductId, Is.EqualTo("1516c3eb-3dfb-4e11-8040-d23fadcdaad0"));
            Assert.That(product.mapping.scope, Is.EqualTo("demo-only"));
            Assert.That(product.nutrition.fiberGrams, Is.Null);
            Assert.That(product.nutrition.sugarsGrams, Is.EqualTo(7));
            Assert.That(product.price.unitPriceKrw, Is.EqualTo(1400));
            Assert.That(product.price.observedAt, Is.EqualTo("2026-05-08"));
            Assert.That(ProductInfoFormatter.Number(null, "g"), Is.EqualTo("정보 없음"));
            Assert.That(ProductInfoFormatter.Number(0, "g"), Is.EqualTo("0g"));
            Assert.That(catalog.TryFind("4006381333931", out _), Is.False);
        }

        [Test]
        public void CatalogRejectsWrongNutritionLinkAndDuplicateBarcode()
        {
            var badLink = Json.Replace("\"sourceCatalogProductId\": \"1516c3eb-3dfb-4e11-8040-d23fadcdaad0\"", "\"sourceCatalogProductId\": \"00000000-0000-0000-0000-000000000000\"");
            Assert.Throws<FormatException>(() => new ProductCatalog(badLink));
            var data = Newtonsoft.Json.Linq.JObject.Parse(Json);
            ((Newtonsoft.Json.Linq.JArray)data["products"]).Add(data["products"][0].DeepClone());
            Assert.Throws<FormatException>(() => new ProductCatalog(data.ToString()));
            Assert.Throws<FormatException>(() => new ProductCatalog(Json.Replace("\"unitPriceKrw\": 1400", "\"unitPriceKrw\": -1")));
        }

        [Test]
        public void MissingPriceAndNutritionRemainMissing()
        {
            var data = Newtonsoft.Json.Linq.JObject.Parse(Json);
            data["products"][0]["price"] = null;
            data["products"][0]["nutrition"] = null;
            var catalog = new ProductCatalog(data.ToString());
            catalog.TryFind("8801104671507", out var product);
            Assert.That(product.price, Is.Null);
            Assert.That(product.nutrition, Is.Null);
            Assert.That(ProductInfoFormatter.Price(product), Is.EqualTo("가격 정보 없음"));
            Assert.That(ProductInfoFormatter.Nutrition(product), Is.EqualTo("영양정보 없음"));
        }

        [Test]
        public void WorkflowRejectsRepeatedScanAndClearsStaleProduct()
        {
            var flow = new ProductWorkflow(new ProductCatalog(Json));
            Assert.That(flow.AcceptBarcode("8801104671507"), Is.True);
            Assert.That(flow.CanPlace, Is.True);
            Assert.That(flow.AcceptBarcode("4006381333931"), Is.False);
            flow.MarkPlaced();
            Assert.That(flow.Phase, Is.EqualTo(ProductPhase.Placed));
            flow.RequestPlacement();
            Assert.That(flow.CanPlace, Is.True);
            flow.StartScan();
            Assert.That(flow.Selected, Is.Null);
            flow.AcceptBarcode("4006381333931");
            Assert.That(flow.Phase, Is.EqualTo(ProductPhase.UnknownBarcode));
            Assert.That(flow.CanPlace, Is.False);
            flow.StartScan();
            flow.ScanTimedOut();
            Assert.That(flow.Phase, Is.EqualTo(ProductPhase.ScanFailed));
            Assert.That(new ProductWorkflow(null).Phase, Is.EqualTo(ProductPhase.DataUnavailable));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DecoderReadsGeneratedEan13IncludingNinetyDegreeRotation(bool rotate)
        {
            const int width = 960, height = 320;
            var bits = new MultiFormatWriter().encode("8801104671507", BarcodeFormat.EAN_13, width, height);
            var pixels = new byte[width * height];
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    pixels[rotate ? x * height + (height - y - 1) : y * width + x] = bits[x, y] ? (byte)0 : (byte)255;
            var result = new BarcodeDecoder().Decode(pixels, rotate ? height : width, rotate ? width : height);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Text, Is.EqualTo("8801104671507"));
            Assert.That(result.Format, Is.EqualTo("EAN_13"));
        }

        [Test]
        public void BarcodeCameraPrefersDetailedCpuFramesWithinBoundedMemory()
        {
            Assert.That(CameraResolutionPolicy.Score(1920, 1080, 30), Is.GreaterThan(CameraResolutionPolicy.Score(640, 480, 60)));
            Assert.That(CameraResolutionPolicy.Score(1280, 720, 30), Is.GreaterThan(CameraResolutionPolicy.Score(640, 480, 30)));
            Assert.That(CameraResolutionPolicy.Score(1920, 1080, 30), Is.GreaterThan(CameraResolutionPolicy.Score(1920, 1080, 60)));
            Assert.That(CameraResolutionPolicy.Score(3840, 2160, 30), Is.Zero);
            Assert.That(CameraResolutionPolicy.Score(0, 1080, 30), Is.Zero);
        }

        [Test]
        public void BarcodeNeedsThreeConsecutiveMatchingCameraFrames()
        {
            var confirmation = new BarcodeConfirmation();
            var known = new BarcodeReadResult("8801104671507", "EAN_13");
            Assert.That(confirmation.Observe(known, 0), Is.False);
            Assert.That(confirmation.Observe(known, 0.35f), Is.False);
            Assert.That(confirmation.Observe(known, 0.7f), Is.True);
            confirmation.Reset();
            Assert.That(confirmation.Count, Is.Zero);
            Assert.That(confirmation.Observe(known, 1), Is.False, "Rescan must not reuse previous confidence");
        }

        [Test]
        public void BarcodeDisagreementMissingFramesAndExpiredReadsResetConfidence()
        {
            var confirmation = new BarcodeConfirmation();
            var known = new BarcodeReadResult("8801104671507", "EAN_13");
            foreach (var text in new[] { "5505256701701", "7201143677507" })
                Assert.That(confirmation.Observe(new BarcodeReadResult(text, "EAN_13"), 0), Is.False);
            Assert.That(confirmation.Observe(known, 0.35f), Is.False);
            Assert.That(confirmation.Count, Is.EqualTo(1));
            Assert.That(confirmation.Observe(null, 0.7f), Is.False);
            Assert.That(confirmation.Observe(known, 1.05f), Is.False);
            Assert.That(confirmation.Count, Is.EqualTo(1));
            Assert.That(confirmation.Observe(known, 3), Is.False);
            Assert.That(confirmation.Count, Is.EqualTo(1));
            Assert.That(confirmation.Observe(new BarcodeReadResult("8801104671508", "EAN_13"), 3.35f), Is.False);
            Assert.That(confirmation.Count, Is.Zero);
        }

        [TestCase("5505256701701")]
        [TestCase("7201143677507")]
        public void ConfirmedUnknownBarcodeNeverFallsBackToDemoProduct(string barcode)
        {
            var confirmation = new BarcodeConfirmation();
            var read = new BarcodeReadResult(barcode, "EAN_13");
            confirmation.Observe(read, 0);
            confirmation.Observe(read, 0.35f);
            Assert.That(confirmation.Observe(read, 0.7f), Is.True);
            var flow = new ProductWorkflow(new ProductCatalog(Json));
            Assert.That(flow.AcceptBarcode(confirmation.Candidate), Is.True);
            Assert.That(flow.LastBarcode, Is.EqualTo(barcode));
            Assert.That(flow.Phase, Is.EqualTo(ProductPhase.UnknownBarcode));
            Assert.That(flow.Selected, Is.Null);
        }

        [Test]
        public void SceneHasWiredComponentsAndTrackedCamera()
        {
            var scene = EditorSceneManager.OpenScene(ProjectBuilder.ScenePath, OpenSceneMode.Single);
            var components = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            Assert.That(components.All(component => component != null), Is.True, "Missing scene script");
            Assert.That(components.OfType<ARSession>().Count(), Is.EqualTo(1));
            Assert.That(components.OfType<TrackedPoseDriver>().Count(), Is.EqualTo(1));
            Assert.That(EditorBuildSettings.scenes.Single(s => s.enabled).path, Is.EqualTo(ProjectBuilder.ScenePath));
            foreach (var component in components.Where(c => c.GetType().Namespace == "MidtermAR"))
            {
                var iterator = new SerializedObject(component).GetIterator();
                while (iterator.NextVisible(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                        Assert.That(iterator.objectReferenceValue, Is.Not.Null, component.name + ": " + iterator.propertyPath);
            }
        }

        [Test]
        public void PlaneVisualizationCannotBeRestoredByThePackagesNextUpdate()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/PlacementPlane.prefab");
            var sessionField = typeof(ARSession).GetField("s_State", BindingFlags.Static | BindingFlags.NonPublic);
            var previousSession = sessionField.GetValue(null);
            var visualizer = root.GetComponent<ARPlaneMeshVisualizer>();
            Mesh createdMesh = null;
            try
            {
                sessionField.SetValue(null, ARSessionState.SessionTracking);
                SetPlaneData(root.GetComponent<ARPlane>(), TrackingState.Tracking);
                // EditMode에서 실제 패키지의 프레임 로직을 직접 실행해 기기 없이 표시 경쟁을 재현한다.
                typeof(ARPlaneMeshVisualizer).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(visualizer, null);
                createdMesh = visualizer.mesh;
                var packageUpdate = typeof(ARPlaneMeshVisualizer).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                var applyVisibility = typeof(ProductPlacement).GetMethod("SetPlaneVisualization", BindingFlags.Static | BindingFlags.NonPublic);
                var renderer = root.GetComponent<MeshRenderer>();
                renderer.enabled = false;
                packageUpdate.Invoke(visualizer, null);
                Assert.That(renderer.enabled, Is.True, "Old renderer-only hiding is overwritten by the actual package update.");

                applyVisibility.Invoke(null, new object[] { root.GetComponent<ARPlane>(), false });
                Assert.That(visualizer.enabled, Is.False);
                for (var frame = 0; frame < 4; frame++)
                {
                    // 비활성 컴포넌트의 Update까지 강제로 호출해도 패키지가 다시 표시하지 못해야 한다.
                    packageUpdate.Invoke(visualizer, null);
                    Assert.That(renderer.enabled, Is.False, "Hidden plane was restored on frame " + frame);
                }

                applyVisibility.Invoke(null, new object[] { root.GetComponent<ARPlane>(), true });
                packageUpdate.Invoke(visualizer, null);
                Assert.That(visualizer.enabled, Is.True);
                Assert.That(renderer.enabled, Is.True, "Repositioning must allow the tracked plane to appear again.");
            }
            finally
            {
                sessionField.SetValue(null, previousSession);
                if (createdMesh != null) UnityEngine.Object.DestroyImmediate(createdMesh);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void SubsumedAndLimitedPlanesAreNotPlacementCandidates()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/PlacementPlane.prefab");
            var replacement = new GameObject("Replacement plane");
            try
            {
                var plane = root.GetComponent<ARPlane>();
                var replacementPlane = replacement.AddComponent<ARPlane>();
                var usable = typeof(ProductPlacement).GetMethod("IsUsablePlane", BindingFlags.Static | BindingFlags.NonPublic);
                SetPlaneData(plane, TrackingState.Tracking);
                Assert.That(usable.Invoke(null, new object[] { plane }), Is.True);
                typeof(ARPlane).GetProperty("subsumedBy").SetValue(plane, replacementPlane);
                Assert.That(usable.Invoke(null, new object[] { plane }), Is.False, "A merged plane must not count as a placement surface.");
                typeof(ARPlane).GetProperty("subsumedBy").SetValue(plane, null);
                SetPlaneData(plane, TrackingState.Limited);
                Assert.That(usable.Invoke(null, new object[] { plane }), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(replacement);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetPlaneData(ARPlane plane, TrackingState state)
        {
            var data = new BoundedPlane(new TrackableId(1, 1), TrackableId.invalidId, Pose.identity,
                Vector2.zero, Vector2.one, PlaneAlignment.HorizontalUp, state, IntPtr.Zero,
                PlaneClassifications.Table, TrackableId.invalidId);
            typeof(ARTrackable<BoundedPlane, ARPlane>).GetProperty("sessionRelativeData", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(plane, data);
        }

        [Test]
        public void KoreanWorldPanelHasGlyphsAndTextFits()
        {
            var root = PrefabUtility.LoadPrefabContents(ProjectBuilder.PanelPath);
            try
            {
                var catalog = new ProductCatalog(Json);
                catalog.TryFind("8801104671507", out var product);
                root.GetComponent<WorldProductPanel>().Bind(product);
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    foreach (var character in text.text.Where(c => !char.IsWhiteSpace(c)))
                        Assert.That(text.font.HasCharacter(character, true, true), Is.True, "Missing glyph " + character);
                    text.ForceMeshUpdate(true, true);
                    Assert.That(text.isTextOverflowing, Is.False, text.name + ": text overflow");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
