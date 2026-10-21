using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppleRest.Editor
{
    public static class SitClearSetup
    {
        private const string ClipPath = "Assets/Animations/Sit.anim";
        private const string FontPath = "Assets/Fonts/ClearFont.asset";
        private const string ClearMessage = "사과를 모두 모으고 휴식을 취했습니다.";

        [MenuItem("AppleRest/Add Sit Animation and Clear")]
        public static void Build()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Assignment2_AppleRest/Assets"))
                throw new InvalidOperationException("Restricted to Assignment2_AppleRest.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GameStage.unity")
            { Debug.LogError("Open GameStage and make it active first."); return; }
            GameObject[] roots = scene.GetRootGameObjects();
            if (roots.SelectMany(r => r.GetComponentsInChildren<SitClearSequence>(true)).Any())
            { Debug.LogWarning("Sit/Clear already configured; existing setup was not overwritten."); return; }
            AppleRestPlayer player = Single<AppleRestPlayer>(roots);
            AppleCollection collection = Single<AppleCollection>(roots);
            StumpInteraction interaction = Single<StumpInteraction>(roots);
            Animator animator = player.GetComponentInChildren<Animator>();
            CharacterController capsule = player.GetComponent<CharacterController>();
            AnimatorController controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
            Collider ground = roots.FirstOrDefault(r => r.name == "Ground")?.GetComponent<Collider>();
            BoxCollider stumpBody = interaction.GetComponent<BoxCollider>();
            if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid ||
                controller == null || capsule == null || ground == null || stumpBody == null || collection.TotalCount != 5)
                throw new InvalidOperationException("Valid Humanoid, controller, ground, stump body and five-apple collection are required.");
            if (AssetDatabase.GetAssetPath(controller) != "Assets/Animations/PlayerLocomotion.controller")
                throw new InvalidOperationException("Only the assignment PlayerLocomotion controller can be modified.");
            SerializedObject interactionData = new SerializedObject(interaction);
            if (interactionData.FindProperty("player").objectReferenceValue != player ||
                interactionData.FindProperty("collection").objectReferenceValue != collection)
                throw new InvalidOperationException("Stump player/collection references do not match.");
            if (TMP_Settings.instance == null || Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
            { Debug.LogError("Import TMP Essential Resources first."); return; }
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idle = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle");
            if (!(idle?.motion is AnimationClip idleClip)) throw new InvalidOperationException("Idle clip is missing.");

            AnimationClip clip = GetSitClip(animator, idleClip);
            TMP_FontAsset font = GetClearFont();
            if (font == null) return;
            AnimatorState sit = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Sit");
            if (sit != null && sit.motion != clip)
                throw new InvalidOperationException("Existing Sit state uses another motion; it was not replaced.");
            if (sit != null && sit.transitions.Length != 0)
                throw new InvalidOperationException("Existing Sit has outgoing transitions; remove them manually to hold the pose.");

            // Only add the assignment Sit state; manual anchoring needs no IK callback.
            if (sit == null)
            {
                sit = machine.AddState("Sit", new Vector3(500, 210));
                sit.motion = clip;
                sit.writeDefaultValues = false;
            }
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(sit);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure Sit and Clear");
            try
            {
                Transform sitAnchor = GetOrCreateSitAnchor(interaction, ground);
                GameObject canvasObject = new GameObject("StageClearCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                Undo.RegisterCreatedObjectUndo(canvasObject, "Create Clear UI");
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.GetComponent<Canvas>().sortingOrder = 100;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                GameObject panel = new GameObject("ClearPanel", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(canvasObject.transform, false);
                RectTransform rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                panel.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.025f, 0.78f);
                panel.GetComponent<Image>().raycastTarget = false;
                Label(panel.transform, font, "CLEAR", 76f, 55f, 120f);
                Label(panel.transform, font, ClearMessage, 34f, -55f, 90f);
                panel.SetActive(false);

                SitClearSequence sequence = Undo.AddComponent<SitClearSequence>(animator.gameObject);
                Assign(sequence, "interaction", interaction);
                Assign(sequence, "collection", collection);
                Assign(sequence, "player", player);
                Assign(sequence, "characterController", capsule);
                Assign(sequence, "sitAnchor", sitAnchor);
                Assign(sequence, "clearPanel", panel);
                AppleCounterUI counter = roots.SelectMany(r => r.GetComponentsInChildren<AppleCounterUI>(true)).FirstOrDefault();
                Assign(sequence, "counterCanvas", counter != null ? counter.GetComponentInParent<Canvas>().gameObject : null);
                GameObject prompt = interactionData.FindProperty("promptPanel").objectReferenceValue as GameObject;
                Assign(sequence, "promptCanvas", prompt != null ? prompt.GetComponentInParent<Canvas>().gameObject : null);
                Undo.RecordObject(animator, "Disable root motion");
                animator.applyRootMotion = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("[AppleRest] Sit/Clear configured. Save GameStage. Adjust only RestStump/SitAnchor position and rotation in Edit Mode. Source FBX unchanged.");
            }
            catch (Exception error)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(error);
            }
        }

        [MenuItem("AppleRest/Use Manual SitAnchor")]
        public static void UseManualSitAnchor()
        {
            if (!Application.dataPath.Replace((char)92, '/').EndsWith("/Assignment2_AppleRest/Assets"))
                throw new InvalidOperationException("Restricted to Assignment2_AppleRest.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GameStage.unity")
                throw new InvalidOperationException("Open GameStage and make it active first.");
            GameObject[] roots = scene.GetRootGameObjects();
            StumpInteraction interaction = Single<StumpInteraction>(roots);
            SitClearSequence sequence = Single<SitClearSequence>(roots);
            Collider ground = roots.FirstOrDefault(r => r.name == "Ground")?.GetComponent<Collider>();
            if (ground == null) throw new InvalidOperationException("Ground collider is missing.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Use manual SitAnchor");
            try
            {
                Transform anchor = GetOrCreateSitAnchor(interaction, ground);
                Undo.RecordObject(sequence, "Connect SitAnchor");
                Assign(sequence, "sitAnchor", anchor);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sequence);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("[AppleRest] SitAnchor connected. Save GameStage. Adjust only its Position/Rotation in Edit Mode. Existing SitAnchor pose is preserved. Old SitPoint is unused.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private static Transform GetOrCreateSitAnchor(StumpInteraction interaction, Collider ground)
        {
            Transform existing = interaction.transform.Find("SitAnchor");
            if (existing != null) return existing; // Never overwrite a manually tuned pose.
            Transform visual = interaction.transform.Find("StumpVisual");
            Renderer[] renderers = visual != null ? visual.GetComponentsInChildren<Renderer>() : new Renderer[0];
            if (renderers.Length == 0) throw new InvalidOperationException("StumpVisual renderers are missing.");
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Vector3 forward = Vector3.ProjectOnPlane(interaction.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            // Coarse initial ROOT marker near the front of the stump, at standing ground height.
            // Its height is intentionally NOT the seat surface or a calculated Humanoid pelvis target.
            Vector3 position = bounds.center + forward * Mathf.Min(0.2f, bounds.extents.magnitude * 0.2f);
            position.y = ground.bounds.max.y + 0.03f;
            GameObject marker = new GameObject("SitAnchor");
            SceneManager.MoveGameObjectToScene(marker, interaction.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(marker, "Create SitAnchor");
            marker.transform.SetParent(interaction.transform, false);
            marker.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
            return marker.transform;
        }
        private static AnimationClip GetSitClip(Animator animator, AnimationClip idle)
        {
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (existing != null)
            {
                if (AnimationUtility.GetAnimationClipSettings(existing).loopTime || existing.length <= 0f)
                    throw new InvalidOperationException("Existing Sit.anim must be non-looping and have positive duration.");
                return existing;
            }
            if (File.Exists(ClipPath)) throw new IOException("Sit.anim exists but cannot be loaded.");
            GameObject preview = UnityEngine.Object.Instantiate(animator.gameObject);
            preview.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                preview.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Animator previewAnimator = preview.GetComponent<Animator>();
                previewAnimator.Rebind();
                idle.SampleAnimation(preview, 0f);
                HumanPose pose = new HumanPose();
                using (HumanPoseHandler handler = new HumanPoseHandler(previewAnimator.avatar, previewAnimator.transform))
                    handler.GetHumanPose(ref pose);
                float[] target = (float[])pose.muscles.Clone();
                SetMuscle(target, "Left Upper Leg Front-Back", 0.65f);
                SetMuscle(target, "Right Upper Leg Front-Back", 0.65f);
                SetMuscle(target, "Left Lower Leg Stretch", -0.8f);
                SetMuscle(target, "Right Lower Leg Stretch", -0.8f);
                SetMuscle(target, "Left Upper Leg In-Out", 0.12f);
                SetMuscle(target, "Right Upper Leg In-Out", 0.12f);
                SetMuscle(target, "Left Arm Down-Up", -0.55f);
                SetMuscle(target, "Right Arm Down-Up", -0.55f);
                SetMuscle(target, "Left Arm Front-Back", 0.15f);
                SetMuscle(target, "Right Arm Front-Back", 0.15f);
                SetMuscle(target, "Left Forearm Stretch", -0.45f);
                SetMuscle(target, "Right Forearm Stretch", -0.45f);
                SetMuscle(target, "Spine Front-Back", 0.08f);
                AnimationClip clip = new AnimationClip { name = "Sit", frameRate = 60f, wrapMode = WrapMode.ClampForever };
                for (int i = 0; i < HumanTrait.MuscleCount; i++)
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), HumanTrait.MuscleName[i]),
                        AnimationCurve.EaseInOut(0f, pose.muscles[i], 2f, target[i]));
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, ClipPath);
                AssetDatabase.SaveAssetIfDirty(clip);
                return clip;
            }
            finally { UnityEngine.Object.DestroyImmediate(preview); }
        }

        private static void SetMuscle(float[] values, string name, float value)
        {
            int index = Array.IndexOf(HumanTrait.MuscleName, name);
            if (index < 0) throw new InvalidOperationException("Unknown Humanoid muscle: " + name);
            values[index] = value;
        }

        private static T Single<T>(GameObject[] roots) where T : Component
        {
            T[] found = roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
            if (found.Length != 1) throw new InvalidOperationException("Expected exactly one " + typeof(T).Name);
            return found[0];
        }

        private static void Assign(UnityEngine.Object obj, string field, UnityEngine.Object value)
        {
            SerializedObject data = new SerializedObject(obj);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Label(Transform parent, TMP_FontAsset font, string text, float size, float y, float height)
        {
            GameObject obj = new GameObject(text == "CLEAR" ? "ClearTitle" : "ClearMessage", typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center; label.color = Color.white; label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(1200, height);
            label.rectTransform.anchoredPosition = new Vector2(0, y);
        }

        private static TMP_FontAsset GetClearFont()
        {
            string glyphs = "CLEAR " + ClearMessage;
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null)
            {
                if (!font.HasCharacters(glyphs)) throw new InvalidOperationException("ClearFont lacks required glyphs.");
                return font;
            }
            if (File.Exists(FontPath)) throw new IOException("ClearFont exists but cannot be loaded.");
            font = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular", 48);
            if (font == null || !font.TryAddCharacters(glyphs))
                throw new InvalidOperationException("Cannot create Clear font from Malgun Gothic Regular.");
            font.name = "ClearFont";
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
