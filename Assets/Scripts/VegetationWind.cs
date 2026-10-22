using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AppleRest
{
    public sealed class VegetationWind : MonoBehaviour
    {
        [SerializeField] private Shader windShader;
        [SerializeField] private MeshRenderer[] vegetation = new MeshRenderer[0];
        [Range(0f, 0.15f)] [SerializeField] private float strength = 0.055f;
        [Range(0.1f, 3f)] [SerializeField] private float speed = 1f;
        [SerializeField] private Vector2 direction = new Vector2(1f, 0.35f);
        private sealed class SavedRenderer
        {
            public MeshRenderer renderer;
            public Material[] materials;
            public MaterialPropertyBlock properties;
            public Bounds localBounds;
            public MotionVectorGenerationMode motionVectors;
        }
        private readonly List<SavedRenderer> saved = new List<SavedRenderer>();
        private readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        private bool started;
        private static readonly int Data = Shader.PropertyToID("_AppleRestVegetationData");
        private static readonly int Settings = Shader.PropertyToID("_AppleRestWindSettings");
        private static readonly int Clock = Shader.PropertyToID("_AppleRestWindTime");

        // Start runs after ThornBush.Awake, preserving its copied red material and textures.
        private void Start() { started = true; Initialize(); }
        private void OnEnable() { if (started) Initialize(); }
        private void Initialize()
        {
            if (saved.Count != 0) return;
            if (windShader == null || !windShader.isSupported)
            { Debug.LogError("[AppleRest] Wind shader missing or unsupported. Original vegetation rendering retained.", this); return; }
            foreach (MeshRenderer renderer in vegetation)
            {
                if (renderer == null) continue;
                Material[] originals = renderer.sharedMaterials;
                // The wind shader follows URP Lit; leave unrelated custom shaders untouched.
                if (originals.Length == 0 || System.Array.Exists(originals,
                    m => m == null || m.shader == null || m.shader.name != "Universal Render Pipeline/Lit")) continue;
                Bounds bounds = renderer.bounds;
                float height = Mathf.Max(bounds.size.y, 0.01f);
                MaterialPropertyBlock originalBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(originalBlock);
                saved.Add(new SavedRenderer { renderer = renderer, materials = originals,
                    properties = originalBlock, localBounds = renderer.localBounds, motionVectors = renderer.motionVectorGenerationMode });
                Material[] replacements = new Material[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                {
                    if (!copies.TryGetValue(originals[i], out Material copy))
                    {
                        copy = new Material(originals[i]) { name = originals[i].name + " (Runtime Wind)", hideFlags = HideFlags.DontSave };
                        copy.shader = windShader;
                        copy.SetShaderPassEnabled("MotionVectors", false);
                        copy.SetShaderPassEnabled("XRMotionVectors", false);
                        copies.Add(originals[i], copy);
                    }
                    replacements[i] = copy;
                }
                renderer.sharedMaterials = replacements;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                MaterialPropertyBlock windBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(windBlock);
                float phase = bounds.center.x * 0.61f + bounds.center.z * 0.83f;
                windBlock.SetVector(Data, new Vector4(bounds.min.y, 1f / height, height, phase));
                renderer.SetPropertyBlock(windBlock);
                // Reserve the maximum configured sway for frustum/shadow culling; no collider changes.
                Vector3 scale = renderer.transform.lossyScale;
                Bounds local = renderer.localBounds;
                float padding = height * 0.16f;
                local.Expand(new Vector3(padding / Mathf.Max(Mathf.Abs(scale.x), 0.001f),
                    padding / Mathf.Max(Mathf.Abs(scale.y), 0.001f),
                    padding / Mathf.Max(Mathf.Abs(scale.z), 0.001f)) * 2f);
                renderer.localBounds = local;
            }
            PushGlobals();
        }

        private void Update() => PushGlobals();
        private void PushGlobals()
        {
            Vector2 wind = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Shader.SetGlobalVector(Settings, new Vector4(wind.x, wind.y, speed, strength));
            Shader.SetGlobalFloat(Clock, Time.unscaledTime); // Ambient wind continues on the start screen.
        }
        private void OnDisable()
        {
            foreach (SavedRenderer item in saved)
            {
                if (item.renderer == null) continue;
                item.renderer.sharedMaterials = item.materials;
                item.renderer.SetPropertyBlock(item.properties);
                item.renderer.localBounds = item.localBounds;
                item.renderer.motionVectorGenerationMode = item.motionVectors;
            }
            foreach (Material material in copies.Values) if (material != null) Destroy(material);
            saved.Clear(); copies.Clear();
        }
    }
}
