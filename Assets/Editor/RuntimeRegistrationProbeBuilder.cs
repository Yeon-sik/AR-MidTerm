using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Object = UnityEngine.Object;

namespace MidtermAR.Editor
{
    // 정식 상품 조회 씬과 분리된 실기기 등록/재인식 검증용 빌드다.
    public static class RuntimeRegistrationProbeBuilder
    {
        public const string ScenePath = "Assets/Scenes/RuntimeRegistrationProbe.unity";

        public static void PrepareScene()
        {
            ProjectBuilder.PrepareImageTrackingScene();
            var scene = EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            var app = Object.FindFirstObjectByType<ProductARApp>();
            var appObject = app.gameObject;
            Object.DestroyImmediate(app);
            Object.DestroyImmediate(appObject.GetComponent<ARBarcodeScanner>());
            Object.DestroyImmediate(appObject.GetComponent<TrackedImageProductPanel>());
            Object.DestroyImmediate(appObject.GetComponent<ProductPlacement>());
            var visuals = appObject.transform.Find("Tracking Visuals");
            if (visuals != null) Object.DestroyImmediate(visuals.gameObject);
            var screen = Object.FindFirstObjectByType<ProductScreenView>();
            if (screen != null) Object.DestroyImmediate(screen.gameObject);
            var camera = Object.FindFirstObjectByType<ARCameraManager>();
            var probe = appObject.AddComponent<RuntimeImageRegistrationProbe>();
            probe.Configure(camera, Object.FindFirstObjectByType<ARTrackedImageManager>(),
                camera.GetComponent<Camera>(), Object.FindFirstObjectByType<ARSession>());
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        public static void BuildAndroid()
        {
            PrepareScene();
            ProjectBuilder.ConfigureAndroid();
            PlayerSettings.bundleVersion = "0.7.0-probe";
            // 같은 코드의 release로 -r 복구할 수 있다. 앱 삭제나 다운그레이드는 필요 없다.
            PlayerSettings.Android.bundleVersionCode = 13;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Builds/Android/RegistrationProbe.apk",
                target = BuildTarget.Android, options = BuildOptions.Development
            });
            Debug.Log($"[RegistrationProbe Build] {report.summary.result}; errors={report.summary.totalErrors}");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Registration probe build failed.");
        }
    }
}
