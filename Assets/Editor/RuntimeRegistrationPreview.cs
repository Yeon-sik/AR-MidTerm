using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MidtermAR.Editor
{
    public static class RuntimeRegistrationPreview
    {
        public static void Capture()
        {
            const string directory = "artifacts/v0.7.0/registration-ui";
            Directory.CreateDirectory(directory);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Registration preview camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -1);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.orthographic = true;
            camera.backgroundColor = new Color(.31f, .37f, .39f);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Generated/NotoSansKR SDF.asset");
            var ui = new GameObject("Registration preview", typeof(RectTransform));
            var view = ui.AddComponent<RuntimeProductRegistrationView>();
            view.Initialize(font);
            var canvas = ui.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            view.Refresh("진라면 약간 매운맛", true, false, RuntimeProductRegistrationView.ReadyInstruction);
            Render(camera, ui, 1080, 2340, directory + "/capture-form.png");
            Object.DestroyImmediate(ui);

            var panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<WorldProductPanel>(ProjectBuilder.PanelPath));
            panel.GetComponent<Canvas>().worldCamera = camera;
            var binding = new ProductImageBinding { runtimeRegistered = true, referenceImageName = "editor-preview" };
            var product = new ProductSnapshot { name = "진라면 약간 매운맛", brand = "오뚜기", barcode = "8801045014869" };
            panel.Bind(product, binding, ProductIdentity.Key(product), "Open Food Facts 정보 · PT 바코드 연결 필요");
            panel.transform.position = Vector3.zero;
            panel.transform.rotation = Quaternion.identity;
            panel.transform.localScale = Vector3.one * (.12f / ((RectTransform)panel.transform).rect.width);
            camera.orthographic = true;
            camera.orthographicSize = .09f;
            Render(camera, panel.gameObject, 1080, 1200, directory + "/off-front-no-nutrition.png");
            Debug.Log("[ProductAR Registration Preview] Editor UI only; no physical tracking evidence.");
        }

        private static void Render(Camera camera, GameObject root, int width, int height, string path)
        {
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            try
            {
                // A first render applies the target size to the camera canvas in batch mode.
                // Validate TMP after that layout pass, not against the editor's initial viewport.
                camera.Render();
                Canvas.ForceUpdateCanvases();
                string overflow = null;
                foreach (var text in root.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate(true, true);
                    if (text.isTextOverflowing)
                    {
                        overflow = "Registration text overflow: " + text.name;
                        Debug.LogWarning(overflow + "; rect=" + text.rectTransform.rect + "; fontSize=" + text.fontSize);
                    }
                    foreach (var character in text.text)
                        if (!char.IsWhiteSpace(character) && !text.font.HasCharacter(character, true, true))
                            throw new InvalidOperationException("Registration font missing glyph.");
                }
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                if (overflow != null) throw new InvalidOperationException(overflow);
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
