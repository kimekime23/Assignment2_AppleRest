#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Assets/Editor 에 넣으면 상단 메뉴 Tools > Apple Rest 가 생깁니다. (이전 ForestBuilder 파일은 지워주세요)  v9
// Build Forest : 구불구불한 길 + 길 위의 사과 + 햇살 드는 밑둥 빈터 + 빽빽한 숲 + 진한 안개
// Clear Forest : 숲·길·햇빛만 지웁니다. (사과 위치는 Ctrl+Z 로 되돌릴 수 있어요)
public static class ForestBuilder
{
    const string RootName = "Forest";
    const string PrefabFolder = "Assets/Prefabs/Forest";
    const string TextureFolder = "Assets/Textures/Forest";
    const int Seed = 11;

    static readonly string[] ModelNames =
        { "Env_Tree_Round", "Env_Tree_Pine", "Env_Bush_Berry", "Env_Rock_Boulder", "Env_Grass_Flowers" };

    [MenuItem("Tools/Apple Rest/Build Forest")]
    public static void Build()
    {
        var prefabs = new Dictionary<string, GameObject>();
        foreach (var n in ModelNames)
        {
            var pf = MakePrefab(n);
            if (pf == null) return;
            prefabs[n] = pf;
        }

        Clear();
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Forest");

        // ---------------------------------------------------------------- 씬 정보
        Bounds ground = new Bounds(Vector3.zero, new Vector3(32f, 0f, 32f));
        var groundObj = GameObject.Find("Ground");
        if (groundObj != null && groundObj.TryGetComponent<Renderer>(out var gRend)) ground = gRend.bounds;
        float gy = ground.max.y;
        Vector3 center = new Vector3(ground.center.x, gy, ground.center.z);
        float half = Mathf.Min(ground.extents.x, ground.extents.z);

        float s = 1f;
        var player = GameObject.Find("Player");
        if (player != null)
        {
            var rs = player.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds pb = rs[0].bounds;
                foreach (var rr in rs) pb.Encapsulate(rr.bounds);
                s = Mathf.Clamp(pb.size.y / 0.96f, 0.5f, 5f);
            }
        }
        var stump = GameObject.Find("RestStump");
        Vector3 start = player != null ? Flat0(player.transform.position, gy) : center + new Vector3(0, 0, -half * 0.6f);
        Vector3 goal = stump != null ? Flat0(stump.transform.position, gy) : center + new Vector3(0, 0, half * 0.6f);

        var apples = new List<Transform>();
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (mb.GetType().Name == "ApplePickup") apples.Add(mb.transform);
        apples.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        var rnd = new System.Random(Seed);
        float inner = half - 5.0f * s;

        // ---------------------------------------------------------------- 1) 구불구불한 길
        Vector3 dir = goal - start; dir.y = 0f;
        float dist = Mathf.Max(dir.magnitude, 0.01f); dir /= dist;
        Vector3 side = new Vector3(-dir.z, 0f, dir.x);
        // 맵을 크게 한 바퀴 돌며 완만하게 굽이치는 길 (길과 길 사이가 넓어 가로수가 들어갈 자리가 생김)
        float R = inner * 0.8f;
        float aS = Mathf.Atan2(start.z - center.z, start.x - center.x);
        float aG = Mathf.Atan2(goal.z - center.z, goal.x - center.x);
        float span = aG - aS;
        while (span <= 0f) span += Mathf.PI * 2f;
        if (span < Mathf.PI) span -= Mathf.PI * 2f;                       // 항상 먼 쪽으로 돌아감 (반 바퀴 이상)
        int steps = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(span) / (Mathf.PI / 6f)));
        var ctrl = new List<Vector3> { start };
        for (int k = 0; k <= steps; k++)
        {
            float a = aS + span * k / steps;
            float rr = R * (1f + 0.17f * (k % 2 == 0 ? 1f : -1f));        // 안팎으로 번갈아 굽이침
            ctrl.Add(new Vector3(center.x + Mathf.Cos(a) * rr, gy, center.z + Mathf.Sin(a) * rr));
        }
        Vector3 last = ctrl[ctrl.Count - 1]; last.y = gy;
        Vector3 toLast = last - goal; toLast.y = 0f;
        ctrl.Add(goal + toLast.normalized * (1.3f * s));                    // 밑둥 바로 앞에서 끝
        for (int i = 1; i < ctrl.Count - 1; i++)
        {
            var p = ctrl[i];
            p.x = Mathf.Clamp(p.x, center.x - inner, center.x + inner);
            p.z = Mathf.Clamp(p.z, center.z - inner, center.z + inner);
            ctrl[i] = new Vector3(p.x, gy, p.z);
        }
        var path = SampleCatmull(ctrl, 24);
        var cum = new List<float> { 0f };
        for (int i = 1; i < path.Count; i++) cum.Add(cum[i - 1] + Vector3.Distance(path[i - 1], path[i]));
        float pathLen = cum[cum.Count - 1];
        float pathW = 0.75f * s;

        var pathGo = new GameObject("ForestPath");
        pathGo.transform.SetParent(root.transform, false);
        pathGo.AddComponent<MeshFilter>().sharedMesh = SaveMesh(BuildRibbon(path, cum, pathW, gy + 0.015f, rnd), "ForestPath");
        pathGo.AddComponent<MeshRenderer>().sharedMaterial = SaveMat("M_ForestPath", new Color(0.55f, 0.44f, 0.31f), false);

        // ---------------------------------------------------------------- 2) 사과를 길 위에 하나씩
        for (int i = 0; i < apples.Count; i++)
        {
            float t = apples.Count == 1 ? 0.5f : Mathf.Lerp(0.10f, 0.92f, i / (float)(apples.Count - 1));
            Vector3 p = PointAt(path, cum, t * pathLen);
            Undo.RecordObject(apples[i], "Place apples on path");
            float lift = apples[i].position.y - gy;
            apples[i].position = new Vector3(p.x, gy + Mathf.Max(lift, 0f), p.z);
        }

        // ---------------------------------------------------------------- 배치 도우미
        var keepClear = new List<(Vector3 pos, float radius)>();
        keepClear.Add((start, 1.2f * s));
        if (player != null)                                                // 캐릭터 등 뒤(카메라 자리)는 비워둠
        {
            Vector3 back = -player.transform.forward; back.y = 0f; back.Normalize();
            Vector3 cpos = start + back * (3.5f * s);
            int nSeg = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(cpos, start) / (0.5f * s)));
            for (int i = 0; i <= nSeg; i++) keepClear.Add((Vector3.Lerp(start, cpos, i / (float)nSeg), 0.7f * s));
        }
        float clearR = 2.0f;
        if (stump != null)
        {
            var srs = stump.GetComponentsInChildren<Renderer>();
            if (srs.Length > 0)
            {
                Bounds sb = srs[0].bounds;
                foreach (var r0 in srs) sb.Encapsulate(r0.bounds);
                clearR = Mathf.Max(sb.extents.x, sb.extents.z) + 1.2f;          // 밑둥 크기 + 1.2 m
            }
        }
        keepClear.Add((goal, clearR));                                       // 햇살 드는 빈터
        var placed = new List<(Vector3 pos, float radius)>();
        int count = 0;

        float PathDist(Vector3 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < path.Count; i += 2) best = Mathf.Min(best, Flat(p - path[i]));
            return best;
        }
        bool Free(Vector3 p, float r, float pathMargin)
        {
            foreach (var k in keepClear) if (Flat(p - k.pos) < k.radius + r) return false;
            if (PathDist(p) < pathW * 0.5f + pathMargin + r) return false;
            foreach (var q in placed) if (Flat(p - q.pos) < q.radius + r) return false;
            return true;
        }
        void Put(string n, Vector3 p, float scale, float radius, float tilt = 0f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[n], root.transform);
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(Rand(rnd, -tilt, tilt), Rand(rnd, 0f, 360f), Rand(rnd, -tilt, tilt));
            go.transform.localScale = Vector3.one * scale;
            placed.Add((p, radius));
            count++;
        }

        // ---------------------------------------------------------------- 3) 길가: 풀과 작은 돌
        for (float d = 0.6f * s; d < pathLen - 0.5f * s; d += 1.0f * s)
        {
            Vector3 p = PointAt(path, cum, d);
            Vector3 tng = (PointAt(path, cum, Mathf.Min(d + 0.2f, pathLen)) - p).normalized;
            Vector3 nrm = new Vector3(-tng.z, 0f, tng.x);
            foreach (float sgn in new[] { -1f, 1f })
            {
                if (rnd.NextDouble() < 0.25) continue;
                Vector3 q = p + nrm * sgn * (pathW * 0.5f + Rand(rnd, 0.15f, 0.5f) * s);
                if (Flat(q - goal) < clearR * 0.6f || Flat(q - start) < 1.2f * s) continue;
                if (rnd.NextDouble() < 0.18) Put("Env_Rock_Boulder", q, Rand(rnd, 0.18f, 0.32f) * s, 0.15f * s, 8f);
                else Put("Env_Grass_Flowers", q, Rand(rnd, 0.8f, 1.25f) * s, 0.2f * s);
            }
        }

        // ---------------------------------------------------------------- 3-2) 길 양옆 가로수
        for (float d = 2.0f * s; d < pathLen - 2.0f * s; d += 2.0f * s)
        {
            Vector3 p = PointAt(path, cum, d);
            Vector3 tng = (PointAt(path, cum, Mathf.Min(d + 0.2f, pathLen)) - p).normalized;
            Vector3 nrm = new Vector3(-tng.z, 0f, tng.x);
            foreach (float sgn in new[] { -1f, 1f })
            {
                if (rnd.NextDouble() < 0.15) continue;                        // 가끔 비워서 자연스럽게
                bool pine = rnd.NextDouble() < 0.6;
                float off = pathW * 0.5f + Rand(rnd, 0.45f, 0.9f) * s + (pine ? 0f : 0.5f * s);
                Vector3 q = p + nrm * sgn * off + tng * Rand(rnd, -0.6f, 0.6f) * s;
                if (Flat(q - start) < 1.2f * s || Flat(q - goal) < clearR) continue;
                if (!Free(q, 0.2f * s, 0.1f * s)) continue;
                Put(pine ? "Env_Tree_Pine" : "Env_Tree_Round", q, Rand(rnd, 0.85f, 1.2f) * s, 0.4f * s);
            }
        }

        // ---------------------------------------------------------------- 4) 빽빽한 숲
        // 바닥 전체를 격자로 훑으며 길·빈터·출발 지점만 빼고 모두 나무로 채움
        for (float gx = -half + 0.8f * s; gx <= half - 0.8f * s; gx += 1.1f * s)
        {
            for (float gz = -half + 0.8f * s; gz <= half - 0.8f * s; gz += 1.1f * s)
            {
                if (rnd.NextDouble() < 0.1) continue;                         // 가끔 빈 칸 (자연스럽게)
                var p = center + new Vector3(gx + Rand(rnd, -0.6f, 0.6f) * s, 0, gz + Rand(rnd, -0.6f, 0.6f) * s);
                if (!Free(p, 0.2f * s, 0.1f * s)) continue;
                bool pine = PathDist(p) < pathW * 0.5f + 1.2f * s || rnd.NextDouble() < 0.5;   // 길 가까이는 가는 소나무
                Put(pine ? "Env_Tree_Pine" : "Env_Tree_Round", p, Rand(rnd, 0.85f, 1.3f) * s, 0.45f * s);
            }
        }
        // 마지막 점검: 근처(1.5 m 안)에 나무가 하나도 없는 빈 땅은 무조건 소나무로 채움
        var trees = new List<Vector3>();
        foreach (Transform t in root.transform) if (t.name.StartsWith("Env_Tree")) trees.Add(t.position);
        for (float gx = -half + 0.6f * s; gx <= half - 0.6f * s; gx += 0.6f * s)
        {
            for (float gz = -half + 0.6f * s; gz <= half - 0.6f * s; gz += 0.6f * s)
            {
                var p = center + new Vector3(gx, 0, gz);
                bool nearTree = false;
                foreach (var tp in trees) if (Flat(p - tp) < 1.5f * s) { nearTree = true; break; }
                if (nearTree) continue;
                if (!Free(p, 0.2f * s, 0.1f * s)) continue;
                Put("Env_Tree_Pine", p, Rand(rnd, 0.85f, 1.2f) * s, 0.35f * s);
                trees.Add(p);
            }
        }

        void Scatter(string n, int amount, float radius, float pathMargin, float minS, float maxS, float tilt)
        {
            int made = 0;
            for (int i = 0; i < amount * 40 && made < amount; i++)
            {
                var p = center + new Vector3(Rand(rnd, -inner - 1f, inner + 1f), 0, Rand(rnd, -inner - 1f, inner + 1f));
                if (!Free(p, radius, pathMargin)) continue;
                Put(n, p, Rand(rnd, minS, maxS) * s, radius, tilt);
                made++;
            }
        }
        Scatter("Env_Rock_Boulder", 12, 0.5f * s, 0.4f * s, 0.7f, 1.3f, 6f);
        Scatter("Env_Bush_Berry", 46, 0.38f * s, 0.15f * s, 0.8f, 1.3f, 0f);
        Scatter("Env_Grass_Flowers", 110, 0.2f * s, 0.05f * s, 0.8f, 1.4f, 0f);

        // ---------------------------------------------------------------- 4-2) 맵 바깥: 넓은 바닥 + 나무 두 겹 (빈 공간 가림)
        if (groundObj != null && groundObj.TryGetComponent<Renderer>(out var gR2))
        {
            var outerFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            outerFloor.name = "OuterFloor";
            outerFloor.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(outerFloor.GetComponent<Collider>());        // 보이기만 함 (밟을 수 없음)
            outerFloor.transform.position = new Vector3(center.x, gy - 0.02f, center.z);
            outerFloor.transform.localScale = new Vector3(ground.size.x * 0.3f, 1f, ground.size.z * 0.3f); // 바닥의 3배
            outerFloor.GetComponent<Renderer>().sharedMaterial = gR2.sharedMaterial;
        }
        foreach (float outset in new[] { 0.9f * s, 2.6f * s })
        {
            float edge = half + outset;
            for (float t = -edge; t < edge; t += 1.6f * s)
            {
                foreach (var off in new[] { new Vector3(t, 0, -edge), new Vector3(t, 0, edge), new Vector3(-edge, 0, t), new Vector3(edge, 0, t) })
                {
                    var p = center + off + new Vector3(Rand(rnd, -0.5f, 0.5f), 0, Rand(rnd, -0.5f, 0.5f)) * s;
                    bool pine = rnd.NextDouble() < 0.5;
                    Put(pine ? "Env_Tree_Pine" : "Env_Tree_Round", p, Rand(rnd, 0.9f, 1.35f) * s, 0.5f * s);
                }
            }
        }

        // ---------------------------------------------------------------- 5) 햇살 드는 빈터
        var sunGo = new GameObject("SunbeamLight");
        sunGo.transform.SetParent(root.transform, false);
        sunGo.transform.position = goal + Vector3.up * 7f * s + Vector3.back * 1.0f * s;
        sunGo.transform.rotation = Quaternion.LookRotation((goal - sunGo.transform.position).normalized);
        var spot = sunGo.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(1f, 0.86f, 0.6f);
        spot.range = 14f * s;
        spot.spotAngle = 40f;
        spot.innerSpotAngle = 20f;
        spot.intensity = 28f;
        spot.shadows = LightShadows.Soft;

        // 전체 햇빛은 살짝 낮추고 서늘하게 -> 빈터가 돋보이게
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            Undo.RecordObject(l, "Dim directional light");
            l.intensity = Mathf.Min(l.intensity, 0.75f);
            l.color = new Color(0.86f, 0.9f, 1f);
        }

        // ---------------------------------------------------------------- 6) 진한 안개
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 4f * s;
        RenderSettings.fogEndDistance = 22f * s;
        var cam = Camera.main;
        RenderSettings.fogColor = cam != null && cam.clearFlags == CameraClearFlags.SolidColor
            ? Color.Lerp(cam.backgroundColor, new Color(0.62f, 0.72f, 0.66f), 0.5f) : new Color(0.62f, 0.72f, 0.7f);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        string msg = $"숲 배치 완료: 오브젝트 {count}개\n길 길이 약 {pathLen:0.0} m, 사과 {apples.Count}개를 길 위에 배치\n캐릭터 배율 {s:0.00}";
        Debug.Log("[ForestBuilder] " + msg.Replace("\n", " / "));
        EditorUtility.DisplayDialog("Build Forest", msg + "\n\n마음에 안 들면 Ctrl+Z 또는 Clear Forest.", "확인");
    }

    [MenuItem("Tools/Apple Rest/Clear Forest")]
    public static void Clear()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    // ================================================================ 프리팹
    static GameObject MakePrefab(string name)
    {
        string modelPath = null;
        foreach (var g in AssetDatabase.FindAssets(name + " t:Model"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == name) { modelPath = p; break; }
        }
        if (modelPath == null)
        {
            EditorUtility.DisplayDialog("Build Forest", $"{name}.fbx 를 프로젝트에서 찾을 수 없어요.\nAssets 폴더 안에 넣어주세요.", "확인");
            return null;
        }
        string texDir = TextureFolder + "/" + name;
        if (!AssetDatabase.IsValidFolder(texDir))
        {
            MakeFolders(texDir);
            ((ModelImporter)AssetImporter.GetAtPath(modelPath)).ExtractTextures(texDir);
            AssetDatabase.Refresh();
        }
        foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (!p.Contains("_Normal")) continue;
            var ti = (TextureImporter)AssetImporter.GetAtPath(p);
            if (ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        }
        string prefabPath = PrefabFolder + "/" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing;

        MakeFolders(PrefabFolder);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
        inst.name = name;
        var rends = inst.GetComponentsInChildren<Renderer>();
        Bounds b = rends.Length > 0 ? rends[0].bounds : new Bounds(Vector3.zero, Vector3.one);
        foreach (var r in rends) b.Encapsulate(r.bounds);
        if (name.StartsWith("Env_Tree"))
        {
            var c = inst.AddComponent<CapsuleCollider>();
            c.radius = b.size.x * (name.EndsWith("Pine") ? 0.08f : 0.09f);
            c.height = b.size.y; c.center = new Vector3(0, b.size.y * 0.5f, 0);
        }
        else if (name == "Env_Bush_Berry")
        {
            var c = inst.AddComponent<CapsuleCollider>();
            c.radius = b.size.x * 0.4f; c.height = b.size.y; c.center = new Vector3(0, b.size.y * 0.5f, 0);
        }
        else if (name == "Env_Rock_Boulder")
        {
            var mf = inst.GetComponentInChildren<MeshFilter>();
            var c = mf.gameObject.AddComponent<MeshCollider>();
            c.sharedMesh = mf.sharedMesh; c.convex = true;
        }
        var prefab = PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
        Object.DestroyImmediate(inst);
        return prefab;
    }

    // ================================================================ 메쉬·재질
    static Mesh BuildRibbon(List<Vector3> pts, List<float> cum, float width, float y, System.Random rnd)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 a = pts[Mathf.Max(i - 1, 0)], b = pts[Mathf.Min(i + 1, pts.Count - 1)];
            Vector3 t = b - a; t.y = 0; t.Normalize();
            Vector3 n = new Vector3(-t.z, 0, t.x);
            float w = width * 0.5f * (1f + 0.12f * Mathf.Sin(cum[i] * 1.7f) + 0.06f * (float)(rnd.NextDouble() - 0.5));
            Vector3 p = new Vector3(pts[i].x, y, pts[i].z);
            v.Add(p - n * w); v.Add(p + n * w);
            uv.Add(new Vector2(0, cum[i] / width)); uv.Add(new Vector2(1, cum[i] / width));
            if (i > 0) { int k = v.Count - 4; tri.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 }); }   // 위에서 보이도록 시계 방향
        }
        var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
    static Mesh BuildDisc(float r, int seg)
    {
        var v = new List<Vector3> { Vector3.zero }; var tri = new List<int>();
        for (int i = 0; i <= seg; i++) { float a = i * Mathf.PI * 2f / seg; v.Add(new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)); }
        for (int i = 1; i <= seg; i++) tri.AddRange(new[] { 0, i + 1, i });
        var m = new Mesh(); m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); return m;
    }
    static Mesh BuildCone(float rTop, float rBottom, float h, int seg)
    {
        var v = new List<Vector3>(); var col = new List<Color>(); var tri = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            v.Add(d * rTop + Vector3.up * h); col.Add(new Color(1, 1, 1, 0.16f));    // 위쪽: 은은하게
            v.Add(d * rBottom); col.Add(new Color(1, 1, 1, 0f));                     // 바닥: 투명
            if (i > 0) { int k = v.Count - 4; tri.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
        }
        var m = new Mesh(); m.SetVertices(v); m.SetColors(col); m.SetTriangles(tri, 0); m.RecalculateNormals(); return m;
    }
    static Mesh SaveMesh(Mesh m, string name)
    {
        MakeFolders(PrefabFolder);
        string p = PrefabFolder + "/" + name + ".asset";
        AssetDatabase.DeleteAsset(p);
        AssetDatabase.CreateAsset(m, p);
        return m;
    }
    static Material SaveMat(string name, Color c, bool unused)
    {
        string p = PrefabFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(sh); AssetDatabase.CreateAsset(m, p);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(m);
        return m;
    }
    static Material ShaftMaterial()
    {
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) return null;
        string p = PrefabFolder + "/M_SunbeamShaft.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f);             // 투명 + 가산(빛처럼 더해짐)
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.One);
        m.SetInt("_ZWrite", 0); m.SetFloat("_Cull", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", new Color(1f, 0.88f, 0.62f, 0.55f));
        EditorUtility.SetDirty(m);
        return m;
    }

    // ================================================================ 곡선
    static List<Vector3> SampleCatmull(List<Vector3> c, int perSeg)
    {
        var o = new List<Vector3>();
        for (int i = 0; i < c.Count - 1; i++)
        {
            Vector3 p0 = c[Mathf.Max(i - 1, 0)], p1 = c[i], p2 = c[i + 1], p3 = c[Mathf.Min(i + 2, c.Count - 1)];
            for (int j = 0; j < perSeg; j++)
            {
                float t = j / (float)perSeg, t2 = t * t, t3 = t2 * t;
                o.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        o.Add(c[c.Count - 1]);
        return o;
    }
    static Vector3 PointAt(List<Vector3> pts, List<float> cum, float d)
    {
        for (int i = 1; i < pts.Count; i++)
            if (cum[i] >= d) { float f = (d - cum[i - 1]) / Mathf.Max(cum[i] - cum[i - 1], 1e-5f); return Vector3.Lerp(pts[i - 1], pts[i], f); }
        return pts[pts.Count - 1];
    }

    static void MakeFolders(string path)
    {
        string cur = "";
        foreach (var part in path.Split('/'))
        {
            string next = cur == "" ? part : cur + "/" + part;
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, part);
            cur = next;
        }
    }
    static Vector3 Flat0(Vector3 v, float y) => new Vector3(v.x, y, v.z);
    static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
}
#endif
