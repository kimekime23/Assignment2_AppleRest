using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppleRest.Editor
{
    public static class StageDeadlineSetup
    {
        private const string FontPath = "Assets/Fonts/DeadlineFont.asset";
        private const string Glyphs = "남은 시간 0123456789초해가 져버렸어요다시 하기";

        [MenuItem("AppleRest/Add Timer and Sunset Failure")]
        public static void Build()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Assignment2_AppleRest/Assets"))
                throw new InvalidOperationException("Restricted to Assignment2_AppleRest.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GameStage.unity")
                throw new InvalidOperationException("Open GameStage and make it active first.");
            GameObject[] roots = scene.GetRootGameObjects();
            if (Find<StageDeadline>(roots).Any())
            { Debug.LogWarning("Deadline already exists; existing settings were preserved."); return; }
            AppleRestPlayer player = One<AppleRestPlayer>(roots);
            StumpInteraction interaction = One<StumpInteraction>(roots);
            SitClearSequence sequence = One<SitClearSequence>(roots);
            Light[] lights = Find<Light>(roots).Where(l => l.type == LightType.Directional && l.isActiveAndEnabled).ToArray();
            Light sun = lights.FirstOrDefault(l => l == RenderSettings.sun);
            if (sun == null && lights.Length == 1) sun = lights[0];
            if (sun == null) throw new InvalidOperationException("Choose the scene Sun Source, or keep exactly one active Directional Light.");
            EventSystem[] events = Find<EventSystem>(roots);
            if (events.Length > 1 || (events.Length == 1 &&
                (!events[0].isActiveAndEnabled || events[0].GetComponent<InputSystemUIInputModule>() == null ||
                 !events[0].GetComponent<InputSystemUIInputModule>().isActiveAndEnabled)))
                throw new InvalidOperationException("Use one active EventSystem with InputSystemUIInputModule before running this tool.");
            if (TMP_Settings.instance == null || Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
                throw new InvalidOperationException("Import TMP Essential Resources first.");
            TMP_FontAsset font = GetFont();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add deadline and sunset failure");
            try
            {
                GameObject root = new GameObject("StageDeadline");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Add deadline");
                StageDeadline deadline = root.AddComponent<StageDeadline>();
                GameObject canvasObject = Child("DeadlineCanvas", root.transform, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                GameObject timer = Child("TimerPanel", canvasObject.transform, typeof(Image));
                RectTransform tr = timer.GetComponent<RectTransform>();
                tr.anchorMin = tr.anchorMax = tr.pivot = Vector2.one;
                tr.anchoredPosition = new Vector2(-24, -24);
                tr.sizeDelta = new Vector2(350, 72);
                timer.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 0.85f);
                timer.GetComponent<Image>().raycastTarget = false;
                TMP_Text timerLabel = Label("RemainingTime", timer.transform, font, "남은 시간 45초", 34);
                Stretch(timerLabel.rectTransform, 12);

                GameObject failure = Child("FailurePanel", canvasObject.transform, typeof(Image));
                Stretch(failure.GetComponent<RectTransform>(), 0);
                failure.GetComponent<Image>().color = new Color(0.02f, 0.015f, 0.03f, 0.88f);
                TMP_Text message = Label("FailureMessage", failure.transform, font, "해가 져버렸어요", 56);
                Center(message.rectTransform, new Vector2(1100, 120), new Vector2(0, 70));
                GameObject buttonObject = Child("RetryButton", failure.transform, typeof(Image), typeof(Button));
                Center(buttonObject.GetComponent<RectTransform>(), new Vector2(320, 90), new Vector2(0, -90));
                buttonObject.GetComponent<Image>().color = new Color(0.27f, 0.38f, 0.22f);
                Button button = buttonObject.GetComponent<Button>();
                button.targetGraphic = buttonObject.GetComponent<Image>();
                TMP_Text buttonText = Label("RetryLabel", buttonObject.transform, font, "다시 하기", 38);
                Stretch(buttonText.rectTransform, 6);
                failure.SetActive(false);
                Assign(deadline, "player", player);
                Assign(deadline, "interaction", interaction);
                Assign(deadline, "sitSequence", sequence);
                Assign(deadline, "sunlight", sun);
                Assign(deadline, "timerLabel", timerLabel);
                Assign(deadline, "failurePanel", failure);
                Assign(deadline, "retryButton", button);
                if (events.Length == 0)
                {
                    GameObject eventObject = new GameObject("DeadlineEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    SceneManager.MoveGameObjectToScene(eventObject, scene);
                    Undo.RegisterCreatedObjectUndo(eventObject, "Add UI EventSystem");
                    eventObject.transform.SetParent(root.transform, false);
                    eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                }
                EditorSceneManager.MarkSceneDirty(scene);
                // Reload requires a loadable scene. Preserve all existing build entries and their order.
                EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
                if (!buildScenes.Any(s => s.path == scene.path && s.enabled))
                {
                    if (buildScenes.Any(s => s.path == scene.path))
                        buildScenes = buildScenes.Select(s => s.path == scene.path ? new EditorBuildSettingsScene(s.path, true) : s).ToArray();
                    else buildScenes = buildScenes.Concat(new[] { new EditorBuildSettingsScene(scene.path, true) }).ToArray();
                    EditorBuildSettings.scenes = buildScenes;
                }
                Undo.CollapseUndoOperations(group);
                Debug.Log("[AppleRest] Deadline added. Save GameStage. Set StageDeadline > Time Limit as needed. GameStage enabled in the shared build scene list for Retry. Existing gameplay and SitAnchor unchanged.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private static TMP_FontAsset GetFont()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null)
            {
                if (!font.HasCharacters(Glyphs)) throw new InvalidOperationException("DeadlineFont lacks required glyphs.");
                return font;
            }
            if (File.Exists(FontPath)) throw new IOException("DeadlineFont path is already occupied.");
            font = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular", 48);
            if (font == null || !font.TryAddCharacters(Glyphs))
                throw new InvalidOperationException("Cannot bake DeadlineFont from Malgun Gothic Regular.");
            font.name = "DeadlineFont";
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            if (!AssetDatabase.IsValidFolder("Assets/Fonts")) AssetDatabase.CreateFolder("Assets", "Fonts");
            AssetDatabase.CreateAsset(font, FontPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (Texture2D texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }

        private static T[] Find<T>(GameObject[] roots) where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static T One<T>(GameObject[] roots) where T : Component
        {
            T[] found = Find<T>(roots);
            if (found.Length != 1) throw new InvalidOperationException("Expected exactly one " + typeof(T).Name);
            return found[0];
        }
        private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            SerializedObject data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static GameObject Child(string name, Transform parent, params Type[] types)
        {
            GameObject obj = new GameObject(name, new[] { typeof(RectTransform) }.Concat(types).ToArray());
            obj.transform.SetParent(parent, false);
            return obj;
        }
        private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text, float size)
        {
            TMP_Text label = Child(name, parent, typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            label.font = font; label.text = text; label.fontSize = size;
            label.color = Color.white; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            return label;
        }
        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset);
        }
        private static void Center(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
        }
    }
}
