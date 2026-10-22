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
    public static class AppleRestStartScreenSetup
    {
        private const string Folder = "Assets/UI/AppleRestCozy";
        private const string Title = "사과를 모으고,\n잠깐의 휴식";
        private const string Description = "해가 지기 전에 사과 5개를 모으고 쉬어가세요.";
        private const string Instructions = "WASD 이동   /   E 줍기 · 앉기";
        private static readonly Color Cream = new Color(0.98f, 0.94f, 0.81f);
        private static readonly Color Green = new Color(0.16f, 0.24f, 0.16f);

        [MenuItem("AppleRest/Add Start Screen")]
        public static void Build()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Assignment2_AppleRest/Assets"))
                throw new InvalidOperationException("Restricted to Assignment2_AppleRest.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GameStage.unity") throw new InvalidOperationException("Open GameStage first.");
            GameObject[] roots = scene.GetRootGameObjects();
            if (Find<AppleRestStartScreen>(roots).Any())
            { Debug.LogWarning("Start screen already exists; no duplicate was created."); return; }
            AppleRestPlayer player = One<AppleRestPlayer>(roots);
            StageDeadline timer = One<StageDeadline>(roots);
            AppleCounterUI counter = One<AppleCounterUI>(roots);
            EventSystem events = One<EventSystem>(roots);
            if (!events.isActiveAndEnabled || events.GetComponent<InputSystemUIInputModule>() == null ||
                !events.GetComponent<InputSystemUIInputModule>().isActiveAndEnabled)
                throw new InvalidOperationException("An active InputSystemUIInputModule is required for the start button.");
            Sprite card = Sprite("Card");
            Sprite apple = Sprite("Apple");
            Sprite arrow = Sprite("Arrow");
            Sprite stump = Sprite("Stump");
            TMP_Text timerLabel = new SerializedObject(timer).FindProperty("timerLabel").objectReferenceValue as TMP_Text;
            Canvas timerCanvas = timerLabel != null ? timerLabel.GetComponentInParent<Canvas>() : null;
            Canvas counterCanvas = counter.GetComponentInParent<Canvas>();
            if (timerCanvas == null || counterCanvas == null) throw new InvalidOperationException("Timer/counter canvas missing.");
            TMP_FontAsset font = GetFont();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Apple Rest start screen");
            try
            {
                GameObject root = new GameObject("AppleRestStartScreen");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Add start screen");
                AppleRestStartScreen controller = root.AddComponent<AppleRestStartScreen>();
                GameObject canvasObject = Child("StartCanvas", root.transform, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 500;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                GameObject screen = Child("StartOverlay", canvasObject.transform, typeof(Image));
                Fill(screen.GetComponent<RectTransform>());
                screen.GetComponent<Image>().color = new Color(0.035f, 0.09f, 0.055f, 0.55f);

                GameObject panel = Child("TitleCard", screen.transform, typeof(Image), typeof(Outline));
                Place(panel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1040, 700));
                panel.GetComponent<Image>().sprite = card;
                panel.GetComponent<Image>().type = Image.Type.Sliced;
                panel.GetComponent<Image>().color = Cream;
                panel.GetComponent<Outline>().effectColor = Green;
                panel.GetComponent<Outline>().effectDistance = new Vector2(3, -3);
                Label(panel.transform, font, "APPLE REST", 22, new Vector2(0, 290), new Vector2(800, 42));
                Label(panel.transform, font, Title, 62, new Vector2(0, 160), new Vector2(960, 180));
                Label(panel.transform, font, Description, 29, new Vector2(0, 36), new Vector2(960, 60));
                Icon(panel.transform, apple, new Vector2(-110, -57), new Vector2(70, 70));
                Label(panel.transform, font, "x5", 30, new Vector2(-48, -57), new Vector2(54, 60));
                Icon(panel.transform, arrow, new Vector2(22, -57), new Vector2(46, 35));
                Icon(panel.transform, stump, new Vector2(105, -57), new Vector2(82, 72));

                GameObject buttonObject = Child("StartButton", panel.transform, typeof(Image), typeof(Button));
                Place(buttonObject.GetComponent<RectTransform>(), new Vector2(0, -180), new Vector2(400, 90));
                Image buttonImage = buttonObject.GetComponent<Image>();
                buttonImage.sprite = card; buttonImage.type = Image.Type.Sliced; buttonImage.color = Color.white;
                Button button = buttonObject.GetComponent<Button>();
                button.targetGraphic = buttonImage;
                ColorBlock colors = button.colors;
                colors.normalColor = Green;
                colors.highlightedColor = colors.selectedColor = new Color(0.27f, 0.40f, 0.23f);
                colors.pressedColor = new Color(0.10f, 0.18f, 0.10f);
                button.colors = colors;
                TMP_Text startLabel = Label(button.transform, font, "시작하기", 36, Vector2.zero, new Vector2(380, 80));
                startLabel.color = Cream;
                Label(panel.transform, font, Instructions, 25, new Vector2(0, -285), new Vector2(960, 50));

                SerializedObject data = new SerializedObject(controller);
                data.FindProperty("player").objectReferenceValue = player;
                data.FindProperty("screen").objectReferenceValue = screen;
                data.FindProperty("startButton").objectReferenceValue = button;
                SerializedProperty hud = data.FindProperty("gameplayHUD");
                hud.arraySize = 2;
                hud.GetArrayElementAtIndex(0).objectReferenceValue = counterCanvas;
                hud.GetArrayElementAtIndex(1).objectReferenceValue = timerCanvas;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("[AppleRest] Start screen added. Save GameStage. Time and movement pause until Start. The existing Time Limit value is preserved.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        private static Sprite Sprite(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + name + ".png");
            if (sprite == null) throw new InvalidOperationException("Run Add Cozy UI and E Apple Pickup first; missing icon: " + name);
            return sprite;
        }
        private static TMP_FontAsset GetFont()
        {
            string path = Folder + "/StartScreenFont.asset";
            string glyphs = (Title + Description + Instructions + "APPLE REST x5 시작하기").Replace("\n", " ");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null)
            {
                if (!font.HasCharacters(glyphs)) throw new InvalidOperationException("StartScreenFont lacks required glyphs.");
                return font;
            }
            if (File.Exists(path)) throw new IOException("StartScreenFont path occupied.");
            font = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular", 64);
            if (font == null) throw new InvalidOperationException("Cannot load Malgun Gothic.");
            if (!font.TryAddCharacters(glyphs, out string missing))
            { UnityEngine.Object.DestroyImmediate(font); throw new InvalidOperationException("Missing start screen characters: " + missing); }
            font.name = "StartScreenFont"; font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, path);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (Texture2D texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font); return font;
        }
        private static T[] Find<T>(GameObject[] roots) where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static T One<T>(GameObject[] roots) where T : Component
        {
            T[] found = Find<T>(roots);
            if (found.Length != 1) throw new InvalidOperationException("Expected one " + typeof(T).Name);
            return found[0];
        }
        private static GameObject Child(string name, Transform parent, params Type[] types)
        {
            GameObject obj = new GameObject(name, new[] { typeof(RectTransform) }.Concat(types).ToArray());
            obj.transform.SetParent(parent, false); return obj;
        }
        private static TMP_Text Label(Transform parent, TMP_FontAsset font, string text, float size, Vector2 position, Vector2 dimensions)
        {
            TMP_Text label = Child("Label", parent, typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            label.font = font; label.text = text; label.fontSize = size; label.color = Green;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            Place(label.rectTransform, position, dimensions); return label;
        }
        private static void Icon(Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            Image icon = Child("Icon", parent, typeof(Image)).GetComponent<Image>();
            icon.sprite = sprite; icon.preserveAspect = true; icon.raycastTarget = false;
            Place(icon.rectTransform, position, size);
        }
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size; }
        private static void Fill(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
