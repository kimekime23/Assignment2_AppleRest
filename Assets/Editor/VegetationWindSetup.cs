using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AppleRest.Editor
{
    public static class VegetationWindSetup
    {
        [MenuItem("AppleRest/Add or Refresh Grass and Bush Wind")]
        public static void Build()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Assignment2_AppleRest/Assets"))
                throw new InvalidOperationException("Restricted to Assignment2_AppleRest.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GameStage.unity") throw new InvalidOperationException("Open GameStage first.");
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Shaders/AppleRestVegetationWind.shader");
            if (shader == null) throw new InvalidOperationException("Wind shader asset is missing.");
            if (ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Wind shader import errors:\n" + string.Join("\n",
                    ShaderUtil.GetShaderMessages(shader).Select(m => $"{m.file}:{m.line} {m.message}")));
            GameObject[] roots = scene.GetRootGameObjects();
            MeshRenderer[] renderers = roots.SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).Where(r =>
            {
                MeshFilter mesh = r.GetComponent<MeshFilter>();
                if (mesh == null || mesh.sharedMesh == null) return false;
                string path = AssetDatabase.GetAssetPath(mesh.sharedMesh);
                return path == "Assets/Art/Models/Env_Bush_Berry.fbx" || path == "Assets/Art/Models/Env_Grass_Flowers.fbx";
            }).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("No grass or berry bush renderers found in GameStage.");
            VegetationWind[] existing = roots.SelectMany(r => r.GetComponentsInChildren<VegetationWind>(true)).ToArray();
            if (existing.Length > 1) throw new InvalidOperationException("Keep exactly one VegetationWind controller.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Connect grass and bush wind");
            try
            {
                VegetationWind controller;
                if (existing.Length == 1) controller = existing[0];
                else
                {
                    GameObject obj = new GameObject("VegetationWind");
                    SceneManager.MoveGameObjectToScene(obj, scene);
                    Undo.RegisterCreatedObjectUndo(obj, "Create wind controller");
                    controller = obj.AddComponent<VegetationWind>();
                }
                Undo.RecordObject(controller, "Refresh vegetation targets");
                SerializedObject data = new SerializedObject(controller);
                data.FindProperty("windShader").objectReferenceValue = shader;
                SerializedProperty targets = data.FindProperty("vegetation");
                targets.arraySize = renderers.Length;
                for (int i = 0; i < renderers.Length; i++) targets.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log($"[AppleRest] Wind linked to {renderers.Length} grass/bush renderers (including placed ThornBush instances). Save GameStage. Strength/Speed/Direction are on VegetationWind. No vegetation transforms, colliders or source materials changed.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }
    }
}
