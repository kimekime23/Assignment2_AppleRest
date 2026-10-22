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
    public static class StorybookUISetup
    {
        private const string Folder = "Assets/Art/UI";
        private static readonly Color Pencil = new Color32(0x42, 0x30, 0x24, 255);
        private static readonly Color Green = new Color32(0x40, 0x6E, 0x3E, 255);
        private static readonly Color Red = new Color32(0x96, 0x46, 0x3C, 255);

        [MenuItem("AppleRest/Apply Storybook UI Kit")]
        public static void Build()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Assignment2_AppleRest/Assets"))
                throw new InvalidOperationException("Restricted to Assignment2_AppleRest.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GameStage.unity") throw new InvalidOperationException("Open GameStage first.");
            GameObject[] roots = scene.GetRootGameObjects();
            if (Find<StorybookUIEffects>(roots).Any())
            { Debug.LogWarning("Storybook UI already applied; existing layout was preserved."); return; }
            AppleCollection collection = One<AppleCollection>(roots);
            StageDeadline deadline = One<StageDeadline>(roots);
            SitClearSequence sit = One<SitClearSequence>(roots);
            StumpInteraction stump = One<StumpInteraction>(roots);
            AppleCounterUI counter = One<AppleCounterUI>(roots);
            TMP_Text counterLabel = Read<TMP_Text>(counter, "label");
            TMP_Text timerLabel = Read<TMP_Text>(deadline, "timerLabel");
            TMP_Text penaltyLabel = Read<TMP_Text>(deadline, "penaltyLabel");
            TMP_Text promptLabel = Read<TMP_Text>(stump, "promptLabel");
            GameObject clearPanel = Read<GameObject>(sit, "clearPanel");
            GameObject failurePanel = Read<GameObject>(deadline, "failurePanel");
            Button failureRetry = Read<Button>(deadline, "retryButton");
            string[] missingReferences = new[] {
                counterLabel == null ? "AppleCounterUI.label" : null,
                timerLabel == null ? "StageDeadline.timerLabel" : null,
                promptLabel == null ? "StumpInteraction.promptLabel" : null,
                Read<GameObject>(stump, "promptPanel") == null ? "StumpInteraction.promptPanel" : null,
                clearPanel == null ? "SitClearSequence.clearPanel" : null,
                failurePanel == null ? "StageDeadline.failurePanel" : null,
                failureRetry == null ? "StageDeadline.retryButton" : null
            }.Where(field => field != null).ToArray();
            if (missingReferences.Length > 0)
                throw new InvalidOperationException("Missing UI references: " + string.Join(", ", missingReferences));
            Canvas timerCanvas = timerLabel.GetComponentInParent<Canvas>();
            if (timerCanvas == null) throw new InvalidOperationException("StageDeadline.timerLabel must be inside a Canvas.");
            ImportImages();
            TMP_FontAsset font = GetFont(roots);
            Sprite counterSprite = Sprite("UI_Paper_Counter"), timerSprite = Sprite("UI_Paper_Timer"),
                promptSprite = Sprite("UI_Paper_Prompt"), resultSprite = Sprite("UI_Paper_Result"),
                buttonSprite = Sprite("UI_Paper_Button"), pressedSprite = Sprite("UI_Paper_Button_Pressed");
            Sprite apple = Sprite("UI_Icon_Apple"), sun = Sprite("UI_Icon_Sun"), stumpIcon = Sprite("UI_Icon_Stump");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply storybook UI appearance");
            try
            {
                // Penalty UI is optional in older scenes. Repair only this presentation reference.
                if (penaltyLabel == null)
                {
                    TMP_Text[] candidates = timerCanvas.GetComponentsInChildren<TMP_Text>(true)
                        .Where(t => t.name == "ThornPenaltyText").ToArray();
                    if (candidates.Length > 1)
                        throw new InvalidOperationException("Multiple ThornPenaltyText objects found. Assign StageDeadline.penaltyLabel explicitly.");
                    if (candidates.Length == 1) penaltyLabel = candidates[0];
                    else
                    {
                        GameObject obj = NewUI("ThornPenaltyText", timerCanvas.transform, typeof(TextMeshProUGUI));
                        Undo.RegisterCreatedObjectUndo(obj, "Create missing time penalty UI");
                        penaltyLabel = obj.GetComponent<TextMeshProUGUI>();
                    }
                    Undo.RecordObject(penaltyLabel, "Initialize penalty label");
                    penaltyLabel.raycastTarget = false;
                    Undo.RecordObject(penaltyLabel.gameObject, "Hide penalty label until contact");
                    penaltyLabel.gameObject.SetActive(false);
                    Undo.RecordObject(deadline, "Connect missing penalty UI");
                    Assign(deadline, "penaltyLabel", penaltyLabel);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(deadline);
                }
                RectTransform counterPaper = counterLabel.transform.parent as RectTransform;
                RectTransform timerPaper = timerLabel.transform.parent as RectTransform;
                HUD(counterPaper, counterLabel, counterSprite, apple, false);
                HUD(timerPaper, timerLabel, timerSprite, sun, true);
                counterLabel.text = "0 / " + collection.TotalCount;
                float limit = new SerializedObject(deadline).FindProperty("timeLimit").floatValue;
                timerLabel.text = Mathf.CeilToInt(limit) + "초";
                RectTransform promptPaper = Read<GameObject>(stump, "promptPanel").GetComponent<RectTransform>();
                Anchor(promptPaper, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24));
                Paper(promptPaper.GetComponent<Image>(), promptSprite);
                SetText(promptLabel, new Vector2(44, 0), new Vector2(610, 82), 42);
                Icon(promptPaper, "StorybookStump", stumpIcon, new Vector2(-305, 0), 76);
                Undo.RecordObject(penaltyLabel.rectTransform, "Move time penalty below timer");
                Anchor(penaltyLabel.rectTransform, Vector2.one, Vector2.one, new Vector2(-36, -152));
                penaltyLabel.rectTransform.sizeDelta = new Vector2(190, 64);
                Undo.RecordObject(penaltyLabel, "Style time penalty");
                penaltyLabel.fontSize = 48; penaltyLabel.alignment = TextAlignmentOptions.MidlineRight;
                penaltyLabel.text = "−3초!";

                Button clearRetry = Result(clearPanel, true, null, resultSprite, buttonSprite, pressedSprite);
                Result(failurePanel, false, failureRetry, resultSprite, buttonSprite, pressedSprite);

                // Apply the same font to every UI text in this scene, including inactive screens.
                foreach (TMP_Text label in Find<TMP_Text>(scene.GetRootGameObjects()))
                {
                    Undo.RecordObject(label, "Apply Nanum Pen UI font");
                    label.font = font; label.fontStyle = FontStyles.Normal; label.color = Pencil;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                }
                clearPanel.transform.Find("StorybookResult/ClearTitle").GetComponent<TMP_Text>().color = Green;
                failurePanel.transform.Find("StorybookResult/FailureTitle").GetComponent<TMP_Text>().color = Red;
                penaltyLabel.color = new Color32(0xBA, 0x3A, 0x30, 255);
                foreach (Button button in Find<Button>(scene.GetRootGameObjects()))
                    StyleButton(button, buttonSprite, pressedSprite);
                // Reuse the supplied icons for the goal bubble and start-screen objective pictures.
                foreach (Image image in Find<Image>(scene.GetRootGameObjects()))
                {
                    if (image.sprite == null) continue;
                    Sprite replacement = image.sprite.name == "Apple" ? apple : image.sprite.name == "Stump" ? stumpIcon : null;
                    if (replacement != null)
                    { Undo.RecordObject(image, "Replace objective icon"); image.sprite = replacement; image.preserveAspect = true; image.color = Color.white; }
                }

                GameObject effectsObject = new GameObject("StorybookUIEffects");
                SceneManager.MoveGameObjectToScene(effectsObject, scene);
                Undo.RegisterCreatedObjectUndo(effectsObject, "Add UI-only effects");
                StorybookUIEffects effects = effectsObject.AddComponent<StorybookUIEffects>();
                Assign(effects, "collection", collection); Assign(effects, "deadline", deadline); Assign(effects, "sitSequence", sit);
                Assign(effects, "counterPaper", counterPaper); Assign(effects, "timerPaper", timerPaper);
                Assign(effects, "counterLabel", counterLabel); Assign(effects, "timerLabel", timerLabel);
                Assign(effects, "penaltyLabel", penaltyLabel); Assign(effects, "clearRetry", clearRetry);
                EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(group);
                Debug.Log("[AppleRest] Storybook UI applied. Save GameStage. Paper uses native dimensions, Simple images and Preserve Aspect. Game rules and Time Limit were not changed.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        private static void ImportImages()
        {
            if (!Directory.Exists(Folder)) throw new DirectoryNotFoundException("Extract the UI kit into " + Folder);
            foreach (string file in Directory.GetFiles(Folder, "*.png"))
            {
                string path = file.Replace('\\', '/');
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Wait for image import: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = Vector4.zero;
                importer.spritePixelsPerUnit = 100;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 4096; importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }
        private static TMP_FontAsset GetFont(GameObject[] roots)
        {
            string path = Folder + "/NanumPenScript_Dynamic.asset";
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
            {
                if (File.Exists(path)) throw new IOException("Font asset path occupied.");
                Font source = AssetDatabase.LoadAssetAtPath<Font>(Folder + "/NanumPenScript-Regular.ttf");
                if (source == null) throw new InvalidOperationException("NanumPenScript-Regular.ttf is missing or still importing.");
                font = TMP_FontAsset.CreateFontAsset(source);
                if (font == null) throw new InvalidOperationException("Could not create Nanum Pen TMP font.");
                font.name = "NanumPenScript_Dynamic";
                font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                font.isMultiAtlasTexturesEnabled = true;
                string glyphs = string.Join(" ", Find<TMP_Text>(roots).Select(t => t.text)) +
                    " -0123456789/!초다 모았다해가 졌어요사과를 모두 모으고 휴식을 취했습니다.내일 다시 쉬러 와요.다시 하기";
                glyphs = glyphs.Replace('\u2212', '-').Replace('\u00B7', '.').Replace('\n', ' ').Replace('\r', ' ');
                if (!font.TryAddCharacters(glyphs, out string missing))
                { UnityEngine.Object.DestroyImmediate(font); throw new InvalidOperationException("Missing Nanum Pen characters: " + missing); }
                // U+2212 is absent from this TTF: map it to this same font's hand-drawn hyphen glyph.
                if (!font.HasCharacter('\u2212') && font.characterLookupTable.TryGetValue(45, out TMP_Character hyphen))
                {
                    font.characterTable.Add(new TMP_Character(0x2212, font, hyphen.glyph));
                    font.ReadFontAssetDefinition();
                }
                // The existing start-screen instructions contain a middle dot (U+00B7),
                // also absent from Nanum Pen. Keep their text intact using its own period glyph.
                if (!font.HasCharacter('\u00B7') && font.characterLookupTable.TryGetValue(46, out TMP_Character period))
                {
                    font.characterTable.Add(new TMP_Character(0x00B7, font, period.glyph));
                    font.ReadFontAssetDefinition();
                }
                AssetDatabase.CreateAsset(font, path);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (Texture2D texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
                EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
            }
            if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic || !font.HasCharacter('\u2212') || !font.HasCharacter('\u00B7'))
                throw new InvalidOperationException("Existing NanumPenScript_Dynamic must be Dynamic and include minus/middle-dot aliases.");
            return font;
        }

        private static void HUD(RectTransform panel, TMP_Text label, Sprite paper, Sprite icon, bool right)
        {
            Anchor(panel, right ? Vector2.one : new Vector2(0, 1), right ? Vector2.one : new Vector2(0, 1), new Vector2(right ? -24 : 24, -24));
            Paper(panel.GetComponent<Image>(), paper);
            SetText(label, new Vector2(40, 0), new Vector2(right ? 175 : 210, 95), right ? 62 : 60);
            Transform oldIcon = panel.Find("CozyIcon");
            if (oldIcon != null) { Undo.RecordObject(oldIcon.gameObject, "Hide previous UI icon"); oldIcon.gameObject.SetActive(false); }
            Icon(panel, "StorybookIcon", icon, new Vector2(right ? -94 : -112, 0), 76);
        }
        private static Button Result(GameObject overlay, bool clear, Button existingRetry, Sprite paper, Sprite normal, Sprite pressed)
        {
            Canvas canvas = overlay.GetComponentInParent<Canvas>();
            if (canvas.GetComponent<GraphicRaycaster>() == null) Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
            Image background = overlay.GetComponent<Image>();
            if (background != null)
            {
                Undo.RecordObject(background, "Style result backdrop");
                background.color = clear ? new Color(0.82f, 0.81f, 0.72f, 0.4f) : new Color(0.12f, 0.08f, 0.09f, 0.6f);
            }
            TMP_Text[] oldText = overlay.GetComponentsInChildren<TMP_Text>(true)
                .Where(t => t.GetComponentInParent<Button>() == null).ToArray();
            foreach (TMP_Text text in oldText)
            { Undo.RecordObject(text.gameObject, "Hide previous result text"); text.gameObject.SetActive(false); }
            GameObject card = NewUI("StorybookResult", overlay.transform, typeof(Image), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(card, "Add result paper");
            Center(card.GetComponent<RectTransform>(), Vector2.zero);
            Paper(card.GetComponent<Image>(), paper);
            TMP_Text title = Text(clear ? "ClearTitle" : "FailureTitle", card.transform, clear ? "다 모았다!" : "해가 졌어요");
            SetText(title, new Vector2(0, 150), new Vector2(780, 130), 88);
            TMP_Text description = Text("ResultDescription", card.transform,
                clear ? "사과를 모두 모으고 휴식을 취했습니다." : "내일 다시 쉬러 와요.");
            SetText(description, new Vector2(0, -4), new Vector2(760, 120), 48);
            Button retry = existingRetry;
            if (retry == null)
            {
                GameObject obj = NewUI("ClearRetryButton", card.transform, typeof(Image), typeof(Button));
                retry = obj.GetComponent<Button>();
                TMP_Text label = Text("RetryText", obj.transform, "다시 하기");
                SetText(label, Vector2.zero, new Vector2(260, 75), 48);
            }
            else Undo.SetTransformParent(retry.transform, card.transform, "Move retry onto paper");
            Center(retry.GetComponent<RectTransform>(), new Vector2(0, -180));
            StyleButton(retry, normal, pressed);
            foreach (TMP_Text label in retry.GetComponentsInChildren<TMP_Text>(true))
            { SetText(label, Vector2.zero, new Vector2(260, 75), 48); label.text = "다시 하기"; }
            Undo.AddComponent<StorybookPaperEntrance>(card);
            return retry;
        }

        private static void StyleButton(Button button, Sprite normal, Sprite pressed)
        {
            Image image = button.GetComponent<Image>();
            if (image == null) return;
            Paper(image, normal);
            Undo.RecordObject(button, "Paper button pressed sprite");
            button.targetGraphic = image; button.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = button.spriteState;
            state.highlightedSprite = normal; state.selectedSprite = normal;
            state.pressedSprite = pressed; state.disabledSprite = normal;
            button.spriteState = state;
        }
        private static void Paper(Image image, Sprite sprite)
        {
            if (image == null) throw new InvalidOperationException("Paper Image is missing.");
            Undo.RecordObject(image, "Apply paper sprite without slicing");
            Undo.RecordObject(image.rectTransform, "Set native paper size");
            image.sprite = sprite; image.overrideSprite = null; image.type = Image.Type.Simple;
            image.color = Color.white; image.preserveAspect = true; image.SetNativeSize();
            foreach (BaseMeshEffect effect in image.GetComponents<BaseMeshEffect>())
            { Undo.RecordObject(effect, "Remove old UI border effect"); effect.enabled = false; }
        }
        private static void SetText(TMP_Text text, Vector2 position, Vector2 size, float fontSize)
        {
            Undo.RecordObject(text, "Layout hand-written label"); Undo.RecordObject(text.rectTransform, "Position hand-written label");
            Center(text.rectTransform, position); text.rectTransform.sizeDelta = size;
            text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false; text.enableAutoSizing = false;
        }
        private static void Icon(Transform parent, string name, Sprite sprite, Vector2 position, float size)
        {
            GameObject obj = NewUI(name, parent, typeof(Image)); Undo.RegisterCreatedObjectUndo(obj, "Add storybook icon");
            Image image = obj.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            Center(image.rectTransform, position); image.rectTransform.sizeDelta = Vector2.one * size;
        }
        private static TMP_Text Text(string name, Transform parent, string text)
        {
            TMP_Text label = NewUI(name, parent, typeof(TextMeshProUGUI)).GetComponent<TMP_Text>(); label.text = text; return label;
        }
        private static GameObject NewUI(string name, Transform parent, params Type[] types)
        {
            GameObject obj = new GameObject(name, new[] { typeof(RectTransform) }.Concat(types).ToArray()); obj.transform.SetParent(parent, false); return obj;
        }
        private static void Center(RectTransform rect, Vector2 position) => Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position);
        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            Undo.RecordObject(rect, "Layout paper UI"); rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot; rect.anchoredPosition = position;
        }
        private static Sprite Sprite(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + name + ".png");
            if (sprite == null) throw new InvalidOperationException("Missing UI kit sprite: " + name); return sprite;
        }
        private static T[] Find<T>(GameObject[] roots) where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static T One<T>(GameObject[] roots) where T : Component
        {
            T[] found = Find<T>(roots); if (found.Length != 1) throw new InvalidOperationException("Expected one " + typeof(T).Name); return found[0];
        }
        private static T Read<T>(UnityEngine.Object owner, string field) where T : UnityEngine.Object => new SerializedObject(owner).FindProperty(field).objectReferenceValue as T;
        private static void Assign(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            SerializedObject data = new SerializedObject(owner); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
