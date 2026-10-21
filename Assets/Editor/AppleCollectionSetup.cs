using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppleRest.Editor
{
    public static class AppleCollectionSetup
    {
        private const string FontPath = "Assets/Fonts/AppleCounterFont.asset";
        private const string Glyphs = "사과 0123456789/";

        [MenuItem("AppleRest/Add 5 Apples and Counter UI")]
        public static void Build()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Assignment2_AppleRest/Assets"))
                throw new InvalidOperationException("This tool is restricted to Assignment2_AppleRest.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                Debug.LogWarning("Stop Play Mode and wait for compilation first.");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GameStage.unity")
            {
                Debug.LogError("Open Assets/Scenes/GameStage.unity and make it the active scene first.");
                return;
            }
            GameObject[] roots = scene.GetRootGameObjects();
            if (roots.Any(r => r.name == "AppleCollectionStage" || r.GetComponentInChildren<AppleCollection>(true) != null ||
                r.GetComponentInChildren<ApplePickup>(true) != null))
            {
                Debug.LogWarning("Apple collection already exists. No duplicate apples or UI were created.");
                return;
            }
            AppleRestPlayer[] players = roots.SelectMany(r => r.GetComponentsInChildren<AppleRestPlayer>(true)).ToArray();
            if (players.Length != 1 || !players[0].gameObject.activeInHierarchy || !players[0].enabled)
            {
                Debug.LogError("GameStage must contain exactly one active AppleRestPlayer.");
                return;
            }
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Red Apple.fbx");
            if (model == null || model.GetComponentsInChildren<Renderer>().Length == 0)
            {
                Debug.LogError("Red Apple.fbx or its renderers are missing. Source assets were not modified.");
                return;
            }
            if (TMP_Settings.instance == null || Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
            {
                Debug.LogError("Import TMP Essential Resources via Window > TextMeshPro, then run this menu again.");
                return;
            }

            TMP_FontAsset font = GetCounterFont();
            if (font == null) return;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add apples and counter UI");
            try
            {
                GameObject root = new GameObject("AppleCollectionStage");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Add apples and counter UI");
                AppleCollection collection = root.AddComponent<AppleCollection>();
                Vector3[] positions = {
                    new Vector3(-4f, 0.65f, 2f), new Vector3(0f, 0.65f, 5f),
                    new Vector3(4f, 0.65f, 2f), new Vector3(-5f, 0.65f, -4f),
                    new Vector3(5f, 0.65f, -4f)
                };
                ApplePickup[] apples = new ApplePickup[positions.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    GameObject apple = new GameObject($"Apple_{i + 1:00}");
                    apple.transform.SetParent(root.transform, false);
                    apple.transform.position = positions[i];
                    SphereCollider trigger = apple.AddComponent<SphereCollider>();
                    trigger.isTrigger = true;
                    trigger.radius = 0.55f;
                    Rigidbody body = apple.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;
                    body.constraints = RigidbodyConstraints.FreezeAll;
                    apples[i] = apple.AddComponent<ApplePickup>();
                    Assign(apples[i], "collection", collection);
                    GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                    visual.name = "AppleVisual";
                    visual.transform.SetParent(apple.transform, false);
                    Bounds bounds = BoundsOf(visual);
                    float diameter = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                    if (diameter < 0.0001f) throw new InvalidOperationException("Apple model bounds are empty.");
                    visual.transform.localScale *= 0.65f / diameter;
                    bounds = BoundsOf(visual);
                    visual.transform.position += apple.transform.position - bounds.center;
                    // Only change the scene instance, never the imported model.
                    foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                    {
                        collider.enabled = false;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                    }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                }
                SerializedObject data = new SerializedObject(collection);
                data.FindProperty("player").objectReferenceValue = players[0];
                SerializedProperty entries = data.FindProperty("apples");
                entries.arraySize = apples.Length;
                for (int i = 0; i < apples.Length; i++) entries.GetArrayElementAtIndex(i).objectReferenceValue = apples[i];
                data.ApplyModifiedPropertiesWithoutUndo();

                GameObject canvasObject = new GameObject("AppleCounterCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvasObject.transform.SetParent(root.transform, false);
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                GameObject panel = new GameObject("CounterPanel", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(canvasObject.transform, false);
                RectTransform panelRect = panel.GetComponent<RectTransform>();
                panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0, 1);
                panelRect.anchoredPosition = new Vector2(24, -24);
                panelRect.sizeDelta = new Vector2(280, 72);
                Image background = panel.GetComponent<Image>();
                background.color = new Color(0.04f, 0.07f, 0.04f, 0.85f);
                background.raycastTarget = false;
                GameObject labelObject = new GameObject("AppleCountText", typeof(RectTransform));
                labelObject.transform.SetParent(panel.transform, false);
                TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
                label.font = font;
                label.fontSize = 36;
                label.color = Color.white;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.raycastTarget = false;
                label.text = "사과 0 / 5";
                RectTransform textRect = label.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(18, 8);
                textRect.offsetMax = new Vector2(-18, -8);
                AppleCounterUI ui = labelObject.AddComponent<AppleCounterUI>();
                Assign(ui, "collection", collection);
                Assign(ui, "label", label);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log("[AppleRest] Added 5 apples and counter UI. Save GameStage with Ctrl+S. Player and Animator were not changed.");
            }
            catch (Exception error)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogException(error);
            }
        }

        private static TMP_FontAsset GetCounterFont()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (existing != null)
            {
                if (existing.HasCharacters(Glyphs)) return existing;
                Debug.LogError("Existing AppleCounterFont lacks required characters; it was not overwritten.");
                return null;
            }
            if (File.Exists(FontPath)) throw new IOException("FontPath exists but is not a TMP font asset.");
            // Bake only the counter glyphs from the installed Windows font; no source font file is copied.
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular", 48);
            if (font == null)
            {
                Debug.LogError("Malgun Gothic Regular is unavailable. Supply a Korean TMP font at " + FontPath);
                return null;
            }
            if (!font.TryAddCharacters(Glyphs))
            {
                UnityEngine.Object.DestroyImmediate(font);
                Debug.LogError("Could not bake the Korean counter characters. No scene changes were made.");
                return null;
            }
            font.name = "AppleCounterFont";
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            if (!AssetDatabase.IsValidFolder("Assets/Fonts")) AssetDatabase.CreateFolder("Assets", "Fonts");
            AssetDatabase.CreateAsset(font, FontPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (Texture2D texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }

        private static Bounds BoundsOf(GameObject visual)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void Assign(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            SerializedObject data = new SerializedObject(target);
            data.FindProperty(property).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
