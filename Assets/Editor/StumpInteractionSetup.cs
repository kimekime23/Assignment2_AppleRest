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
    public static class StumpInteractionSetup
    {
        private const string FontPath = "Assets/Fonts/StumpPromptFont.asset";

        [MenuItem("AppleRest/Add Stump Interaction and Prompt")]
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
                Debug.LogError("Open GameStage and make it the active scene first.");
                return;
            }
            GameObject[] roots = scene.GetRootGameObjects();
            if (roots.Any(r => r.name == "StumpInteractionStage" || r.GetComponentInChildren<StumpInteraction>(true) != null))
            {
                Debug.LogWarning("Stump interaction already exists; no duplicate objects were created.");
                return;
            }
            AppleRestPlayer[] players = roots.SelectMany(r => r.GetComponentsInChildren<AppleRestPlayer>(true)).ToArray();
            AppleCollection[] collections = roots.SelectMany(r => r.GetComponentsInChildren<AppleCollection>(true)).ToArray();
            if (players.Length != 1 || !players[0].isActiveAndEnabled || collections.Length != 1 ||
                !collections[0].isActiveAndEnabled || collections[0].TotalCount != 5)
            {
                Debug.LogError("GameStage needs one active player and one AppleCollection configured with 5 apples.");
                return;
            }
            SerializedObject existingCollection = new SerializedObject(collections[0]);
            if (existingCollection.FindProperty("player").objectReferenceValue != players[0])
            {
                Debug.LogError("AppleCollection references a different player. No existing settings were changed.");
                return;
            }
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Mossy Tree Stump.fbx");
            if (model == null || model.GetComponentsInChildren<Renderer>().Length == 0)
            {
                Debug.LogError("Mossy Tree Stump.fbx or its renderers are missing. Source assets were not modified.");
                return;
            }
            if (TMP_Settings.instance == null || Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
            {
                Debug.LogError("Import TMP Essential Resources via Window > TextMeshPro first.");
                return;
            }
            TMP_FontAsset font = GetPromptFont();
            if (font == null) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add stump interaction and prompt");
            try
            {
                GameObject root = new GameObject("StumpInteractionStage");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Add stump interaction and prompt");
                GameObject stump = new GameObject("RestStump");
                stump.transform.SetParent(root.transform, false);
                stump.transform.position = new Vector3(0f, 0f, 9f);
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                visual.name = "StumpVisual";
                visual.transform.SetParent(stump.transform, false);
                Bounds bounds = BoundsOf(visual);
                float width = Mathf.Max(bounds.size.x, bounds.size.z);
                if (width < 0.0001f) throw new InvalidOperationException("Stump model bounds are empty.");
                visual.transform.localScale *= 1.8f / width;
                bounds = BoundsOf(visual);
                visual.transform.position += new Vector3(stump.transform.position.x - bounds.center.x,
                    stump.transform.position.y - bounds.min.y, stump.transform.position.z - bounds.center.z);
                foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                bounds = BoundsOf(visual);
                BoxCollider solid = stump.AddComponent<BoxCollider>();
                solid.center = stump.transform.InverseTransformPoint(bounds.center);
                solid.size = bounds.size;
                SphereCollider trigger = stump.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.center = Vector3.up * 0.8f;
                trigger.radius = 2.2f;
                Rigidbody body = stump.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.constraints = RigidbodyConstraints.FreezeAll;
                StumpInteraction interaction = stump.AddComponent<StumpInteraction>();

                GameObject canvasObject = new GameObject("StumpPromptCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvasObject.transform.SetParent(root.transform, false);
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 20;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                GameObject panel = new GameObject("InteractionPromptPanel", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(canvasObject.transform, false);
                RectTransform panelRect = panel.GetComponent<RectTransform>();
                panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0f);
                panelRect.anchoredPosition = new Vector2(0f, 40f);
                panelRect.sizeDelta = new Vector2(760f, 84f);
                Image background = panel.GetComponent<Image>();
                background.color = new Color(0.04f, 0.07f, 0.04f, 0.85f);
                background.raycastTarget = false;
                GameObject textObject = new GameObject("InteractionPromptText", typeof(RectTransform));
                textObject.transform.SetParent(panel.transform, false);
                TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
                label.font = font;
                label.fontSize = 32;
                label.color = Color.white;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                label.text = StumpInteraction.LockedPrompt;
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(16, 8);
                label.rectTransform.offsetMax = new Vector2(-16, -8);
                Assign(interaction, "player", players[0]);
                Assign(interaction, "collection", collections[0]);
                Assign(interaction, "promptPanel", panel);
                Assign(interaction, "promptLabel", label);
                panel.SetActive(false);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("[AppleRest] Added stump trigger and prompt. Save GameStage with Ctrl+S. Sit/Clear are not implemented.");
            }
            catch (Exception error)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(error);
            }
        }

        private static TMP_FontAsset GetPromptFont()
        {
            string glyphs = StumpInteraction.LockedPrompt + StumpInteraction.ReadyPrompt;
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (existing != null)
            {
                if (existing.HasCharacters(glyphs)) return existing;
                Debug.LogError("StumpPromptFont is missing required glyphs; it was not overwritten.");
                return null;
            }
            if (File.Exists(FontPath)) throw new IOException("StumpPromptFont exists but is not a TMP font asset.");
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular", 48);
            if (font == null)
            {
                Debug.LogError("Malgun Gothic Regular is unavailable. Supply a Korean TMP font at " + FontPath);
                return null;
            }
            if (!font.TryAddCharacters(glyphs))
            {
                UnityEngine.Object.DestroyImmediate(font);
                Debug.LogError("Could not bake prompt glyphs. No scene changes were made.");
                return null;
            }
            font.name = "StumpPromptFont";
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

        private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            SerializedObject data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
