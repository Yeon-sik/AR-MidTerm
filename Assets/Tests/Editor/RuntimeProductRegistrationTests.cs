using System;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace MidtermAR.Tests
{
    public sealed class RuntimeProductRegistrationTests
    {
        [Test]
        public void BeginFreezesTheIdentifiedProductUntilTheAttemptEnds()
        {
            var state = new RegistrationLifecycle();
            Assert.That(state.Begin("pt:one", "첫 상품"), Is.True);
            Assert.That(state.Begin("off:two", "다른 상품"), Is.False);
            var attempt = state.StartJob();
            Assert.That(attempt.ProductKey, Is.EqualTo("pt:one"));
            Assert.That(attempt.ProductLabel, Is.EqualTo("첫 상품"));
            Assert.That(state.ProductKey, Is.EqualTo("pt:one"));
        }

        [Test]
        public void SuccessfulCurrentAttemptPublishesOnceAndBlocksOnlyItsProduct()
        {
            var state = new RegistrationLifecycle();
            state.Begin("pt:one", "첫 상품");
            var attempt = state.StartJob();
            Assert.That(state.Complete(attempt, true), Is.True);
            Assert.That(state.Complete(attempt, true), Is.False);
            Assert.That(state.IsActive, Is.False);
            Assert.That(state.IsBusy, Is.False);
            Assert.That(state.CanBegin("pt:one"), Is.False);
            Assert.That(state.CanBegin("off:two"), Is.True);
        }

        [Test]
        public void CancelOrPauseKeepsNativeJobOwnedAndIgnoresItsLateSuccess()
        {
            var state = new RegistrationLifecycle();
            state.Begin("pt:one", "첫 상품");
            var attempt = state.StartJob();
            state.Cancel();
            Assert.That(state.IsActive, Is.False);
            Assert.That(state.IsBusy, Is.True, "Cancel must not release native-job resources before completion.");
            Assert.That(state.CanBegin("off:two"), Is.False);
            Assert.That(state.Complete(attempt, true), Is.False, "The cancelled generation must never publish ImageAdded.");
            Assert.That(state.IsBusy, Is.False);
            Assert.That(state.HasNativeImage("pt:one"), Is.True, "Successful native additions cannot be removed by cancelling their UI.");
            Assert.That(state.CanBegin("pt:one"), Is.False);
            Assert.That(state.CanBegin("off:two"), Is.True);
        }

        [Test]
        public void CancelledFailedAttemptCanBeTriedAgainAfterItsJobCompletes()
        {
            var state = new RegistrationLifecycle();
            state.Begin("off:one", "첫 상품");
            var attempt = state.StartJob();
            state.Cancel();
            Assert.That(state.Complete(attempt, false), Is.False);
            Assert.That(state.HasNativeImage("off:one"), Is.False);
            Assert.That(state.Begin("off:one", "첫 상품"), Is.True);
        }

        [Test]
        public void QualityFailureKeepsTheProductSelectedForAnotherCapture()
        {
            var state = new RegistrationLifecycle();
            state.Begin("off:one", "첫 상품");
            var failed = state.StartJob();
            Assert.That(state.Complete(failed, false), Is.False);
            Assert.That(state.IsActive, Is.True);
            Assert.That(state.IsBusy, Is.False);
            var retry = state.StartJob();
            Assert.That(retry.ProductKey, Is.EqualTo(failed.ProductKey));
            Assert.That(state.Complete(retry, true), Is.True);
        }

        [Test]
        public void OldCompletionCannotReleaseASeparateProductsPendingJob()
        {
            var state = new RegistrationLifecycle();
            state.Begin("pt:one", "첫 상품");
            var first = state.StartJob();
            state.Complete(first, true);
            state.Begin("off:two", "다른 상품");
            var second = state.StartJob();
            Assert.That(state.Complete(first, true), Is.False);
            Assert.That(state.IsBusy, Is.True);
            Assert.That(state.Complete(second, true), Is.True);
        }

        [Test]
        public void InvalidIdentityAndConcurrentJobsAreRejected()
        {
            var state = new RegistrationLifecycle();
            Assert.That(state.Begin(null, "상품"), Is.False);
            Assert.That(state.Begin("  ", "상품"), Is.False);
            Assert.That(state.Begin("pt:one", "  "), Is.False);
            Assert.Throws<InvalidOperationException>(() => state.StartJob());
            state.Begin("pt:one", "상품");
            state.StartJob();
            Assert.Throws<InvalidOperationException>(() => state.StartJob());
        }

        [Test]
        public void FormalCaptureKeepsTheDeviceProvenGuideAndDoesNotInventPhysicalSize()
        {
            Assert.That(RuntimeProductRegistrationController.GuideViewport, Is.EqualTo(RuntimeImageRegistrationProbe.GuideViewport));
            Assert.That(RuntimeProductRegistrationController.RequestedPhysicalWidthMetres, Is.Null);
            Assert.That(RuntimeProductRegistrationView.CaptureArea.Overlaps(RuntimeProductRegistrationView.CancelArea), Is.False);
        }

        [Test]
        public void RegistrationViewUsesKoreanFontAndTreatsProductNamesAsPlainText()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Generated/NotoSansKR SDF.asset");
            Assert.That(font, Is.Not.Null);
            var root = new GameObject("Registration View Test", typeof(RectTransform));
            try
            {
                var view = root.AddComponent<RuntimeProductRegistrationView>();
                view.Initialize(font);
                view.Refresh("<size=100>상품</size>", true, false, RuntimeProductRegistrationView.ReadyInstruction);
                var texts = root.GetComponentsInChildren<TextMeshProUGUI>();
                Assert.That(texts.Length, Is.GreaterThanOrEqualTo(5));
                foreach (var text in texts)
                {
                    Assert.That(text.font, Is.SameAs(font));
                    Assert.That(text.richText, Is.False);
                    foreach (var character in text.text)
                        if (!char.IsControl(character)) Assert.That(font.HasCharacter(character, true, true), Is.True, "Missing glyph: " + character);
                }
                StringAssert.Contains("앱을 종료", RuntimeProductRegistrationView.SessionNotice);
                StringAssert.Contains("추정", RuntimeProductRegistrationView.SessionNotice);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
