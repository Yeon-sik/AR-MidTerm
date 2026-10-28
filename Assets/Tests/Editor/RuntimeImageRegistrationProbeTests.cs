using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace MidtermAR.Tests
{
    public sealed class RuntimeImageRegistrationProbeTests
    {
        private static BarcodeViewportTransform Identity => new BarcodeViewportTransform(
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1));

        [Test]
        public void CaptureConvertsCpuTopRowsIntoUnityBottomRowsWithoutMirroring()
        {
            var result = RuntimeImageRegistrationCapture.CropRgb24(Pixels(1, 2, 3, 4), 2, 2,
                Identity, new Rect(0, 0, 1, 1), 2, 2);
            CollectionAssert.AreEqual(Pixels(3, 4, 1, 2), result);
        }

        [Test]
        public void CaptureUsesPortraitRotationAndKeepsGuideAspectRatio()
        {
            var portrait = new BarcodeViewportTransform(new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 1));
            var result = RuntimeImageRegistrationCapture.CropRgb24(Pixels(1, 2, 3, 4, 5, 6), 2, 3,
                portrait, new Rect(0, 0, 1, 1), 3, 2);
            CollectionAssert.AreEqual(Pixels(1, 3, 5, 2, 4, 6), result);
        }

        [Test]
        public void CaptureHonorsTheCameraViewportCropInsteadOfTheFullCpuFrame()
        {
            var cropped = new BarcodeViewportTransform(new Vector2(-.5f, 0), new Vector2(1.5f, 0), new Vector2(-.5f, 1));
            var result = RuntimeImageRegistrationCapture.CropRgb24(Pixels(1, 2, 3, 4), 4, 1,
                cropped, new Rect(0, 0, 1, 1), 2, 1);
            CollectionAssert.AreEqual(Pixels(2, 3), result);
        }

        [Test]
        public void CaptureRejectsMissingMappingAndGuidesOutsideTheCameraImage()
        {
            Assert.Throws<ArgumentException>(() => RuntimeImageRegistrationCapture.CropRgb24(Pixels(1), 1, 1,
                default, new Rect(0, 0, 1, 1), 1, 1));
            var inset = new BarcodeViewportTransform(new Vector2(.2f, .2f), new Vector2(.8f, .2f), new Vector2(.2f, .8f));
            Assert.Throws<ArgumentException>(() => RuntimeImageRegistrationCapture.CropRgb24(Pixels(1), 1, 1,
                inset, new Rect(0, 0, 1, 1), 1, 1));
        }

        [Test]
        public void CaptureRejectsInvalidBufferAndDimensions()
        {
            Assert.Throws<ArgumentException>(() => RuntimeImageRegistrationCapture.CropRgb24(new byte[2], 1, 1,
                Identity, new Rect(0, 0, 1, 1), 1, 1));
            Assert.Throws<ArgumentException>(() => RuntimeImageRegistrationCapture.CropRgb24(Pixels(1), 1, 1,
                Identity, new Rect(0, 0, 1, 1), 1601, 1));
            Assert.That(RuntimeImageRegistrationCapture.TryImagePoint(Identity, new Vector2(float.NaN, 0), out _), Is.False);
        }

        [Test]
        public void RegistrationSuccessNeedsFirstTrackingThenLossThenFreshFullTracking()
        {
            var evidence = new RuntimeImageRegistrationEvidence();
            evidence.Registered("front");
            Assert.That(evidence.Reacquired, Is.False);
            evidence.Observe("front", true, TrackingState.Tracking, 1, new Vector2(.1f, .2f));
            Assert.That(evidence.FirstTracking, Is.True);
            Assert.That(evidence.Reacquired, Is.False);
            evidence.Observe("front", true, TrackingState.Tracking, 2, new Vector2(.1f, .2f));
            Assert.That(evidence.Visible, Is.False);
            Assert.That(evidence.ObservedLoss, Is.True);
            evidence.Observe("front", true, TrackingState.Tracking, 1, new Vector2(.11f, .21f));
            Assert.That(evidence.Reacquired, Is.True);
            Assert.That(evidence.EstimatedSize, Is.EqualTo(new Vector2(.11f, .21f)));
        }

        [TestCase(0f, .1f)]
        [TestCase(.1f, 0f)]
        [TestCase(float.NaN, .1f)]
        [TestCase(float.PositiveInfinity, .1f)]
        public void FullTrackingWithoutValidPhysicalSizeDoesNotConfirmRecognition(float width, float height)
        {
            var evidence = new RuntimeImageRegistrationEvidence();
            evidence.Registered("front");
            evidence.Observe("front", true, TrackingState.Tracking, 1, new Vector2(width, height));
            Assert.That(evidence.FirstTracking, Is.False);
            Assert.That(evidence.Visible, Is.False);
        }

        [Test]
        public void PauseUnknownNativeMethodAndStaleFramesDoNotCountAsPhysicalLoss()
        {
            var evidence = new RuntimeImageRegistrationEvidence();
            evidence.Registered("front");
            evidence.Observe("front", true, TrackingState.Tracking, 1, Vector2.one);
            evidence.Observe("front", false, TrackingState.None, 0, Vector2.zero);
            evidence.Observe("front", true, TrackingState.Tracking, -1, Vector2.one);
            evidence.Hide();
            Assert.That(evidence.Visible, Is.False);
            Assert.That(evidence.ObservedLoss, Is.False);
            evidence.Observe("front", true, TrackingState.Tracking, 1, Vector2.one);
            Assert.That(evidence.Reacquired, Is.False);
        }

        [Test]
        public void DifferentImageAndCancelledRegistrationCannotConfirmReacquisition()
        {
            var evidence = new RuntimeImageRegistrationEvidence();
            evidence.Registered("front");
            evidence.Observe("other", true, TrackingState.Tracking, 1, Vector2.one);
            Assert.That(evidence.FirstTracking, Is.False);
            evidence.Observe("front", true, TrackingState.Tracking, 1, Vector2.one);
            evidence.Reset();
            evidence.Observe("front", true, TrackingState.None, 0, Vector2.zero);
            evidence.Observe("front", true, TrackingState.Tracking, 1, Vector2.one);
            Assert.That(evidence.Reacquired, Is.False);
            Assert.That(evidence.Visible, Is.False);
        }

        [Test]
        public void ProbePointerUsesBottomLeftInputCoordinatesForCaptureAndCancel()
        {
            var screen = new Vector2Int(1000, 2000);
            Assert.That(RuntimeImageRegistrationProbe.ActionAtPointer(new Vector2(600, 320), screen),
                Is.EqualTo(RuntimeImageRegistrationProbe.PointerAction.Capture));
            Assert.That(RuntimeImageRegistrationProbe.ActionAtPointer(new Vector2(600, 140), screen),
                Is.EqualTo(RuntimeImageRegistrationProbe.PointerAction.Cancel));
            Assert.That(RuntimeImageRegistrationProbe.ActionAtPointer(new Vector2(600, 1680), screen),
                Is.EqualTo(RuntimeImageRegistrationProbe.PointerAction.None), "Unflipped GUI coordinates must not trigger capture.");
        }

        [Test]
        public void ProbePointerOutsideButtonsAndInvalidScreensDoesNothing()
        {
            var screen = new Vector2Int(1000, 2000);
            foreach (var point in new[] { new Vector2(10, 320), new Vector2(600, 240),
                         new Vector2(-1, 320), new Vector2(1001, 320), new Vector2(float.NaN, 320) })
                Assert.That(RuntimeImageRegistrationProbe.ActionAtPointer(point, screen),
                    Is.EqualTo(RuntimeImageRegistrationProbe.PointerAction.None));
            Assert.That(RuntimeImageRegistrationProbe.ActionAtPointer(new Vector2(600, 320), Vector2Int.zero),
                Is.EqualTo(RuntimeImageRegistrationProbe.PointerAction.None));
        }

        private static byte[] Pixels(params byte[] values)
        {
            var bytes = new byte[values.Length * 3];
            for (var i = 0; i < values.Length; i++)
                bytes[i * 3] = bytes[i * 3 + 1] = bytes[i * 3 + 2] = values[i];
            return bytes;
        }
    }
}
