using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidtermAR
{
    // 두 인식 경로의 결과를 메인 스레드에서 합쳐 한 종류의 정보판만 표시한다.
    public sealed class ProductARApp : MonoBehaviour
    {
        [SerializeField] private TextAsset catalogJson;
        [SerializeField] private TextAsset imageBindingsJson;
        [SerializeField] private TextAsset remoteLookupJson;
        [SerializeField] private ARReadiness readiness;
        [SerializeField] private ARBarcodeScanner scanner;
        [SerializeField] private ProductScreenView screen;
        [SerializeField] private TrackedImageProductPanel imagePanel;
        [SerializeField] private BarcodeProductPanel barcodePanel;
        private ProductRecognitionState recognition;
        private bool paused;
        private string lastProductId;
        private float nextRefresh;
        private BarcodeReadResult lastRead;
        private BarcodeViewportTransform lastTransform;
        private float lastCapturedAt = float.NegativeInfinity;
        private LookupCoordinator lookup;

        private void Awake()
        {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            Debug.developerConsoleEnabled = false;
#endif
            try
            {
                var catalog = new ProductCatalog(catalogJson.text);
                var images = new ProductImageCatalog(imageBindingsJson.text, catalog);
                images.ValidateLibrary(imagePanel.ReferenceLibrary);
                imagePanel.Configure(catalog, images);
                recognition = new ProductRecognitionState(catalog, images);
                var config = RemoteLookupConfig.Parse(remoteLookupJson != null ? remoteLookupJson.text : null);
                if (config.PtReady)
                {
                    var http = new UnityLookupHttp();
                    lookup = new LookupCoordinator(new ProductLookup(new PtBarcodeClient(config, http),
                        new OffProductClient(config, http, Application.version, () => Time.realtimeSinceStartupAsDouble),
                        new PublicNutritionClient(config, http), catalog));
                    recognition.EnableOnlineLookup();
                }
                Debug.Log("[ProductAR Lookup] mode=" + (lookup == null ? "local-only" : "online"));
#if DEVELOPMENT_BUILD && UNITY_ANDROID && !UNITY_EDITOR
                DevelopmentLookupCheck.RunIfRequested(config);
#endif
                Debug.Log("[ProductAR v5] Local catalog loaded: " + catalog.Count + "; snapshot=" + catalog.SnapshotDate);
            }
            catch (Exception error)
            {
                recognition = null;
                Debug.LogError("[ProductAR v5] Catalog load: " + error.Message);
            }
        }

        private void OnEnable()
        {
            scanner.Observed += OnObserved;
            imagePanel.RescanRequested += RestartScan;
        }
        private void OnDisable()
        {
            scanner.Observed -= OnObserved;
            imagePanel.RescanRequested -= RestartScan;
            scanner.SetContinuous(false);
            recognition?.Suspend();
            imagePanel.InvalidatePose();
            barcodePanel.SetCameraReady(false);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (recognition?.Selected != null || recognition?.ConfirmedBarcode != null) RestartScan();
                else Application.Quit();
            }
            scanner.SetContinuous(recognition != null && !paused && readiness.CameraReady && scanner.HasCameraFrame);
        }

        private void OnObserved(BarcodeReadResult result, BarcodeViewportTransform transform, float capturedAt)
        {
            if (paused || recognition == null || float.IsNaN(capturedAt) || float.IsInfinity(capturedAt)
                || capturedAt > Time.unscaledTime || Time.unscaledTime - capturedAt > BarcodeFollowState.MaxAgeSeconds
                || capturedAt <= lastCapturedAt) return;
            recognition.ObserveBarcode(result, capturedAt, Time.unscaledTime);
            lastRead = result;
            lastTransform = transform;
            lastCapturedAt = capturedAt;
        }

        private void LateUpdate()
        {
            if (recognition != null)
            {
                imagePanel.SampleFrame();
                var previousRoute = recognition.Route;
                recognition.Resolve(imagePanel.CurrentProduct, imagePanel.HasImageConflict, Time.unscaledTime);
                if (!paused)
                {
                    lookup?.Tick(recognition, Time.realtimeSinceStartupAsDouble);
                    recognition.Resolve(imagePanel.CurrentProduct, imagePanel.HasImageConflict, Time.unscaledTime);
                }
                if (previousRoute != recognition.Route)
                {
                    nextRefresh = 0; // 추적 소실 안내는 정기 갱신을 기다리지 않고 같은 프레임에 반영한다.
                    Debug.Log("[ProductAR v5] Route: " + previousRoute + " -> " + recognition.Route);
                }
                Present();
            }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.15f;
            Refresh();
        }

        private void Present()
        {
            // 선택은 소실 즉시 해제하되, 숨긴 정보판의 탭은 같은 상품 재획득 때 재사용한다.
            if (recognition.Selected != null && lastProductId != recognition.Selected.catalogProductId)
            {
                lastProductId = recognition.Selected?.catalogProductId;
                imagePanel.SetProduct(null, null);
                barcodePanel.SetProduct(null);
            }
            var ready = !paused && readiness.CameraReady && scanner.HasCameraFrame;
            imagePanel.Present(ready && recognition.Route == ProductDisplayRoute.Image3D);
            var barcodeAllowed = ready && recognition.Route == ProductDisplayRoute.Barcode2D;
            if (barcodeAllowed)
            {
                var product = recognition.BarcodeDisplayProduct;
                barcodePanel.SetProduct(product, recognition.BarcodeBadge(product), recognition.SourceBadge, recognition.LookupResult?.Detail);
                barcodePanel.SetCameraReady(true);
                barcodePanel.Observe(lastRead, lastTransform, lastCapturedAt);
            }
            else barcodePanel.SetCameraReady(false);
        }

        public void PrimaryAction()
        {
            if (readiness.PermissionBlocked) { readiness.OpenSettings(); return; }
            if (!readiness.CameraReady || !scanner.HasCameraFrame) { readiness.Retry(); return; }
            RestartScan();
        }
        public void RestartScan()
        {
            scanner.SetContinuous(false);
            recognition?.Reset();
            imagePanel.ResetSelection();
            barcodePanel.SetProduct(null);
            lastProductId = null;
            lastRead = null;
            lastCapturedAt = float.NegativeInfinity;
            nextRefresh = 0;
            Refresh();
        }

        private void Refresh()
        {
            var ready = readiness.CameraReady && scanner.HasCameraFrame && !paused;
            var step = "상품 인식";
            var title = "앞면이나 바코드를 비춰주세요";
            var message = "앞면은 3D, 바코드는 2D로 정보를 표시합니다.";
            var summary = "";
            var action = "다른 상품 스캔";
            var primary = recognition?.Selected != null || recognition?.ConfirmedBarcode != null;
            var error = false;
            var attention = false;
            if (recognition == null)
            { title = "상품정보를 불러올 수 없어요"; message = "앱을 종료한 뒤 다시 실행해 주세요."; error = true; primary = false; }
            else if (!ready)
            {
                title = readiness.PermissionBlocked ? "카메라 사용을 허용해 주세요" : "카메라를 준비하고 있어요";
                message = readiness.Status;
                action = readiness.PermissionBlocked ? "설정 열기" : "다시 확인";
                primary = !paused && !readiness.IsInitializing;
                error = readiness.PermissionBlocked;
            }
            else if (recognition.Route == ProductDisplayRoute.Conflict)
            {
                title = recognition.LookupResult?.Status == LookupStatus.Conflict ? "연결 확인 필요" : "인식 결과가 일치하지 않아요";
                message = "정보판을 숨겼습니다. 한 상품만 비춰주세요.";
                primary = true; attention = true;
            }
            else if (recognition.Route == ProductDisplayRoute.Image3D)
            {
                step = "앞면 · 3D 정보";
                title = imagePanel.HasVisibleTarget ? "앞면에 상품정보를 표시해요" : "앞면 위치를 확인하고 있어요";
                message = "영양·가격 탭으로 정보를 바꿔보세요.";
                if (string.IsNullOrEmpty(recognition.Selected.barcode)) message += "\n바코드 미연결";
                if (imagePanel.Binding?.developmentOnly == true) message += "\n개발용 카드 연결 · 상품 포장 인식 아님";
                primary = true;
            }
            else if (recognition.Route == ProductDisplayRoute.Barcode2D)
            {
                step = "바코드 · 2D 정보";
                title = "바코드로 상품을 확인했어요";
                message = recognition.SourceBadge + "\n" + recognition.BarcodeBadge(recognition.BarcodeDisplayProduct);
                if (!string.IsNullOrEmpty(recognition.LookupResult?.Detail)) message += "\n" + recognition.LookupResult.Detail;
                if (!barcodePanel.HasVisibleTarget) message += "\n화면 위치를 확인하려면 바코드를 유지해 주세요.";
                primary = true;
            }
            else if (recognition.Route == ProductDisplayRoute.UnknownBarcode)
            {
                step = "상품 등록 확인";
                title = recognition.OnlineEnabled ? recognition.LookupResult?.Badge ?? "PT 조회 중…" : "등록이 필요한 제품";
                message = recognition.OnlineEnabled ? recognition.LookupMessage : recognition.UnknownMessage;
                primary = true; attention = true;
            }
            else if (recognition.ConfirmationCount > 0)
            { title = "바코드를 확인하고 있어요"; message = "같은 번호를 3회 확인할 때까지 유지해 주세요."; }
            screen.Show(step, title, message, summary, action, primary, false, false, error, attention,
                secondaryAction: recognition?.Selected != null && ready);
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            recognition?.Suspend();
            scanner.SetContinuous(false);
            imagePanel.InvalidatePose();
            barcodePanel.SetCameraReady(false);
            lastRead = null;
            lastCapturedAt = float.NegativeInfinity;
            nextRefresh = 0;
        }
        private void OnDestroy() { lookup?.Dispose(); }
    }
}
