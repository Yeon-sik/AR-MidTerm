using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidtermAR
{
    public sealed class ProductARApp : MonoBehaviour
    {
        [SerializeField] private TextAsset catalogJson;
        [SerializeField] private ARReadiness readiness;
        [SerializeField] private ARBarcodeScanner scanner;
        [SerializeField] private ProductScreenView screen;
        [SerializeField] private BarcodeProductPanel followPanel;
        private ProductWorkflow workflow;
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
        }

        private void OnDisable()
        {
            scanner.Decoded -= OnDecoded;
            scanner.TimedOut -= OnScanTimedOut;
            scanner.Observed -= OnObserved;
            scanner.SetScanning(false);
            followPanel.SetCameraReady(false);
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
            followPanel.SetProduct(workflow.Selected);
            if (workflow.Selected != null)
            {
                workflow.FollowBarcode();
                scanner.SetFollowing(workflow.Selected.barcode);
            }
            else scanner.SetScanning(false);
            Debug.Log("[ProductAR] Barcode decoded: " + result.Text + ", phase: " + workflow.Phase);
            Refresh();
        }

        private void OnObserved(BarcodeReadResult result, BarcodeViewportTransform transform, float capturedAt)
        {
            if (!paused && workflow.Phase == ProductPhase.Following) followPanel.Observe(result, transform, capturedAt);
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
            followPanel.SetProduct(null);
            workflow.StartScan();
            Refresh();
        }

        private void Refresh()
        {
            var cameraReady = readiness.CameraReady && scanner.HasCameraFrame && !paused;
            followPanel.SetCameraReady(cameraReady);
            if (cameraReady && workflow.Phase == ProductPhase.Following) scanner.SetFollowing(workflow.Selected.barcode);
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
            else if (workflow.Phase == ProductPhase.Following)
            {
                var visible = followPanel.HasVisibleTarget;
                step = visible ? "2 / 2 · 바코드 따라가는 중" : "2 / 2 · 바코드 위치 찾는 중";
                title = visible ? "상품정보를 자동으로 표시해요" : "바코드를 다시 비춰주세요";
                message = visible ? "바코드가 보이는 동안 정보창이 따라가요." : "같은 상품의 바코드 전체를\n화면 가운데에 선명하게 비춰주세요.";
                summary = visible ? "" : workflow.Selected.name + " · " + workflow.Selected.specification;
                action = "다른 상품 스캔";
                primary = true;
                attention = !visible;
                if (!scanner.HasViewportTransform)
                {
                    step = "2 / 2 · 화면 위치 준비 중";
                    title = "정보창 위치를 준비하고 있어요";
                    message = "인식한 바코드의 화면 위치를\n확인하고 있어요. 잠시 유지해 주세요.";
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
                followPanel.ClearPosition();
                followPanel.SetCameraReady(false);
            }
            nextRefresh = 0;
        }
    }
}
