using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// 원작 추출 프로젝트에서 가져온 RoundTable_OG · Lobby_OG(Assets/_Export/Env)를 우리 씬에 설치한다.
// 0) 로비 텍스처 축소  1) 머티리얼 URP Lit 변환 + 조명 HDRP 밝기 → URP 환산  2) InGame 테이블 교체
// 3) LobbyScene 대기실 환경 교체  4) 대기실 좌석 · 카메라 · 분위기를 원작 로비에 맞춤
// 메뉴: Tools / 원작 환경 /
public static class OriginalEnvInstaller
{
    private const string Menu = "Tools/원작 환경/";
    private const string TablePrefab = "Assets/_Export/Env/RoundTable_OG.prefab";
    private const string LobbyPrefab = "Assets/_Export/Env/Lobby_OG.prefab";
    private const string MaterialConvertMenu = "Tools/원작 캐릭터/HDRP 머티리얼 → URP Lit 변환 (선택 + 자식)";

    private const float TableScale = 1.5f / 0.9f;   // 테이블 씬 = 원작 × 1.667 (Sit 캐릭터 · 의자와 같음)
    private const float LobbyScale = 1f / 0.9f;     // 대기실 = 원작 ÷ 0.9 (Stand 캐릭터와 같음)
    private const float SpotHeight = 1f;            // Player 프리팹 LobbyModelRoot가 y -1 → 스팟은 바닥 + 1
    private const int MaxShadowLights = 3;
    private const string ConvertedMarker = "_URPConverted";   // 조명 변환을 두 번 하지 않도록 남기는 표시
    private const string LobbyVolumeProfile = "Assets/_Export/Env/LobbyVolumeProfile.asset";

    // 대기실 분위기 값 (4번 메뉴를 다시 실행하면 씬 · 볼륨에 다시 들어감). Play 중 Inspector로 맞춰 보고 여기에 옮겨 적는다
    private static readonly Color LobbyAmbient = new Color(0.32f, 0.29f, 0.27f);   // 거의 중립색. 붉은 기는 원작 조명 색에서 나오게
    private const float LobbyFogDensity = 0.01f;    // 앞(3m)은 거의 그대로, 뒤쪽 벽만 흐리게
    private const float LobbyExposure = 1f;         // 볼륨 Post Exposure
    private const float KeyLightIntensity = 10f;    // 카메라 위에서 좌석 줄을 비추는 스포트라이트
    private static readonly Color KeyLightColor = new Color(1f, 0.78f, 0.55f);   // 원작 앞쪽처럼 따뜻한 주황빛 (볼륨 색 필터 대신 조명에만)
    private const float KeyLightAngle = 100f;       // 양 끝 좌석(Slot3 · Slot4)까지 들어오도록
    private const float LobbyLightBoost = 2.5f;     // 원작 로비 조명(HDRP → URP 환산값)에 곱함. 라이트맵이 없어 부족한 밝기 보충

    // ───── 0. 로비 텍스처 줄이기 ─────
    // 원작 로비 텍스처는 4K PNG가 많아 레포가 수 GB로 커진다. 원본 파일 자체를 1024px 이하로 다시 저장한다
    // (Inspector Max Size는 빌드 크기만 줄이고 레포 크기는 그대로라 소용없음).
    // 캐릭터 · 테이블 · 다른 씬에서도 쓰는 텍스처는 건드리지 않는다. 3번(대기실 교체) 전에 실행

    private const int MaxTextureSize = 1024;

    [MenuItem(Menu + "0. 로비 텍스처 줄이기 (3번 전에 한 번)")]
    private static void ShrinkLobbyTextures()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefab) == null)
        {
            Debug.LogError($"[원작 환경] {LobbyPrefab}이 없습니다.");
            return;
        }

        // 로비 말고 다른 프리팹 · 씬이 쓰는 텍스처 (LobbyScene은 3번 이후 로비를 품으므로 제외)
        string[] otherRoots = AssetDatabase.FindAssets("t:Prefab t:Scene", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p != LobbyPrefab && !p.EndsWith("/LobbyScene.unity"))
            .ToArray();
        var shared = new HashSet<string>(AssetDatabase.GetDependencies(otherRoots, true));

        string[] lobbyTextures = AssetDatabase.GetDependencies(LobbyPrefab, true)
            .Where(p => AssetImporter.GetAtPath(p) is TextureImporter)
            .ToArray();
        string[] targets = lobbyTextures.Where(p => !shared.Contains(p)).ToArray();

        long before = 0, after = 0;
        int resized = 0, small = 0, skipped = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < targets.Length; i++)
            {
                string path = targets[i];
                if (EditorUtility.DisplayCancelableProgressBar("로비 텍스처 줄이기", path, (float)i / targets.Length)) break;

                string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") { skipped++; continue; }

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                if (Mathf.Max(w, h) <= MaxTextureSize) { small++; continue; }

                byte[] source = System.IO.File.ReadAllBytes(path);
                byte[] result = Downscale(source, w, h, ext == ".png");
                if (result == null) { skipped++; continue; }

                System.IO.File.WriteAllBytes(path, result);
                before += source.Length;
                after += result.Length;
                resized++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[원작 환경] 로비 텍스처 줄이기: 대상 {targets.Length}장 중 {resized}장을 {MaxTextureSize}px 이하로 " +
                  $"({before / 1048576f:0} MB → {after / 1048576f:0} MB), 이미 작음 {small}장, 건너뜀 {skipped}장, 캐릭터 · 테이블과 공유라 제외 {lobbyTextures.Length - targets.Length}장");
    }

    // 색 변환 없이(Linear) 절반씩 줄여 가며 축소 → 노멀맵 · 마스크맵 값도 그대로 유지
    private static byte[] Downscale(byte[] bytes, int width, int height, bool png)
    {
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        if (!src.LoadImage(bytes)) { Object.DestroyImmediate(src); return null; }
        src.filterMode = FilterMode.Bilinear;

        float scale = (float)MaxTextureSize / Mathf.Max(width, height);
        int targetW = Mathf.Max(1, Mathf.RoundToInt(width * scale));
        int targetH = Mathf.Max(1, Mathf.RoundToInt(height * scale));

        RenderTexture previous = RenderTexture.active;
        Texture current = src;
        RenderTexture currentRt = null;
        int w = width, h = height;
        while (w > targetW || h > targetH)
        {
            w = Mathf.Max(targetW, w / 2);
            h = Mathf.Max(targetH, h / 2);
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            rt.filterMode = FilterMode.Bilinear;
            Graphics.Blit(current, rt);
            if (currentRt != null) RenderTexture.ReleaseTemporary(currentRt);
            currentRt = rt;
            current = rt;
        }

        RenderTexture.active = currentRt;
        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        dst.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        dst.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(currentRt);

        byte[] encoded = png ? dst.EncodeToPNG() : dst.EncodeToJPG(90);
        Object.DestroyImmediate(src);
        Object.DestroyImmediate(dst);
        return encoded;
    }

    // ───── 1. 머티리얼 · 조명 변환 ─────

    [MenuItem(Menu + "1. 머티리얼 · 조명 변환")]
    private static void ConvertAssets()
    {
        GameObject table = AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefab);
        GameObject lobby = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefab);
        if (table == null || lobby == null)
        {
            Debug.LogError($"[원작 환경] 프리팹이 없습니다. OriginalEnv.unitypackage를 먼저 Import 하세요. ({TablePrefab}, {LobbyPrefab})");
            return;
        }

        // 기존 캐릭터용 변환기를 그대로 사용 (선택한 프리팹 + 자식의 .mat 전부)
        Selection.objects = new Object[] { table, lobby };
        if (!EditorApplication.ExecuteMenuItem(MaterialConvertMenu))
        {
            Debug.LogError($"[원작 환경] '{MaterialConvertMenu}' 메뉴를 못 찾았습니다 (OriginalCharacterTools.cs 확인).");
            return;
        }

        Debug.Log("[원작 환경] " + ConvertLights(LobbyPrefab));
    }

    // Map_Bar와 같은 환산: URP 밝기 = 0.2 × √(HDRP 값), 0.05~10. Range는 대기실 배율만큼 늘림 (Transform Scale을 따라가지 않음)
    private static string ConvertLights(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.transform.Find(ConvertedMarker) != null) return "조명: 이미 변환됨 (건너뜀)";

            Light[] lights = root.GetComponentsInChildren<Light>(true);
            var shadowed = new HashSet<Light>(lights
                .Where(l => l.type == LightType.Point || l.type == LightType.Spot)
                .OrderByDescending(l => l.intensity)
                .Take(MaxShadowLights));

            int area = 0;
            foreach (Light light in lights)
            {
                if (light.type == LightType.Rectangle || light.type == LightType.Disc)
                {
                    light.type = LightType.Point;   // URP 면광원은 베이크 전용이라 실시간으로 안 보임
                    area++;
                }

                light.intensity = Mathf.Clamp(0.2f * Mathf.Sqrt(Mathf.Max(0f, light.intensity)), 0.05f, 10f);
                light.range *= LobbyScale;
                light.lightmapBakeType = LightmapBakeType.Realtime;   // 라이트맵을 굽지 않으므로 Baked 조명도 실시간으로
                light.shadows = shadowed.Contains(light) ? LightShadows.Soft : LightShadows.None;
            }

            new GameObject(ConvertedMarker).transform.SetParent(root.transform, false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return $"조명: {lights.Length}개 변환 (면광원 → Point {area}개, 그림자 {shadowed.Count}개)";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ───── 2. InGame 테이블 교체 ─────

    [MenuItem(Menu + "2. 테이블 교체 (InGame 씬을 열고 실행)")]
    private static void InstallTable()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefab);
        GameObject seatsGo = GameObject.Find("Seats");
        Transform seats = seatsGo != null ? seatsGo.transform : null;
        Transform chair = seats != null ? seats.Find("Chair") : null;
        Transform gameTable = seats != null ? seats.Find("GameTable_Prefab") : null;
        Transform oldMesh = gameTable != null ? gameTable.Find("GameTable/SM_GameTable") : null;

        if (prefab == null || seats == null || chair == null || gameTable == null || oldMesh == null)
        {
            Debug.LogError("[원작 환경] InGame 씬의 Seats / Seats/Chair / Seats/GameTable_Prefab/GameTable/SM_GameTable 또는 RoundTable_OG 프리팹이 없습니다.");
            return;
        }
        if (seats.Find("RoundTable_OG") != null)
        {
            Debug.LogWarning("[원작 환경] 이미 RoundTable_OG가 있습니다. 다시 하려면 지우고 SM_GameTable Renderer를 켠 뒤 실행하세요.");
            return;
        }

        var table = (GameObject)PrefabUtility.InstantiatePrefab(prefab, seats.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(table, "Install RoundTable_OG");
        table.transform.SetParent(seats, false);
        table.transform.localRotation = Quaternion.identity;
        table.transform.localScale = Vector3.one * (TableScale / seats.lossyScale.x);
        table.transform.position = seats.position;

        // 높이: 원작 의자 ↔ 테이블 높이 차를 같은 배율로 → 우리 의자 높이에 맞춤
        Transform chairRef = table.transform.Find("ChairRef");
        table.transform.position += Vector3.up * (chair.position.y - chairRef.position.y);

        // 테이블 위 기준점(TableCenter · SeatDeal · CardPool)이 새 상판 높이를 따라오도록 GameTable_Prefab을 올리거나 내림
        Vector3 center = gameTable.Find("GameTable/TableCenter") is Transform tc ? tc.position : gameTable.position;
        float oldTop = SurfaceY(oldMesh.gameObject, center);
        float newTop = SurfaceY(table, center);

        var log = new StringBuilder("[원작 환경] 테이블 교체\n");
        if (!float.IsNaN(oldTop) && !float.IsNaN(newTop))
        {
            Undo.RecordObject(gameTable, "Install RoundTable_OG");
            gameTable.position += Vector3.up * (newTop - oldTop);
            log.AppendLine($"  상판 높이 {oldTop:0.000} → {newTop:0.000} (GameTable_Prefab {newTop - oldTop:+0.000;-0.000} 이동)");
        }
        else
        {
            log.AppendLine("  ⚠ 상판 높이를 못 쟀습니다. GameTable_Prefab 높이를 직접 맞춰 주세요.");
        }

        // 반지름은 Renderer를 끄기 전에 잰다
        float oldRadius = Radius(oldMesh.gameObject, seats.position);
        float newRadius = Radius(table, seats.position);
        Transform deal = gameTable.Find("GameTable/SeatDeal_0");
        float dealRadius = deal != null ? Vector3.Distance(Flat(deal.position), Flat(seats.position)) : 0f;
        log.AppendLine($"  상판 반지름 {oldRadius:0.00} → {newRadius:0.00} m (카드 놓는 SeatDeal은 중심에서 {dealRadius:0.00} m)");

        Renderer oldRenderer = oldMesh.GetComponent<Renderer>();
        Undo.RecordObject(oldRenderer, "Install RoundTable_OG");
        oldRenderer.enabled = false;
        log.AppendLine("  기존 SM_GameTable은 Renderer만 꺼 두었습니다 (되돌리기 쉽게).");

        EditorSceneManager.MarkSceneDirty(seats.gameObject.scene);
        Debug.Log(log.ToString());
    }

    // 원작 메쉬 피벗이 상판 중심이 아닐 때: 메쉬 외곽(Bounds) 중심을 Seats 중심(X·Z)에 맞춘다. 높이는 건드리지 않음
    [MenuItem(Menu + "2-1. 테이블 중심 맞추기 (InGame 씬)")]
    private static void CenterTable()
    {
        GameObject seatsGo = GameObject.Find("Seats");
        Transform root = seatsGo != null ? seatsGo.transform.Find("RoundTable_OG") : null;
        Renderer[] renderers = root != null ? root.GetComponentsInChildren<MeshRenderer>(true) : new Renderer[0];
        if (renderers.Length == 0)
        {
            Debug.LogError("[원작 환경] Seats/RoundTable_OG 또는 그 아래 MeshRenderer가 없습니다.");
            return;
        }

        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);

        Vector3 offset = Flat(seatsGo.transform.position - b.center);
        Undo.RecordObject(root, "Center RoundTable_OG");
        root.position += offset;
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        Debug.Log($"[원작 환경] 테이블 중심 맞춤: {offset.magnitude:0.00} m 이동 (X {offset.x:+0.00;-0.00}, Z {offset.z:+0.00;-0.00})\n" +
                  $"  외곽 크기 {b.size.x:0.00} × {b.size.z:0.00} m, 높이 {b.min.y:0.00} ~ {b.max.y:0.00} m");
    }

    // ───── 3. LobbyScene 대기실 환경 교체 ─────

    [MenuItem(Menu + "3. 대기실 환경 교체 (LobbyScene을 열고 실행)")]
    private static void InstallLobby()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefab);
        GameObject spotsGo = GameObject.Find("WaitingSpot");
        if (prefab == null || spotsGo == null)
        {
            Debug.LogError("[원작 환경] LobbyScene의 WaitingSpot 또는 Lobby_OG 프리팹이 없습니다.");
            return;
        }
        if (GameObject.Find("Lobby_OG") != null)
        {
            Debug.LogWarning("[원작 환경] 이미 Lobby_OG가 있습니다. 다시 하려면 지우고 Map_Bar를 켠 뒤 실행하세요.");
            return;
        }

        var scene = spotsGo.scene;
        var lobby = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(lobby, "Install Lobby_OG");
        lobby.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        lobby.transform.localScale = Vector3.one * LobbyScale;

        Transform markers = lobby.transform.Find("Markers");
        Transform cam = markers != null ? markers.Find("LobbyCamera") : null;
        List<Transform> slots = Enumerable.Range(1, 4).Select(i => markers != null ? markers.Find("Slot" + i) : null).ToList();
        if (cam == null || slots.Any(s => s == null))
        {
            Debug.LogError("[원작 환경] Lobby_OG/Markers에 LobbyCamera · Slot1~4가 없습니다. 추출을 다시 하세요.");
            return;
        }

        // 원작 카메라에서 봤을 때 왼쪽 → 오른쪽 순서로 Spot1~4에 배정
        Vector3 right = Flat(cam.right).normalized;
        slots = slots.OrderBy(s => Vector3.Dot(s.position - cam.position, right)).ToList();

        // 캐릭터가 카메라 쪽을 보도록: LobbyCameraFraming은 스팟의 -Z 쪽에 카메라를 둔다
        Vector3 slotCenter = slots.Aggregate(Vector3.zero, (sum, s) => sum + s.position) / slots.Count;
        Vector3 toCamera = Flat(cam.position - slotCenter).normalized;
        Quaternion spotRotation = Quaternion.LookRotation(-toCamera, Vector3.up);

        var log = new StringBuilder("[원작 환경] 대기실 교체\n");
        for (int i = 0; i < 4; i++)
        {
            Transform spot = spotsGo.transform.Find("Spot" + (i + 1));
            if (spot == null) { log.AppendLine($"  ⚠ Spot{i + 1} 없음"); continue; }

            float floor = FloorY(lobby, slots[i].position);
            bool found = !float.IsNaN(floor);
            if (!found) floor = slots[i].position.y;

            Undo.RecordObject(spot, "Install Lobby_OG");
            spot.SetPositionAndRotation(new Vector3(slots[i].position.x, floor + SpotHeight, slots[i].position.z), spotRotation);
            log.AppendLine($"  Spot{i + 1} ← {slots[i].name} (바닥 {floor:0.000}{(found ? "" : " · 못 찾아 원작 Slot 높이 사용")})");
        }

        GameObject mapBar = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Map_Bar");
        if (mapBar != null)
        {
            Undo.RecordObject(mapBar, "Install Lobby_OG");
            mapBar.SetActive(false);
            log.AppendLine("  기존 Map_Bar는 꺼 두었습니다 (되돌리기 쉽게).");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(log.ToString());
    }

    // ───── 4. 대기실 좌석 · 카메라 · 분위기 ─────
    // 원작 로비: 카메라는 눈높이 · 수평 · 세로 FOV 60으로 고정, Slot1(방장)이 정면, 4명이 부채꼴로 카메라를 바라봄.
    // 조명은 원작이 HDRP 라이트맵(굽기)이라 그대로 못 가져오므로 어두운 주변광 + 안개 + 색보정으로 분위기를 맞춘다.

    [MenuItem(Menu + "4. 대기실 좌석 · 카메라 · 분위기 (LobbyScene을 열고 실행)")]
    private static void SetupLobbyMood()
    {
        GameObject lobby = GameObject.Find("Lobby_OG");
        GameObject spotsGo = GameObject.Find("WaitingSpot");
        Transform markers = lobby != null ? lobby.transform.Find("Markers") : null;
        Transform cam = markers != null ? markers.Find("LobbyCamera") : null;
        if (spotsGo == null || cam == null)
        {
            Debug.LogError("[원작 환경] LobbyScene의 WaitingSpot 또는 Lobby_OG/Markers/LobbyCamera가 없습니다. 3번을 먼저 실행하세요.");
            return;
        }

        Scene scene = lobby.scene;
        var log = new StringBuilder("[원작 환경] 대기실 좌석 · 카메라 · 분위기\n");

        // 좌석: 좌석 번호(SeatAnchors 배열 순서) i → 원작 Slot(i+1). 좌석 0(방장) = Slot1 = 왼쪽에서 두 번째, 원작과 같은 배치
        // 모두 같은 방향(카메라 쪽 정면)을 본다 (스팟 -Z = 캐릭터 정면)
        if (!spotsGo.TryGetComponent(out LiarsBar.Server.SeatAnchors anchors) || anchors.SeatCount < 4)
        {
            Debug.LogError("[원작 환경] WaitingSpot의 SeatAnchors에 좌석 4개가 연결돼 있어야 합니다.");
            return;
        }
        Quaternion spotRotation = Quaternion.LookRotation(Flat(cam.forward).normalized, Vector3.up);
        Vector3 spotCenter = Vector3.zero;
        for (int i = 0; i < 4; i++)
        {
            Transform slot = markers.Find("Slot" + (i + 1));
            Transform spot = anchors.GetSeat(i);
            if (slot == null || spot == null) { log.AppendLine($"  ⚠ Slot{i + 1} 또는 좌석 {i} 없음"); continue; }

            float floor = FloorY(lobby, slot.position);
            if (float.IsNaN(floor)) floor = slot.position.y;

            var position = new Vector3(slot.position.x, floor + SpotHeight, slot.position.z);
            Undo.RecordObject(spot, "Lobby Mood");
            spot.SetPositionAndRotation(position, spotRotation);
            spotCenter += position / 4f;
            log.AppendLine($"  좌석 {i} ({spot.name}) ← Slot{i + 1} (바닥 {floor:0.000}, 카메라까지 {Flat(cam.position - position).magnitude:0.00} m)");
        }

        // 카메라 자리: 원작 카메라 위치 · 방향 (Lobby_OG 배율이 이미 반영된 월드 값)
        GameObject pose = FindOrCreateRoot(scene, "LobbyCameraPose");
        Undo.RecordObject(pose.transform, "Lobby Mood");
        pose.transform.SetPositionAndRotation(cam.position, cam.rotation);
        if (!pose.TryGetComponent(out LobbyCameraAnchor _)) Undo.AddComponent<LobbyCameraAnchor>(pose);
        log.AppendLine($"  카메라 자리 {cam.position} (바닥 위 {cam.position.y - anchors.GetSeat(0).position.y + SpotHeight:0.00} m), 세로 FOV 60");

        // 분위기: 주변광 · 안개 덮어쓰기 + 전역 볼륨(색보정)
        GameObject mood = FindOrCreateRoot(scene, "LobbyLighting");
        if (!mood.TryGetComponent(out SceneLightingOverride lighting)) lighting = Undo.AddComponent<SceneLightingOverride>(mood);
        SetLighting(lighting);
        if (!mood.TryGetComponent(out Volume volume)) volume = Undo.AddComponent<Volume>(mood);
        Undo.RecordObject(volume, "Lobby Mood");
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = LoadOrCreateLobbyProfile();
        SetExposure(volume.sharedProfile, LobbyExposure);
        log.AppendLine($"  LobbyLighting: 주변광 {LobbyAmbient} · 안개 {LobbyFogDensity} · 노출 +{LobbyExposure} ({LobbyVolumeProfile})");

        // 원작은 라이트맵으로 캐릭터 앞이 밝았다 → 카메라 위쪽에서 좌석 줄을 비추는 따뜻한 스포트라이트로 대신
        GameObject keyGo = FindOrCreateRoot(scene, "LobbyKeyLight");
        if (!keyGo.TryGetComponent(out Light key)) key = Undo.AddComponent<Light>(keyGo);
        Undo.RecordObject(keyGo.transform, "Lobby Mood");
        Undo.RecordObject(key, "Lobby Mood");
        Vector3 keyPos = cam.position + Vector3.up * 1.2f;
        keyGo.transform.SetPositionAndRotation(keyPos, Quaternion.LookRotation(spotCenter - keyPos, Vector3.up));
        key.type = LightType.Spot;
        key.spotAngle = KeyLightAngle;
        key.innerSpotAngle = KeyLightAngle * 0.6f;   // 가장자리 좌석도 밝게 (안쪽 각도를 넓혀 감쇠를 늦춤)
        key.range = 12f;
        key.intensity = KeyLightIntensity;
        key.color = KeyLightColor;
        key.shadows = LightShadows.Soft;
        key.lightmapBakeType = LightmapBakeType.Realtime;
        log.AppendLine($"  LobbyKeyLight: 카메라 위 1.2 m에서 좌석 줄 중심을 비춤 (세기 {KeyLightIntensity})");

        // 원작 로비 조명 밝기 보충: 프리팹 원본 값 × 배율 (다시 실행해도 누적되지 않음)
        int boosted = 0;
        foreach (Light light in lobby.GetComponentsInChildren<Light>(true))
        {
            Light source = PrefabUtility.GetCorrespondingObjectFromSource(light);
            if (source == null) continue;
            Undo.RecordObject(light, "Lobby Mood");
            light.intensity = source.intensity * LobbyLightBoost;
            boosted++;
        }
        log.AppendLine($"  원작 로비 조명 {boosted}개 × {LobbyLightBoost}");

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(log.ToString());
    }

    // 다시 실행해도 같은 값이 들어가도록 매번 덮어씀 (조정은 Play 중 Inspector에서 해 보고 이 상수에 반영)
    private static void SetLighting(SceneLightingOverride lighting)
    {
        var so = new SerializedObject(lighting);
        so.FindProperty("ambientColor").colorValue = LobbyAmbient;
        so.FindProperty("fog").boolValue = true;
        so.FindProperty("fogColor").colorValue = LobbyAmbient * 0.5f;
        so.FindProperty("fogDensity").floatValue = LobbyFogDensity;
        so.FindProperty("reflectionIntensity").floatValue = 0.5f;
        so.ApplyModifiedProperties();
    }

    private static void SetExposure(VolumeProfile profile, float exposure)
    {
        if (profile == null || !profile.TryGet(out ColorAdjustments color)) return;
        Undo.RecordObject(color, "Lobby Mood");
        color.postExposure.Override(exposure);
        color.colorFilter.Override(Color.white);   // 색 필터를 겹치면 전체가 주황 · 빨강으로 물들어 원작과 달라짐
        color.saturation.Override(0f);
        color.contrast.Override(10f);
        EditorUtility.SetDirty(color);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
    }

    private static GameObject FindOrCreateRoot(Scene scene, string name)
    {
        GameObject go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
        if (go != null) return go;

        go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        Undo.RegisterCreatedObjectUndo(go, "Lobby Mood");
        return go;
    }

    // 원작 로비 느낌: 어둡고 따뜻한 색, 대비 ↑, 가장자리 어둡게, 조명 주변 은은한 번짐
    private static VolumeProfile LoadOrCreateLobbyProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LobbyVolumeProfile);
        if (profile != null) return profile;

        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, LobbyVolumeProfile);

        Tonemapping tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.3f);
        color.contrast.Override(20f);
        color.colorFilter.Override(Color.white);
        color.saturation.Override(10f);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.35f);
        vignette.smoothness.Override(0.4f);

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1f);
        bloom.intensity.Override(0.5f);

        foreach (VolumeComponent component in profile.components)
        {
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    // ───── 측정 도구 ─────

    // 한 점 위에서 아래로 쏴 해당 오브젝트 메쉬의 가장 높은 면 높이 (임시 MeshCollider 사용 후 제거)
    private static float SurfaceY(GameObject root, Vector3 point)
    {
        return CastDown(root.GetComponentsInChildren<MeshFilter>(true), point + Vector3.up * 3f, 6f);
    }

    // 원작 Slot 바로 아래 바닥. Slot 높이 + 0.5m 위에서 아래 3m까지만 본다 (천장 · 2층 제외)
    private static float FloorY(GameObject root, Vector3 slot)
    {
        var candidates = root.GetComponentsInChildren<MeshFilter>(true).Where(mf =>
        {
            Renderer r = mf.GetComponent<Renderer>();
            if (r == null) return false;
            Bounds b = r.bounds;
            return slot.x >= b.min.x && slot.x <= b.max.x && slot.z >= b.min.z && slot.z <= b.max.z;
        });
        return CastDown(candidates, slot + Vector3.up * 0.5f, 3f);
    }

    private static float CastDown(IEnumerable<MeshFilter> filters, Vector3 origin, float distance)
    {
        var temp = new List<MeshCollider>();
        float best = float.NaN;
        try
        {
            foreach (MeshFilter mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                temp.Add(mc);
            }
            Physics.SyncTransforms();

            var ray = new Ray(origin, Vector3.down);
            foreach (MeshCollider mc in temp)
                if (mc.Raycast(ray, out RaycastHit hit, distance) && (float.IsNaN(best) || hit.point.y > best))
                    best = hit.point.y;
        }
        finally
        {
            foreach (MeshCollider mc in temp) Object.DestroyImmediate(mc);
        }
        return best;
    }

    private static float Radius(GameObject root, Vector3 center)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return 0f;
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return Mathf.Max(b.max.x - center.x, center.x - b.min.x, b.max.z - center.z, center.z - b.min.z);
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
