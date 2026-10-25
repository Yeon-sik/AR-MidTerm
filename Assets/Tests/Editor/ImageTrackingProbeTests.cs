using MidtermAR.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MidtermAR.Tests
{
    public sealed class ImageTrackingProbeTests
    {
        [TestCase(true, false, TrackingState.Tracking, 1, true, true)]
        [TestCase(true, false, TrackingState.Tracking, 2, true, false)]
        [TestCase(true, false, TrackingState.Tracking, -1, true, false)]
        [TestCase(true, false, TrackingState.Limited, 1, true, false)]
        [TestCase(true, false, TrackingState.None, 0, true, false)]
        [TestCase(false, false, TrackingState.Tracking, 1, true, false)]
        [TestCase(true, true, TrackingState.Tracking, 1, true, false)]
        [TestCase(true, false, TrackingState.Tracking, 1, false, false)]
        public void CubeRequiresCurrentCameraTracking(bool ready, bool paused, TrackingState state, int method, bool expected, bool visible)
            => Assert.That(ImageTrackingProbe.CanDisplay(ready, paused, state, method, expected), Is.EqualTo(visible));

        [Test]
        public void GeneratedSceneUsesOneImageAndPreservesTheProductScene()
        {
            var original = System.IO.File.ReadAllBytes(ProjectBuilder.ScenePath);
            ImageTrackingProbeBuilder.PrepareScene();
            EditorSceneManager.OpenScene(ImageTrackingProbeBuilder.ScenePath);
            try
            {
                Assert.That(System.IO.File.ReadAllBytes(ProjectBuilder.ScenePath), Is.EqualTo(original));
                var manager = Object.FindFirstObjectByType<ARTrackedImageManager>();
                Assert.That(manager, Is.Not.Null);
                Assert.That(manager.referenceLibrary.count, Is.EqualTo(1));
                var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(ImageTrackingProbeBuilder.LibraryPath);
                Assert.That(library[0].name, Is.EqualTo(ImageTrackingProbe.TargetName));
                Assert.That(library[0].textureGuid.ToString("N"),
                    Is.EqualTo(AssetDatabase.AssetPathToGUID(ImageTrackingProbeBuilder.MarkerPath)),
                    "An existing reference library must use the current marker texture.");
                Assert.That(library[0].specifySize, Is.False, "Do not invent a physical width before measurement.");
                Assert.That(manager.requestedMaxNumberOfMovingImages, Is.EqualTo(1));
                Assert.That(Object.FindFirstObjectByType<ARBarcodeScanner>(), Is.Null);
                Assert.That(Object.FindFirstObjectByType<ARPlaneManager>(), Is.Null);
                var probe = Object.FindFirstObjectByType<ImageTrackingProbe>();
                var serialized = new SerializedObject(probe);
                foreach (var property in new[] { "imageManager", "session", "readiness", "cameraManager", "cube", "statusText" })
                    Assert.That(serialized.FindProperty(property).objectReferenceValue, Is.Not.Null, property);
                var cube = (Transform)serialized.FindProperty("cube").objectReferenceValue;
                Assert.That(cube.gameObject.activeSelf, Is.False);
            }
            finally { EditorSceneManager.OpenScene(ProjectBuilder.ScenePath); }
        }
    }
}
