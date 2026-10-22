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
    public static class CozyPresentationSetup
    {
        private const string ArtFolder = "Assets/UI/AppleRestCozy";
        private static readonly Color Cream = new Color(0.98f, 0.94f, 0.81f);
        private static readonly Color Ink = new Color(0.16f, 0.24f, 0.16f);
        private static readonly Color Gold = new Color(0.77f, 0.52f, 0.22f);

        [MenuItem("AppleRest/Add Cozy UI and E Apple Pickup")]
        public static void Build()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Assignment2_AppleRest/Assets"))
                throw new InvalidOperationException("Restricted to Assignment2_AppleRest.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GameStage.unity") throw new InvalidOperationException("Open GameStage first.");
            GameObject[] roots = scene.GetRootGameObjects();
            if (Find<CozyInteractionUI>(roots).Any())
            { Debug.LogWarning("Cozy UI already exists. Existing layout was preserved."); return; }
            AppleRestPlayer player = One<AppleRestPlayer>(roots);
            AppleCollection collection = One<AppleCollection>(roots);
            StumpInteraction stump = One<StumpInteraction>(roots);
            SitClearSequence sit = One<SitClearSequence>(roots);
            StageDeadline deadline = One<StageDeadline>(roots);
            AppleCounterUI counter = One<AppleCounterUI>(roots);
            Camera camera = Find<Camera>(roots).FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (camera == null) throw new InvalidOperationException("MainCamera is missing.");
            if (TMP_Settings.instance == null) throw new InvalidOperationException("Import TMP Essential Resources first.");
            EnsureFolder("Assets/UI");
            EnsureFolder(ArtFolder);
            Sprite card = Art("Card");
            Sprite apple = Art("Apple");
            Sprite clock = Art("Clock");
            Sprite stumpIcon = Art("Stump");
            Sprite arrow = Art("Arrow");
            Art("Cursor", true);
            Texture2D cursor = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/Cursor.png");
            TMP_FontAsset font = Font();

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add cozy UI and E pickup");
            try
            {
                TMP_Text counterLabel = Read<TMP_Text>(counter, "label");
                TMP_Text timerLabel = Read<TMP_Text>(deadline, "timerLabel");
                StyleHUD(counterLabel, card, apple);
                StyleHUD(timerLabel, card, clock);
                GameObject stumpPanel = Read<GameObject>(stump, "promptPanel");
                StyleCard(stumpPanel, card);
                foreach (TMP_Text label in stumpPanel.GetComponentsInChildren<TMP_Text>(true)) ColorText(label, Ink);
                StyleOverlay(Read<GameObject>(sit, "clearPanel"), card);
                StyleOverlay(Read<GameObject>(deadline, "failurePanel"), card);

                GameObject root = new GameObject("CozyPresentation");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Create cozy presentation");
                CozyInteractionUI controller = root.AddComponent<CozyInteractionUI>();
                GameObject canvasObj = UI("CozyCanvas", root.transform, typeof(Canvas), typeof(CanvasScaler));
                Canvas canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50; // Existing end screens remain above the hints.
                CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                GameObject hint = UI("ApplePickupHint", canvasObj.transform, typeof(Image));
                RectTransform hintRect = hint.GetComponent<RectTransform>();
                Floating(hintRect, new Vector2(190, 64));
                StyleCard(hint, card);
                TMP_Text hintLabel = Label("PickupText", hint.transform, font, "E  줍기", 30);
                Fill(hintLabel.rectTransform, 8);
                hint.SetActive(false);

                GameObject bubble = UI("GoalBubble", canvasObj.transform, typeof(Image));
                RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
                Floating(bubbleRect, new Vector2(310, 144));
                StyleCard(bubble, card);
                Icon("GoalApple", bubble.transform, apple, new Vector2(-105, 20), new Vector2(62, 62));
                TMP_Text count = Label("GoalCount", bubble.transform, font, "x5", 30);
                Place(count.rectTransform, new Vector2(-49, 20), new Vector2(52, 50));
                Icon("GoalArrow", bubble.transform, arrow, new Vector2(18, 20), new Vector2(42, 34));
                Icon("GoalStump", bubble.transform, stumpIcon, new Vector2(92, 20), new Vector2(76, 66));
                TMP_Text progress = Label("GoalProgress", bubble.transform, font, "사과 0 / 5", 23);
                Place(progress.rectTransform, new Vector2(0, -43), new Vector2(270, 34));
                progress.color = new Color(0.42f, 0.36f, 0.25f);
                GameObject tail = UI("Tail", bubble.transform, typeof(Image));
                tail.GetComponent<Image>().color = Cream;
                tail.GetComponent<Image>().raycastTarget = false;
                Place(tail.GetComponent<RectTransform>(), new Vector2(0, -72), new Vector2(16, 16));
                tail.transform.localRotation = Quaternion.Euler(0, 0, 45);
                bubble.SetActive(false);

                Assign(controller, "player", player);
                Assign(controller, "collection", collection);
                Assign(controller, "stump", stump);
                Assign(controller, "viewCamera", camera);
                Assign(controller, "canvasRect", canvasObj.GetComponent<RectTransform>());
                Assign(controller, "pickupHint", hintRect);
                Assign(controller, "goalBubble", bubbleRect);
                Assign(controller, "goalProgress", progress);
                Assign(controller, "cursorTexture", cursor);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("[AppleRest] Cozy UI ready. Save GameStage. Apples now require E near the nearest apple. Hover the player for the goal bubble. SitAnchor, Forest and game rules were not changed.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        private static void StyleHUD(TMP_Text label, Sprite card, Sprite icon)
        {
            if (label == null) throw new InvalidOperationException("HUD label reference missing.");
            GameObject panel = label.transform.parent.gameObject;
            StyleCard(panel, card);
            ColorText(label, Ink);
            Undo.RecordObject(label.rectTransform, "Inset HUD text");
            label.rectTransform.offsetMin = new Vector2(76, 8);
            label.rectTransform.offsetMax = new Vector2(-14, -8);
            Undo.RecordObject(label, "Style HUD text");
            label.fontSize = 30;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            GameObject iconObj = UI("CozyIcon", panel.transform, typeof(Image));
            Undo.RegisterCreatedObjectUndo(iconObj, "Add HUD icon");
            Image image = iconObj.GetComponent<Image>();
            image.sprite = icon; image.preserveAspect = true; image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
            rect.sizeDelta = new Vector2(52, 52); rect.anchoredPosition = new Vector2(40, 0);
        }

        private static void StyleCard(GameObject obj, Sprite card)
        {
            if (obj == null) throw new InvalidOperationException("UI panel reference missing.");
            Image image = obj.GetComponent<Image>();
            if (image == null) return;
            Undo.RecordObject(image, "Style cream card");
            image.sprite = card; image.type = Image.Type.Sliced; image.color = Cream;
            Outline outline = obj.GetComponent<Outline>();
            if (outline == null) outline = Undo.AddComponent<Outline>(obj);
            Undo.RecordObject(outline, "Style card border");
            outline.effectColor = Ink; outline.effectDistance = new Vector2(2, -2);
        }

        private static void StyleOverlay(GameObject panel, Sprite card)
        {
            if (panel == null) return;
            Image backdrop = panel.GetComponent<Image>();
            if (backdrop != null)
            { Undo.RecordObject(backdrop, "Forest end screen tint"); backdrop.color = new Color(0.055f, 0.12f, 0.09f, 0.92f); }
            foreach (TMP_Text label in panel.GetComponentsInChildren<TMP_Text>(true)) ColorText(label, Cream);
            foreach (Button button in panel.GetComponentsInChildren<Button>(true))
            {
                StyleCard(button.gameObject, card);
                foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true)) ColorText(label, Ink);
            }
        }

        private static void ColorText(TMP_Text label, Color color)
        { Undo.RecordObject(label, "Style text"); label.color = color; }
        private static T Read<T>(UnityEngine.Object owner, string field) where T : UnityEngine.Object =>
            new SerializedObject(owner).FindProperty(field).objectReferenceValue as T;
        private static void Assign(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            SerializedObject data = new SerializedObject(owner);
            data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static T[] Find<T>(GameObject[] roots) where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static T One<T>(GameObject[] roots) where T : Component
        {
            T[] found = Find<T>(roots);
            if (found.Length != 1) throw new InvalidOperationException("Expected one " + typeof(T).Name);
            return found[0];
        }
        private static GameObject UI(string name, Transform parent, params Type[] types)
        {
            GameObject obj = new GameObject(name, new[] { typeof(RectTransform) }.Concat(types).ToArray());
            obj.transform.SetParent(parent, false); return obj;
        }
        private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text, float size)
        {
            TMP_Text label = UI(name, parent, typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            label.font = font; label.text = text; label.fontSize = size; label.color = Ink;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; return label;
        }
        private static void Icon(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            Image image = UI(name, parent, typeof(Image)).GetComponent<Image>();
            image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            Place(image.rectTransform, position, size);
        }
        private static void Floating(RectTransform rect, Vector2 size)
        { rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.pivot = new Vector2(0.5f, 0); rect.sizeDelta = size; }
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size; }
        private static void Fill(RectTransform rect, float padding)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * padding; rect.offsetMax = -Vector2.one * padding; }
        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }

        private static TMP_FontAsset Font()
        {
            string path = ArtFolder + "/CozyFont.asset";
            const string glyphs = "E 줍기사과 0123456789/x";
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null)
            {
                if (!font.HasCharacters(glyphs)) throw new InvalidOperationException("CozyFont lacks required glyphs.");
                return font;
            }
            if (File.Exists(path)) throw new IOException("CozyFont path occupied.");
            font = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular", 48);
            if (font == null) throw new InvalidOperationException("Cannot load Malgun Gothic.");
            if (!font.TryAddCharacters(glyphs, out string missing))
            { UnityEngine.Object.DestroyImmediate(font); throw new InvalidOperationException("Missing CozyFont glyphs: " + missing); }
            font.name = "CozyFont"; font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, path);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (Texture2D texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font); return font;
        }

        // Small code-drawn icons, with no external package or original texture changes.
        private static Sprite Art(string name, bool cursor = false)
        {
            string path = ArtFolder + "/" + name + ".png";
            if (!File.Exists(path))
            {
                int size = cursor ? 32 : 128;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    Color sum = Color.clear;
                    for (int sy = 0; sy < 2; sy++) for (int sx = 0; sx < 2; sx++)
                    {
                        Color sample = Pixel(name, (x + (sx + 0.5f) / 2f) / size, (y + (sy + 0.5f) / 2f) / size);
                        sum += new Color(sample.r * sample.a, sample.g * sample.a, sample.b * sample.a, sample.a);
                    }
                    sum *= 0.25f;
                    if (sum.a > 0) { sum.r /= sum.a; sum.g /= sum.a; sum.b /= sum.a; }
                    texture.SetPixel(x, y, sum);
                }
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = cursor ? TextureImporterType.Cursor : TextureImporterType.Sprite;
                importer.isReadable = cursor; importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                if (!cursor)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 100;
                    if (name == "Card") importer.spriteBorder = new Vector4(24, 24, 24, 24);
                }
                importer.SaveAndReimport();
            }
            return cursor ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static bool Ellipse(float x, float y, float cx, float cy, float rx, float ry) =>
            Mathf.Pow((x - cx) / rx, 2) + Mathf.Pow((y - cy) / ry, 2) <= 1;
        private static Color Pixel(string name, float x, float y)
        {
            Color result = Color.clear;
            Color leaf = new Color(0.35f, 0.52f, 0.23f);
            if (name == "Card")
            {
                float dx = Mathf.Max(0.18f - x, x - 0.82f, 0);
                float dy = Mathf.Max(0.18f - y, y - 0.82f, 0);
                return dx * dx + dy * dy <= 0.18f * 0.18f ? Color.white : Color.clear;
            }
            if (name == "Apple")
            {
                if (Ellipse(x, y, 0.5f, 0.39f, 0.31f, 0.29f) || Ellipse(x, y, 0.34f, 0.56f, 0.23f, 0.24f) || Ellipse(x, y, 0.66f, 0.56f, 0.23f, 0.24f)) result = new Color(0.74f, 0.20f, 0.17f);
                if (x > 0.47f && x < 0.54f && y > 0.69f && y < 0.91f) result = Ink;
                if (Ellipse(x, y, 0.65f, 0.83f, 0.15f, 0.07f)) result = leaf;
                if (Ellipse(x, y, 0.29f, 0.57f, 0.055f, 0.11f)) result = new Color(1f, 0.7f, 0.5f);
            }
            if (name == "Clock")
            {
                if (Ellipse(x, y, 0.5f, 0.49f, 0.41f, 0.41f)) result = Gold;
                if (Ellipse(x, y, 0.5f, 0.49f, 0.32f, 0.32f)) result = Cream;
                if ((Mathf.Abs(x - 0.5f) < 0.035f && y > 0.46f && y < 0.73f) || (Mathf.Abs(y - 0.49f) < 0.035f && x > 0.47f && x < 0.72f)) result = Ink;
                if (x > 0.4f && x < 0.6f && y > 0.9f && y < 0.96f) result = Ink;
            }
            if (name == "Stump")
            {
                if (y > 0.14f && y < 0.65f && Mathf.Abs(x - 0.5f) < (0.29f + (0.65f - y) * 0.17f)) result = new Color(0.40f, 0.25f, 0.15f);
                if (result.a > 0 && y < 0.56f && (Mathf.Abs(x - 0.31f) < 0.02f || Mathf.Abs(x - 0.6f) < 0.025f)) result = Gold;
                if (Ellipse(x, y, 0.5f, 0.66f, 0.36f, 0.19f)) result = new Color(0.82f, 0.61f, 0.35f);
                float ring = Mathf.Pow((x - 0.5f) / 0.25f, 2) + Mathf.Pow((y - 0.66f) / 0.12f, 2);
                if (ring > 0.75f && ring < 1.1f) result = Gold;
                if (Ellipse(x, y, 0.25f, 0.26f, 0.14f, 0.065f)) result = leaf;
            }
            if (name == "Arrow")
            {
                if ((x > 0.12f && x < 0.65f && Mathf.Abs(y - 0.5f) < 0.075f) ||
                    (x > 0.53f && x < 0.9f && Mathf.Abs(y - 0.5f) < (0.9f - x) * 0.8f)) result = Gold;
            }
            if (name == "Cursor")
            {
                // Upper-left pointing leaf-shaped pointer; hotspot is (3,3) from the top-left.
                Vector2[] polygon = { new Vector2(0.08f, 0.92f), new Vector2(0.18f, 0.20f), new Vector2(0.34f, 0.39f), new Vector2(0.57f, 0.12f), new Vector2(0.70f, 0.23f), new Vector2(0.47f, 0.49f), new Vector2(0.80f, 0.53f) };
                bool inside = false;
                for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                    if ((polygon[i].y > y) != (polygon[j].y > y) && x < (polygon[j].x - polygon[i].x) * (y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x) inside = !inside;
                if (inside) result = Cream;
                if (Ellipse(x, y, 0.39f, 0.57f, 0.15f, 0.12f)) result = leaf;
            }
            return result;
        }
    }
}
