using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidtermAR
{
    public sealed class ProductARApp : MonoBehaviour
    {
        [SerializeField] private TextAsset catalogJson;
        [SerializeField] private TextAsset imageBindingsJson;
        [SerializeField] private ARReadiness readiness;
        [SerializeField] private ARBarcodeScanner scanner;
        [SerializeField] private ProductScreenView screen;
        [SerializeField] private TrackedImageProductPanel imagePanel;
        private ProductWorkflow workflow;
        private ProductImageCatalog imageCatalog;
        private float nextRefresh;
        private bool paused;

        private void Awake()
        {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            Debug.developerConsoleEnabled = false;
#endif
            try
            {
                var catalog = new ProductCatalog(catalogJson.text);
                imageCatalog = new ProductImageCatalog(imageBindingsJson.text, catalog);
                imageCatalog.ValidateLibrary(imagePanel.ReferenceLibrary);
                workflow = new ProductWorkflow(catalog);
                Debug.Log("[ProductAR] Local demo catalog loaded: " + catalog.Count + ", mapping: demo-only");
            }
            catch (Exception error)
            {
                workflow = new ProductWorkflow(null);
                Debug.LogError("[ProductAR] Catalog load: " + error.Message);
            }
        }

        private void OnEnable()
        {
            scanner.Decoded += OnDecoded;
            scanner.TimedOut += OnScanTimedOut;
            scanner.Observed += OnObserved;
            imagePanel.RescanRequested += RestartScan;
        }

        private void OnDisable()
        {
            scanner.Decoded -= OnDecoded;
            scanner.TimedOut -= OnScanTimedOut;
            scanner.Observed -= OnObserved;
            imagePanel.RescanRequested -= RestartScan;
            scanner.SetScanning(false);
            imagePanel.InvalidatePose();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (workflow.Selected != null) RestartScan();
                else Application.Quit();
            }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.15f;
            Refresh();
        }

        private void OnDecoded(BarcodeReadResult result)
        {
            if (result.Format != "EAN_13" || !workflow.AcceptBarcode(result.Text)) return;
            ProductImageBinding binding = null;
            if (workflow.Selected != null) imageCatalog.TryFind(workflow.Selected, out binding);
            imagePanel.SetProduct(workflow.Selected, binding);
            if (workflow.Selected != null)
            {
                workflow.FollowImage();
                scanner.SetFollowing(workflow.Selected.barcode);
            }
            else scanner.SetScanning(false);
            Debug.Log("[ProductAR] Barcode decoded: " + result.Text + ", phase: " + workflow.Phase);
            Refresh();
        }

        private void OnObserved(BarcodeReadResult result, BarcodeViewportTransform transform, float capturedAt)
        {
            // 화면 좌표·바코드 소실은 이미지의 3D 자세를 무효화하지 않는다.
            if (!paused && workflow.Phase == ProductPhase.TrackingImage
                && capturedAt <= Time.unscaledTime && Time.unscaledTime - capturedAt <= 1f) imagePanel.ObserveBarcode(result);
        }
        private void OnScanTimedOut() { workflow.ScanTimedOut(); Refresh(); }

        public void PrimaryAction()
        {
            if (readiness.PermissionBlocked) { readiness.OpenSettings(); return; }
            if (!readiness.CameraReady || !scanner.HasCameraFrame) { readiness.Retry(); return; }
            RestartScan();
        }

        public void RestartScan()
        {
            scanner.SetScanning(false);
            imagePanel.SetProduct(null, null);
            workflow.StartScan();
            Refresh();
        }

        private void Refresh()
        {
            var cameraReady = readiness.CameraReady && scanner.HasCameraFrame && !paused;
            if (cameraReady && workflow.Phase == ProductPhase.TrackingImage) scanner.SetFollowing(workflow.Selected.barcode);
            else scanner.SetScanning(cameraReady && workflow.Phase == ProductPhase.Scanning);
            var step = "1 / 2 · 바코드 스캔";
            var title = "바코드를 비춰주세요";
            var message = "바코드 전체가 선명하게 보이도록\n휴대폰을 잠시 멈춰주세요.";
            var action = "다시 스캔";
            var summary = "";
            var error = false;
            var attention = false;
            var primary = false;
            if (workflow.Phase == ProductPhase.DataUnavailable)
            { step = "준비 오류"; title = "상품정보를 불러올 수 없어요"; message = "앱을 종료한 뒤 다시 실행해 주세요."; error = true; }
            else if (!cameraReady)
            {
                step = readiness.PermissionBlocked ? "카메라 권한 필요" : "카메라 준비 중";
                title = readiness.PermissionBlocked ? "카메라 사용을 허용해 주세요" : "카메라를 준비하고 있어요";
                message = readiness.CameraReady && !scanner.HasCameraFrame ? "카메라 영상을 기다리고 있어요.\n계속되면 다시 확인을 눌러주세요." : readiness.Status;
                action = readiness.PermissionBlocked ? "설정 열기" : "다시 확인";
                error = readiness.PermissionBlocked;
                attention = !error;
                primary = !paused && !readiness.IsInitializing;
            }
            else if (workflow.Phase == ProductPhase.UnknownBarcode)
            { step = "1 / 2 · 상품 확인 필요"; title = "시연 데이터에 없는 상품이에요"; message = "읽은 번호: " + workflow.LastBarcode + "\n포장의 번호를 확인하고 다시 스캔해 주세요."; error = true; primary = true; }
            else if (workflow.Phase == ProductPhase.ScanFailed)
            { step = "1 / 2 · 스캔 시간 초과"; title = "바코드를 읽지 못했어요"; message = "반사와 초점을 확인하고\n바코드 전체가 보이도록 다시 시도해 주세요."; error = true; primary = true; }
            else if (workflow.Phase == ProductPhase.TrackingImage)
            {
                var visible = imagePanel.HasVisibleTarget;
                step = visible ? "2 / 2 · 이미지 추적 중" : "2 / 2 · 상품 이미지 찾는 중";
                title = visible ? "이미지 위에 상품정보를 표시해요" : "선택한 상품의 앞면을 비춰주세요";
                message = visible ? "영양·가격 버튼으로 정보를 바꿔보세요."
                    : "바코드가 안 보여도 선택은 유지됩니다.\n등록한 포장 앞면 전체를 비춰주세요.";
                summary = visible ? "" : workflow.Selected.name + " · " + workflow.Selected.specification;
                action = "다른 상품 스캔";
                primary = true;
                attention = !visible;
                if (imagePanel.Binding == null)
                {
                    title = "등록된 상품 이미지가 없어요";
                    message = "이 상품의 포장 이미지를 등록한 뒤\n다시 스캔해 주세요.";
                    error = true;
                }
                else if (imagePanel.ConflictingBarcode != null)
                {
                    title = "다른 상품의 바코드가 보였어요";
                    message = "정보판을 숨겼습니다.\n다른 상품 스캔을 눌러 다시 선택해 주세요.";
                    error = true;
                }
                else if (imagePanel.HasImageConflict)
                {
                    title = "선택한 상품과 다른 이미지예요";
                    message = "정보판을 숨겼습니다.\n선택한 상품의 등록 이미지만 비춰주세요.";
                }
                else if (!readiness.IsReady)
                {
                    title = "이미지 위치를 확인하고 있어요";
                    message = readiness.TrackingGuidance;
                }
                else if (imagePanel.TrackingMethodUnavailable)
                {
                    title = "이미지 추적 상태를 확인할 수 없어요";
                    message = "정보판을 숨겼습니다.\n잠시 후 다시 확인해 주세요.";
                }
                else if (!visible && imagePanel.Binding.developmentOnly)
                {
                    title = "개발용 카드 마커를 비춰주세요";
                    message = imagePanel.Binding.displayName + "\n상품 포장 인식은 아직 검증하지 않았습니다.";
                }
            }
            else if (scanner.ConfirmationCount > 0)
            { step = "1 / 2 · 바코드 확인 중"; title = "상품을 확인하고 있어요"; message = "바코드가 보이도록 잠시 유지해 주세요."; }
            screen.Show(step, title, message, summary, action, primary, false, false, error, attention,
                secondaryAction: workflow.Selected != null && cameraReady);
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            if (value && workflow != null)
            {
                scanner.SetScanning(false);
                imagePanel.InvalidatePose();
            }
            nextRefresh = 0;
        }
    }
}
