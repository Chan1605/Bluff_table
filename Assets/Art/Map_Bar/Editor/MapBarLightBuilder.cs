using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LiarsBar.Tools
{
    // Map_Bar.fbx → Map_Bar.prefab(Variant) 생성 + 원작 조명(Lights_High) 배치
    public static class MapBarLightBuilder
    {
        private const string RootFolder = "Assets/Art/Map_Bar";
        private const string FbxPath = RootFolder + "/Map_Bar.fbx";
        private const string PrefabPath = RootFolder + "/Map_Bar.prefab";
        private const string JsonPath = RootFolder + "/Map_Bar_Lights.json";
        private const string LightsRootName = "Lights";

        // HDRP 밝기(칸델라) → URP 밝기: Scale * 0.2 * sqrt(raw)
        private const float IntensityScale = 1f;
        private const float MinIntensity = 0.05f;
        private const float MaxIntensity = 10f;

        // 그림자 켤 라이트 수 (밝은 순)
        private const int MaxShadowLights = 3;

        // 앵커 중앙값 오차 허용 (m)
        private const float MaxMedianError = 0.5f;

        // 반복 정렬 때 남길 앵커 비율 · 횟수
        private const float KeepRatio = 0.5f;
        private const int RefineIterations = 3;

        [Serializable]
        private class LightData
        {
            public LightEntry[] lights;
            public AnchorEntry[] anchors;
        }

        [Serializable]
        private class LightEntry
        {
            public string name;
            public string group;
            public string type;
            public float[] position;
            public float[] direction;
            public float[] color;
            public bool useTemperature;
            public float temperature;
            public float rawIntensity;
            public float range;
            public float spotAngle;
            public float innerSpotAngle;
            public bool shadows;
        }

        [Serializable]
        private class AnchorEntry
        {
            public string name;
            public float[] center;
        }

        // 축 순서 · 부호 · 배율 · 이동
        private struct Mapping
        {
            public int[] Order;
            public Vector3 Sign;
            public float Scale;
            public Vector3 Offset;
            public float Error;

            public Vector3 Axes(Vector3 v) => new Vector3(Sign.x * v[Order[0]], Sign.y * v[Order[1]], Sign.z * v[Order[2]]);
            public Vector3 Point(Vector3 p) => Axes(p) * Scale + Offset;
            public Vector3 Direction(Vector3 d) => Axes(d).normalized;
            public override string ToString() => $"축 ({Order[0]},{Order[1]},{Order[2]}) 부호 {Sign}";
        }

        [MenuItem("Tools/Map_Bar/Build Prefab With Lights")]
        private static void Build()
        {
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (json == null || model == null)
            {
                Debug.LogError($"[Map_Bar] 파일이 없습니다: {(json == null ? JsonPath : FbxPath)}");
                return;
            }

            LightData data = JsonUtility.FromJson<LightData>(json.text);
            if (data?.lights == null || data.anchors == null)
            {
                Debug.LogError("[Map_Bar] 조명 데이터가 비어 있습니다.");
                return;
            }

            EnsurePrefabVariant(model);

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (!TryFitMapping(root.transform, data.anchors, out Mapping mapping, out int anchorCount))
                    return;

                Transform lightsRoot = RebuildLightsRoot(root.transform);
                int created = CreateLights(lightsRoot, data.lights, mapping);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[Map_Bar] 라이트 {created}개 배치 완료 → {PrefabPath} " +
                          $"(앵커 {anchorCount}개, {mapping}, 배율 {mapping.Scale:F3}, 중앙값 오차 {mapping.Error:F3}m)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsurePrefabVariant(GameObject model)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Map_Bar";
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            Debug.Log($"[Map_Bar] Prefab Variant 생성: {PrefabPath}");
        }

        // 원작 좌표 → 프리팹 좌표 (축 뒤집힘 · 배율 · 이동) 추정
        private static bool TryFitMapping(Transform root, AnchorEntry[] anchors, out Mapping best, out int count)
        {
            best = default;
            count = 0;

            Dictionary<string, Transform> byName = new Dictionary<string, Transform>();
            HashSet<string> duplicated = new HashSet<string>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (byName.ContainsKey(t.name)) duplicated.Add(t.name);
                else byName[t.name] = t;
            }

            List<string> names = new List<string>();
            List<Vector3> source = new List<Vector3>();
            List<Vector3> target = new List<Vector3>();
            foreach (AnchorEntry anchor in anchors)
            {
                if (duplicated.Contains(anchor.name) || !byName.TryGetValue(anchor.name, out Transform t)) continue;
                Renderer renderer = t.GetComponent<Renderer>();
                if (renderer == null) continue;

                // 루트 배율 · 회전과 무관하게 월드 좌표로 비교
                names.Add(anchor.name);
                source.Add(ToVector(anchor.center));
                target.Add(renderer.bounds.center);
            }

            count = source.Count;
            if (count < 4)
            {
                Debug.LogError($"[Map_Bar] 위치 기준 오브젝트가 {count}개뿐이라 좌표를 맞출 수 없습니다.");
                return false;
            }

            best.Error = float.MaxValue;
            foreach (int[] order in AxisOrders())
                foreach (Vector3 sign in AxisSigns())
                {
                    Mapping m = Fit(source, target, order, sign);
                    if (m.Error < best.Error) best = m;
                }

            // 오차 작은 앵커만 남기며 반복 정렬
            List<int> kept = Enumerable.Range(0, source.Count).ToList();
            for (int iteration = 0; iteration < RefineIterations; iteration++)
            {
                Mapping current = best;
                List<int> next = kept
                    .OrderBy(i => Vector3.Distance(current.Point(source[i]), target[i]))
                    .Take(Mathf.Max(4, Mathf.CeilToInt(kept.Count * KeepRatio)))
                    .ToList();
                best = Fit(next.Select(i => source[i]).ToList(), next.Select(i => target[i]).ToList(), best.Order, best.Sign);
                kept = next;
            }
            count = kept.Count;

            Mapping final = best;
            List<int> worst = Enumerable.Range(0, source.Count)
                .OrderByDescending(i => Vector3.Distance(final.Point(source[i]), target[i]))
                .Take(5).ToList();
            Debug.Log("[Map_Bar] 오차 큰 앵커: " + string.Join(", ",
                worst.Select(i => $"{names[i]} {Vector3.Distance(final.Point(source[i]), target[i]):F2}m")));

            if (best.Error > MaxMedianError)
            {
                Debug.LogError($"[Map_Bar] 좌표 오차가 큽니다 (중앙값 {best.Error:F2}m, {best}, 배율 {best.Scale:F3}). 라이트를 만들지 않았습니다.");
                return false;
            }
            return true;
        }

        private static IEnumerable<int[]> AxisOrders()
        {
            yield return new[] { 0, 1, 2 };
            yield return new[] { 0, 2, 1 };
            yield return new[] { 1, 0, 2 };
            yield return new[] { 1, 2, 0 };
            yield return new[] { 2, 0, 1 };
            yield return new[] { 2, 1, 0 };
        }

        private static IEnumerable<Vector3> AxisSigns()
        {
            foreach (float x in new[] { 1f, -1f })
                foreach (float y in new[] { 1f, -1f })
                    foreach (float z in new[] { 1f, -1f })
                        yield return new Vector3(x, y, z);
        }

        private static Mapping Fit(List<Vector3> source, List<Vector3> target, int[] order, Vector3 sign)
        {
            Mapping m = new Mapping { Order = order, Sign = sign };
            Vector3 sourceMean = Mean(source);
            Vector3 targetMean = Mean(target);

            float numerator = 0f;
            float denominator = 0f;
            for (int i = 0; i < source.Count; i++)
            {
                Vector3 p = m.Axes(source[i] - sourceMean);
                numerator += Vector3.Dot(p, target[i] - targetMean);
                denominator += p.sqrMagnitude;
            }

            m.Scale = denominator > 0f ? numerator / denominator : 1f;
            m.Offset = targetMean - m.Axes(sourceMean) * m.Scale;

            if (m.Scale <= 0f)
            {
                m.Error = float.MaxValue;
                return m;
            }

            // 중앙값 오차 (튀는 앵커 영향 줄임)
            List<float> errors = new List<float>(source.Count);
            for (int i = 0; i < source.Count; i++)
                errors.Add(Vector3.Distance(m.Point(source[i]), target[i]));
            errors.Sort();
            m.Error = errors[errors.Count / 2];
            return m;
        }

        private static Transform RebuildLightsRoot(Transform root)
        {
            Transform old = root.Find(LightsRootName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            GameObject lightsRoot = new GameObject(LightsRootName);
            lightsRoot.transform.SetParent(root, false);
            return lightsRoot.transform;
        }

        private static int CreateLights(Transform lightsRoot, LightEntry[] entries, Mapping mapping)
        {
            HashSet<LightEntry> shadowed = new HashSet<LightEntry>(
                entries.Where(e => e.shadows).OrderByDescending(e => e.rawIntensity).Take(MaxShadowLights));

            Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
            int created = 0;
            foreach (LightEntry entry in entries)
            {
                Transform parent = GetGroup(lightsRoot, groups, entry.group);
                GameObject go = new GameObject(entry.name);
                go.transform.SetParent(parent, false);
                go.transform.position = mapping.Point(ToVector(entry.position));

                Vector3 forward = mapping.Direction(ToVector(entry.direction));
                if (forward.sqrMagnitude > 0f)
                {
                    Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
                    go.transform.rotation = Quaternion.LookRotation(forward, up);
                }

                Light light = go.AddComponent<Light>();
                light.type = entry.type == "Spot" ? LightType.Spot : LightType.Point;
                light.color = new Color(entry.color[0], entry.color[1], entry.color[2]);
                light.useColorTemperature = entry.useTemperature;
                light.colorTemperature = entry.temperature;
                light.intensity = Mathf.Clamp(IntensityScale * 0.2f * Mathf.Sqrt(entry.rawIntensity), MinIntensity, MaxIntensity);
                light.range = entry.range * mapping.Scale;
                if (light.type == LightType.Spot)
                {
                    light.spotAngle = Mathf.Clamp(entry.spotAngle, 1f, 179f);
                    light.innerSpotAngle = Mathf.Clamp(entry.innerSpotAngle, 0f, light.spotAngle);
                }
                light.shadows = shadowed.Contains(entry) ? LightShadows.Soft : LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;
                created++;
            }
            return created;
        }

        private static Transform GetGroup(Transform lightsRoot, Dictionary<string, Transform> groups, string name)
        {
            string key = string.IsNullOrEmpty(name) ? "Etc" : name;
            if (groups.TryGetValue(key, out Transform group)) return group;

            group = new GameObject(key).transform;
            group.SetParent(lightsRoot, false);
            groups[key] = group;
            return group;
        }

        private static Vector3 Mean(List<Vector3> points)
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector3 p in points) sum += p;
            return sum / points.Count;
        }

        private static Vector3 ToVector(float[] values)
        {
            return values != null && values.Length >= 3 ? new Vector3(values[0], values[1], values[2]) : Vector3.zero;
        }
    }
}
