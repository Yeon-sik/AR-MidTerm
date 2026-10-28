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
        [SerializeField] private RuntimeProductRegistrationController frontRegistration;
        private ProductCatalog localCatalog;
        private ProductImageCatalog imageCatalog;
        private SessionImageRegistry sessionImages;
        private ProductRecognitionState recognition;
        private ProductSnapshot pendingRegistrationProduct;
        private ProductSnapshot pendingRegistrationLocalProduct;
        private string pendingRegistrationSource;
        private string registrationNotice;
        private float registrationNoticeUntil;
        private bool registrationScreenActive;
        private bool registrationSucceeded;
        private bool paused;
        private string lastDisplayedProductKey;
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
                localCatalog = catalog;
                imageCatalog = images;
                sessionImages = new SessionImageRegistry(catalog);
                imagePanel.Configure(catalog, images, sessionImages);
                recognition = new ProductRecognitionState(catalog, images, sessionImages);
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
            if (frontRegistration != null)
            {
                frontRegistration.ImageAdded += OnFrontImageAdded;
                frontRegistration.StateChanged += OnRegistrationStateChanged;
            }
        }
        private void OnDisable()
        {
            scanner.Observed -= OnObserved;
            if (frontRegistration != null)
            {
                frontRegistration.ImageAdded -= OnFrontImageAdded;
                frontRegistration.StateChanged -= OnRegistrationStateChanged;
                frontRegistration.Cancel();
            }
            ClearPendingRegistration();
            scanner.SetContinuous(false);
            recognition?.Suspend();
            imagePanel.InvalidatePose();
            barcodePanel.SetCameraReady(false);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (frontRegistration?.IsActive == true) frontRegistration.Cancel();
                else if (recognition?.DisplayProduct != null || recognition?.ConfirmedBarcode != null) RestartScan();
                else Application.Quit();
            }
            scanner.SetContinuous(recognition != null && !paused && frontRegistration?.IsActive != true
                && readiness.CameraReady && scanner.HasCameraFrame);
        }

        private void OnObserved(BarcodeReadResult result, BarcodeViewportTransform transform, float capturedAt)
        {
            if (paused || frontRegistration?.IsActive == true || recognition == null || float.IsNaN(capturedAt) || float.IsInfinity(capturedAt)
                || capturedAt > Time.unscaledTime || Time.unscaledTime - capturedAt > BarcodeFollowState.MaxAgeSeconds
                || capturedAt <= lastCapturedAt) return;
            recognition.ObserveBarcode(result, capturedAt, Time.unscaledTime);
            lastRead = result;
            lastTransform = transform;
            lastCapturedAt = capturedAt;
        }

        private void LateUpdate()
        {
            if (frontRegistration?.IsActive == true)
            {
                imagePanel.Present(false);
                barcodePanel.SetCameraReady(false);
                screen.gameObject.SetActive(false);
                return;
            }
            if (!screen.gameObject.activeSelf) screen.gameObject.SetActive(true);
            if (recognition != null)
            {
                imagePanel.SampleFrame();
                var previousRoute = recognition.Route;
                recognition.ResolveImage(imagePanel.ImageTarget, imagePanel.HasImageConflict, Time.unscaledTime);
                if (!paused)
                {
                    lookup?.Tick(recognition, Time.realtimeSinceStartupAsDouble);
                    recognition.ResolveImage(imagePanel.ImageTarget, imagePanel.HasImageConflict, Time.unscaledTime);
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
            var displayedKey = recognition.DisplayProductKey;
            if (displayedKey != null && lastDisplayedProductKey != displayedKey)
            {
                lastDisplayedProductKey = displayedKey;
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
            frontRegistration?.Cancel();
            ClearPendingRegistration();
            registrationNotice = null;
            registrationSucceeded = false;
            scanner.SetContinuous(false);
            recognition?.Reset();
            imagePanel.ResetSelection();
            barcodePanel.SetProduct(null);
            lastDisplayedProductKey = null;
            lastRead = null;
            lastCapturedAt = float.NegativeInfinity;
            nextRefresh = 0;
            Refresh();
        }

        // 등록 버튼을 누른 시점의 상품을 고정한다. 앞면으로 돌려 바코드가 사라져도 대상은 바뀌지 않는다.
        public void BeginFrontRegistration()
        {
            if (paused || frontRegistration == null || !readiness.CameraReady || !scanner.HasCameraFrame
                || !CanOfferFrontRegistration(recognition, imageCatalog, sessionImages)) return;
            var product = recognition.BarcodeDisplayProduct;
            var key = ProductIdentity.Key(product);
            if (!frontRegistration.CanBegin(key)) return;
            pendingRegistrationProduct = SessionImageRegistry.CopyProduct(product);
            localCatalog.TryFindById(product.catalogProductId, out pendingRegistrationLocalProduct);
            pendingRegistrationSource = recognition.SourceBadge;
            if (!frontRegistration.Begin(key, product.name)) { ClearPendingRegistration(); return; }
            recognition.Suspend();
            scanner.SetContinuous(false);
            imagePanel.InvalidatePose();
            barcodePanel.SetCameraReady(false);
            lastRead = null;
            lastCapturedAt = float.NegativeInfinity;
            lastDisplayedProductKey = null;
            registrationNotice = null;
            registrationSucceeded = false;
            screen.gameObject.SetActive(false);
        }

        public static bool CanOfferFrontRegistration(ProductRecognitionState state, ProductImageCatalog images,
            SessionImageRegistry registry)
        {
            if (state?.Route != ProductDisplayRoute.Barcode2D || state.BarcodeDisplayProduct == null
                || string.IsNullOrEmpty(ProductIdentity.Key(state.BarcodeDisplayProduct))) return false;
            if (state.OnlineEnabled && state.LookupResult?.Status != LookupStatus.Matched
                && state.LookupResult?.Status != LookupStatus.ExternalFound) return false;
            if (registry?.ContainsProduct(state.BarcodeDisplayProduct) == true) return false;
            return images == null || !images.TryFind(state.BarcodeDisplayProduct, out var binding) || binding.developmentOnly;
        }

        private void OnFrontImageAdded(string referenceImageName)
        {
            if (paused || pendingRegistrationProduct == null) return;
            var registered = sessionImages.TryAdd(referenceImageName, pendingRegistrationProduct,
                pendingRegistrationLocalProduct, pendingRegistrationSource, out _);
            registrationSucceeded = registered;
            registrationNotice = registered ? "앞면을 등록했어요. 같은 앞면을 비추고 휴대폰을 천천히 움직여주세요."
                : "앞면 연결을 완료하지 못했어요. 앱을 다시 실행한 뒤 등록해주세요.";
            registrationNoticeUntil = Time.unscaledTime + 25f;
            Debug.Log("[ProductAR Registration] product binding=" + registered + "; image=" + referenceImageName);
            ClearPendingRegistration();
            nextRefresh = 0;
        }

        private void OnRegistrationStateChanged()
        {
            if (frontRegistration?.IsActive == true)
            { registrationScreenActive = true; return; }
            // 취소 뒤 늦게 끝난 native 작업은 새로운 바코드 선택을 해제하지 않는다.
            if (!registrationScreenActive) return;
            registrationScreenActive = false;
            if (registrationNotice == null)
            {
                registrationSucceeded = false;
                registrationNotice = frontRegistration?.Status;
                registrationNoticeUntil = Time.unscaledTime + 8f;
            }
            ClearPendingRegistration();
            recognition?.Suspend();
            imagePanel.InvalidatePose();
            screen.gameObject.SetActive(true);
            nextRefresh = 0;
        }

        private void ClearPendingRegistration()
        { pendingRegistrationProduct = null; pendingRegistrationLocalProduct = null; pendingRegistrationSource = null; }

        private void Refresh()
        {
            var ready = readiness.CameraReady && scanner.HasCameraFrame && !paused;
            RefreshScreen(ready);
        }

        private void RefreshScreen(bool ready)
        {
            var step = "상품 인식";
            var title = "앞면이나 바코드를 비춰주세요";
            var message = "상품을 화면 안에 선명하게 보여주세요.";
            var summary = "";
            var action = "다른 상품 스캔";
            var primary = recognition?.DisplayProduct != null || recognition?.ConfirmedBarcode != null;
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
                message = "한 상품만 비춰주세요.";
                primary = true; attention = true;
            }
            else if (recognition.Route == ProductDisplayRoute.Image3D)
            {
                step = "앞면 · 3D 정보";
                title = imagePanel.HasVisibleTarget ? "앞면에 상품정보를 표시해요" : "앞면 위치를 확인하고 있어요";
                message = ProductInfoFormatter.Honesty(recognition.DisplayProduct, imagePanel.Binding);
                primary = true;
            }
            else if (recognition.Route == ProductDisplayRoute.Barcode2D)
            {
                step = "바코드 · 2D 정보";
                title = "바코드로 상품을 확인했어요";
                var frontHint = CanOfferFrontRegistration(recognition, imageCatalog, sessionImages)
                    && frontRegistration?.HasProcessedProduct(ProductIdentity.Key(recognition.BarcodeDisplayProduct)) == true
                    ? RuntimeProductRegistrationController.RestartNotice
                    : ProductInfoFormatter.FrontHint(recognition.BarcodeBadge(recognition.BarcodeDisplayProduct));
                message = ProductInfoFormatter.CompactSource(recognition.SourceBadge) + "\n" + frontHint;
                if (!string.IsNullOrEmpty(recognition.LookupResult?.Detail)) message += "\n" + recognition.LookupResult.Detail;
                if (!barcodePanel.HasVisibleTarget) message += "\n바코드를 선명하게 비춰주세요.";
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
            else if (registrationNotice != null && Time.unscaledTime < registrationNoticeUntil)
            { title = registrationSucceeded ? "등록한 앞면을 비춰주세요" : "앞면 등록"; message = registrationNotice; }
            var canRegister = ready && frontRegistration != null && frontRegistration.CanBegin(ProductIdentity.Key(recognition?.BarcodeDisplayProduct))
                && CanOfferFrontRegistration(recognition, imageCatalog, sessionImages);
            screen.Show(step, title, message, summary, action, primary, canRegister, false, error, attention, moveAction: "앞면 등록",
                secondaryAction: recognition?.DisplayProduct != null && ready);
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            if (value) { frontRegistration?.Cancel(); ClearPendingRegistration(); }
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
