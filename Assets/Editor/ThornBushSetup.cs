using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AppleRest.Editor
{
    public static class ThornBushSetup
    {
        private const string PrefabPath = "Assets/Prefabs/ThornBush.prefab";
        private const string MaterialPath = "Assets/Prefabs/M_ThornBush.mat";
        private const string FontPath = "Assets/Fonts/ThornPenaltyFont.asset";

        [MenuItem("AppleRest/Configure 45 Second Timer and ThornBush")]
        public static void Configure()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Assignment2_AppleRest/Assets"))
                throw new InvalidOperationException("Restricted to Assignment2_AppleRest.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GameStage.unity")
                throw new InvalidOperationException("Open GameStage first.");
            StageDeadline[] timers = FindTimers(scene);
            if (timers.Length == 0) { StageDeadlineSetup.Build(); timers = FindTimers(scene); }
            if (timers.Length != 1) throw new InvalidOperationException("Expected exactly one StageDeadline.");
            StageDeadline timer = timers[0];
            SerializedObject data = new SerializedObject(timer);
            TMP_Text timeLabel = data.FindProperty("timerLabel").objectReferenceValue as TMP_Text;
            Canvas canvas = timeLabel != null ? timeLabel.GetComponentInParent<Canvas>() : null;
            if (canvas == null) throw new InvalidOperationException("Timer UI is missing.");
            TMP_FontAsset font = GetFont();
            FinishPrefabMaterials();

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure 45 seconds and thorn penalty UI");
            try
            {
                TMP_Text popup = data.FindProperty("penaltyLabel").objectReferenceValue as TMP_Text;
                if (popup == null)
                {
                    GameObject obj = new GameObject("ThornPenaltyText", typeof(RectTransform), typeof(TextMeshProUGUI));
                    SceneManager.MoveGameObjectToScene(obj, scene);
                    Undo.RegisterCreatedObjectUndo(obj, "Create thorn penalty UI");
                    obj.transform.SetParent(canvas.transform, false);
                    popup = obj.GetComponent<TextMeshProUGUI>();
                    popup.font = font;
                    popup.fontSize = 34;
                    popup.alignment = TextAlignmentOptions.MidlineRight;
                    popup.color = new Color(1f, 0.15f, 0.12f);
                    popup.raycastTarget = false;
                    popup.text = "-3초";
                    popup.rectTransform.anchorMin = popup.rectTransform.anchorMax = popup.rectTransform.pivot = Vector2.one;
                    popup.rectTransform.anchoredPosition = new Vector2(-390, -45);
                    popup.rectTransform.sizeDelta = new Vector2(160, 60);
                    obj.SetActive(false);
                }
                Undo.RecordObject(timer, "Set timer to 45 seconds");
                data.Update();
                data.FindProperty("timeLimit").floatValue = 45f;
                data.FindProperty("penaltyLabel").objectReferenceValue = popup;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(timer);
                Undo.RecordObject(timeLabel, "Set initial timer text");
                timeLabel.text = "남은 시간 45초";
                PrefabUtility.RecordPrefabInstancePropertyModifications(timeLabel);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("[AppleRest] Timer set to 45 seconds and penalty UI connected. Save GameStage. Drag Assets/Prefabs/ThornBush.prefab into the scene yourself. Forest and SitAnchor unchanged.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        private static StageDeadline[] FindTimers(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<StageDeadline>(true)).ToArray();

        private static void FinishPrefabMaterials()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null || AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                throw new InvalidOperationException("ThornBush prefab or copied material is missing.");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (root.GetComponent<ThornBush>() == null) throw new InvalidOperationException("ThornBush component is missing.");
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                }
                // Saves overrides on this new variant only, never on the source FBX or Forest prefab.
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static TMP_FontAsset GetFont()
        {
            const string glyphs = "-0123456789.초";
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null)
            {
                if (!font.HasCharacters(glyphs)) throw new InvalidOperationException("ThornPenaltyFont lacks required characters.");
                return font;
            }
            if (File.Exists(FontPath)) throw new IOException("ThornPenaltyFont path is occupied.");
            font = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular", 48);
            if (font == null)
                throw new InvalidOperationException("Cannot load Malgun Gothic Regular for ThornPenaltyFont.");
            if (!font.TryAddCharacters(glyphs, out string missingCharacters))
                {
                UnityEngine.Object.DestroyImmediate(font);
                throw new InvalidOperationException("Cannot bake thorn penalty characters. Missing: " + missingCharacters);
            }
            font.name = "ThornPenaltyFont";
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            if (!AssetDatabase.IsValidFolder("Assets/Fonts")) AssetDatabase.CreateFolder("Assets", "Fonts");
            AssetDatabase.CreateAsset(font, FontPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (Texture2D texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }
    }
}
