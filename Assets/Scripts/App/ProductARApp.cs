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
        [SerializeField] private ProductPlacement placement;
        [SerializeField] private ProductScreenView screen;
        private ProductWorkflow workflow;
        private float nextRefresh;
        private float noticeUntil;
        private string notice;
        private bool paused;

        private void Awake()
        {
            try
            {
                var catalog = new ProductCatalog(catalogJson.text);
                workflow = new ProductWorkflow(catalog);
                Debug.Log("[ProductAR] Local demo catalog loaded: " + catalog.Count + ", mapping: demo-only, physical decode: pending device test");
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
            placement.Placed += OnPlaced;
            placement.Notice += OnNotice;
        }
        private void OnDisable()
        {
            scanner.Decoded -= OnDecoded;
            scanner.TimedOut -= OnScanTimedOut;
            placement.Placed -= OnPlaced;
            placement.Notice -= OnNotice;
            scanner.SetScanning(false);
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
            scanner.SetScanning(false);
            placement.SetProduct(workflow.Selected);
            Debug.Log("[ProductAR] Barcode decoded: " + result.Text + " / " + result.Format + ", phase: " + workflow.Phase);
            Refresh();
        }
        private void OnScanTimedOut() { workflow.ScanTimedOut(); Refresh(); }
        private void OnPlaced() { workflow.MarkPlaced(); noticeUntil = 0; Refresh(); }
        private void OnNotice(string message) { notice = message; noticeUntil = Time.unscaledTime + 4f; Refresh(); }

        public void PrimaryAction()
        {
            if (readiness.PermissionBlocked) { readiness.OpenSettings(); return; }
            if (!readiness.CameraReady) { readiness.Retry(); return; }
            RestartScan();
        }
        public void RestartScan()
        {
            scanner.SetScanning(false);
            placement.SetProduct(null);
            workflow.StartScan();
            noticeUntil = 0;
            Refresh();
        }
        public void Reposition() { if (!placement.IsBusy) { workflow.RequestPlacement(); Refresh(); } }
        public void DeletePanel()
        {
            placement.ClearPlacement();
            workflow.RequestPlacement();
            Refresh();
        }

        private void Refresh()
        {
            var ready = readiness.IsReady && !paused;
            var cameraReady = readiness.CameraReady && scanner.HasCameraFrame && !paused;
            scanner.SetScanning(cameraReady && workflow.Phase == ProductPhase.Scanning);
            placement.AllowPlacement = ready && workflow.CanPlace;
            var summary = workflow.Selected == null ? "시연 상품 · 더단백 크런치바 초코 40g" : workflow.Selected.brand + " · " + workflow.Selected.specification + "\n" + workflow.Selected.name;
            var title = "바코드를 비춰주세요";
            var message = "바코드 전체와 양쪽 여백이 보이게 맞춰주세요.\n제품을 움직이지 않고 잠시 기다려주세요.";
            var action = "다시 스캔";
            var error = false;
            if (workflow.Phase == ProductPhase.DataUnavailable)
            { title = "상품정보를 준비하지 못했습니다"; message = "시연 데이터 파일을 확인해야 합니다."; error = true; }
            else if (!cameraReady)
            {
                title = readiness.PermissionBlocked ? "카메라 권한이 필요합니다" : "AR 카메라 준비 중";
                message = readiness.Status;
                action = readiness.PermissionBlocked ? "설정 열기" : "다시 확인";
                error = readiness.PermissionBlocked;
            }
            else if (workflow.Phase == ProductPhase.UnknownBarcode)
            { title = "시연 데이터에 없는 상품입니다"; message = "등록된 상품정보가 없습니다.\n다른 상품의 바코드를 스캔해 주세요."; error = true; }
            else if (workflow.Phase == ProductPhase.ScanFailed)
            { title = "바코드를 읽지 못했습니다"; message = "반사와 초점을 확인하고\n바코드 전체가 보이도록 다시 시도해 주세요."; error = true; }
            else if (placement.IsBusy)
            { title = "정보판을 고정하고 있습니다"; message = "휴대폰을 잠시 움직이지 말아주세요."; }
            else if (workflow.Phase == ProductPhase.AwaitingPlacement)
            {
                title = ready && placement.HasTrackedPlane ? "상품 옆을 눌러 배치하세요" : "책상 표면을 천천히 비춰주세요";
                message = ready && placement.HasTrackedPlane ? "표시된 수평 표면을 누르면 정보판이 놓입니다.\n상품을 옮기면 정보판도 재배치해 주세요." : "상품 옆의 책상을 여러 각도에서 비추면\n정보판을 놓을 표면을 찾을 수 있습니다.";
            }
            else if (workflow.Phase == ProductPhase.Placed)
            {
                title = placement.PlacementTracking ? "정보판을 공간에 배치했습니다" : "공간 추적을 복구하고 있습니다";
                message = placement.PlacementTracking ? "휴대폰을 움직이며 정보를 확인하세요.\n상품을 옮기면 재배치를 눌러주세요." : "잘못된 위치를 표시하지 않도록 정보판을 숨겼습니다.\n주변을 천천히 비추거나 다시 배치해 주세요.";
            }
            if (cameraReady && Time.unscaledTime < noticeUntil) message = notice;
            screen.Show(title, message, summary, action, !readiness.IsInitializing && !placement.IsBusy && workflow.Phase != ProductPhase.DataUnavailable,
                ready && workflow.Phase == ProductPhase.Placed && !placement.IsBusy, placement.HasPlacement && !placement.IsBusy, error);
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            if (value && workflow != null)
            {
                scanner.SetScanning(false);
                placement.ClearPlacement();
                workflow.RequestPlacement();
            }
            nextRefresh = 0;
        }
    }
}
