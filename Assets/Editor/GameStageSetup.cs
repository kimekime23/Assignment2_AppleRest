using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AppleRest.Editor
{
    public static class GameStageSetup
    {
        private const string ScenePath = "Assets/Scenes/GameStage.unity";
        private const string ModelPath = "Assets/Art/Models/Char_ChibiGirl_Final.fbx";
        private const string ControllerPath = "Assets/Animations/PlayerLocomotion.controller";

        // Intentionally manual: importing or recompiling scripts must not create scenes.
        [MenuItem("AppleRest/Create GameStage (reuse existing controller)")]
        public static void Build()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Assignment2_AppleRest/Assets"))
                throw new InvalidOperationException("Setup is restricted to Assignment2_AppleRest.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                Debug.LogWarning("Stop Play Mode and wait for compilation before creating GameStage.");
                return;
            }
            if (File.Exists(ScenePath))
            {
                Debug.Log("GameStage already exists. Open it from Assets/Scenes; it will not be overwritten.");
                return;
            }
            try
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
                // FBX preview clips can also end in Idle/Walk. Select only the named imported clips.
                AnimationClip idle = clips.SingleOrDefault(c => c.name == "Rig|Idle");
                AnimationClip walk = clips.SingleOrDefault(c => c.name == "Rig|Walk");
                Animator importedAnimator = model != null ? model.GetComponentInChildren<Animator>() : null;
                if (idle == null || walk == null || importedAnimator == null || importedAnimator.avatar == null ||
                    !importedAnimator.avatar.isValid || !importedAnimator.avatar.isHuman)
                    throw new InvalidOperationException("Character/Idle/Walk/valid Humanoid Avatar is missing. Original assets were not modified.");
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) throw new InvalidOperationException("URP Lit shader is not available.");
                if (model.GetComponentsInChildren<Renderer>().Length == 0)
                    throw new InvalidOperationException("Character has no renderers. Original assets were not modified.");
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                if (File.Exists(ControllerPath) && controller == null)
                    throw new InvalidOperationException("Existing controller could not be loaded; it will not be overwritten.");
                if (controller != null)
                {
                    RepairClipReferences(controller, idle, walk);
                    ValidateController(controller, idle, walk);
                }
                // The user invokes this menu. Let them save/cancel before replacing the open scene.
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                foreach (string folder in new[] { "Scenes", "Animations", "Materials" })
                    if (!AssetDatabase.IsValidFolder("Assets/" + folder)) AssetDatabase.CreateFolder("Assets", folder);
                if (controller == null)
                {
                    controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                    controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                    AnimatorStateMachine states = controller.layers[0].stateMachine;
                    AnimatorState idleState = states.AddState("Idle", new Vector3(250, 50));
                    AnimatorState walkState = states.AddState("Walk", new Vector3(500, 50));
                    idleState.motion = idle;
                    walkState.motion = walk;
                    idleState.writeDefaultValues = false;
                    walkState.writeDefaultValues = false;
                    states.defaultState = idleState;
                    Transition(idleState, walkState, AnimatorConditionMode.Greater);
                    Transition(walkState, idleState, AnimatorConditionMode.Less);
                    EditorUtility.SetDirty(controller);
                }
                SceneManager.SetActiveScene(scene);
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Ground";
                ground.transform.position = new Vector3(0f, -0.25f, 0f);
                ground.transform.localScale = new Vector3(32f, 0.5f, 32f);
                const string floorPath = "Assets/Materials/StageGround.mat";
                Material floor = AssetDatabase.LoadAssetAtPath<Material>(floorPath);
                if (floor == null)
                {
                    if (File.Exists(floorPath)) throw new IOException("Existing floor material could not be loaded.");
                    floor = new Material(lit) { name = "StageGround" };
                    floor.SetColor("_BaseColor", new Color(0.32f, 0.46f, 0.30f));
                    AssetDatabase.CreateAsset(floor, floorPath);
                }
                ground.GetComponent<Renderer>().sharedMaterial = floor;

                GameObject player = new GameObject("Player");
                player.tag = "Player";
                player.transform.position = new Vector3(0f, 0.05f, 0f);
                CharacterController capsule = player.AddComponent<CharacterController>();
                capsule.height = 1.8f;
                capsule.radius = 0.3f;
                capsule.center = Vector3.up * 0.9f;
                capsule.stepOffset = 0.25f;
                capsule.skinWidth = 0.03f;
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                visual.name = "CharacterVisual";
                visual.transform.SetParent(player.transform, false);
                Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                float scale = 1.8f / Mathf.Max(bounds.size.y, 0.01f);
                visual.transform.localScale *= scale;
                bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                visual.transform.position += Vector3.up * (player.transform.position.y - bounds.min.y);
                Animator animator = visual.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);

                GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.fieldOfView = 55f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.55f, 0.72f, 0.84f);
                cameraObject.transform.position = player.transform.position + new Vector3(0f, 3.4f, -5.5f);
                cameraObject.transform.LookAt(player.transform.position + Vector3.up * 1.1f);
                AppleRestCamera follow = cameraObject.AddComponent<AppleRestCamera>();
                Assign(follow, "target", player.transform);
                AppleRestPlayer movement = player.AddComponent<AppleRestPlayer>();
                Assign(movement, "characterAnimator", animator);
                Assign(movement, "viewCamera", cameraObject.transform);
                GameObject lightObject = new GameObject("Directional Light", typeof(Light));
                Light light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.5f;
                light.shadows = LightShadows.Soft;
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
                RenderSettings.sun = light;
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Scene save failed.");
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath)
                    .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
                string result = "GameStage created. Humanoid Avatar valid; Idle/Walk connected; root motion off; scene saved. Visual scale=" + scale;
                File.WriteAllText("Library/AppleRestSetupResult.txt", result);
                Debug.Log("[AppleRest] " + result);
            }
            catch (Exception error)
            {
                File.WriteAllText("Library/AppleRestSetupResult.txt", error.ToString());
                Debug.LogException(error);
            }
        }

        private static void RepairClipReferences(AnimatorController controller, AnimationClip idle, AnimationClip walk)
        {
            // This assignment controller is owned by this setup tool. Preserve its states and transitions.
            if (controller.layers.Length == 0) return;
            bool changed = false;
            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                AnimationClip expected = child.state.name == "Idle" ? idle : child.state.name == "Walk" ? walk : null;
                if (expected == null || child.state.motion == expected) continue;
                child.state.motion = expected;
                EditorUtility.SetDirty(child.state);
                changed = true;
            }
            if (!changed) return;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            Debug.Log("[AppleRest] Rebound Idle/Walk to the exact imported FBX clips; source FBX unchanged.");
        }

        private static void ValidateController(AnimatorController controller, AnimationClip idle, AnimationClip walk)
        {
            if (controller.layers.Length == 0 || !controller.parameters.Any(p =>
                p.name == "Speed" && p.type == AnimatorControllerParameterType.Float))
                throw new InvalidOperationException("Existing controller has no Base Layer or float Speed. It was not modified.");
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idleState = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle");
            AnimatorState walkState = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Walk");
            if (idleState == null || walkState == null)
                throw new InvalidOperationException("Controller is missing the Idle or Walk state.");
            if (idleState.motion != idle || walkState.motion != walk)
                throw new InvalidOperationException("Controller Motion references do not match Rig|Idle / Rig|Walk.");
            if (machine.defaultState != idleState)
                throw new InvalidOperationException("Controller default state must be Idle.");
            if (!HasTransition(idleState, walkState, AnimatorConditionMode.Greater))
                throw new InvalidOperationException("Idle -> Walk must use Speed > 0.1, no Exit Time, fixed duration 0.15.");
            if (!HasTransition(walkState, idleState, AnimatorConditionMode.Less))
                throw new InvalidOperationException("Walk -> Idle must use Speed < 0.1, no Exit Time, fixed duration 0.15.");
        }

        private static bool HasTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
        {
            return from.transitions.Any(t => t.destinationState == to && !t.hasExitTime && !t.mute &&
                t.hasFixedDuration && Mathf.Approximately(t.duration, 0.15f) && t.conditions.Length == 1 &&
                t.conditions.Any(c => c.parameter == "Speed" && c.mode == mode && Mathf.Approximately(c.threshold, 0.1f)));
        }

        private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Transition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.15f;
            transition.hasFixedDuration = true;
            transition.AddCondition(mode, 0.1f, "Speed");
        }
    }
}
