using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 공용 원본 컨트롤러(Bristle_OG)에 러시안 룰렛 상태를 넣는다. 여러 번 실행해도 같은 결과 (기존 룰렛 상태는 지우고 다시 만듦)
//
//   파라미터: Roulette (Trigger), Survived (Bool)
//   Any State ─Roulette→ Roulette(Bristle_silah_trigger)
//                          ├ 끝나면 & Survived  → RouletteEmpty(Bristle_silah_bos_0) → 끝나면 NoCards
//                          └ 끝나면 & !Survived → RouletteDead(Bristle_silah_dolu_uzun)   (쓰러진 채 유지)
//   Any State ─Winner→ Victory(bristle_win_deck)   (마지막 1인 승리)
//   Any State ─Reload→ GunReload(Bristle_reloadyeni) → 끝나면 NoCards   (게임 시작: 탄 넣고 테이블에 내려놓기)
//
// 다른 캐릭터 클립은 OriginalOverrideBuilder가 원작 Blorf 컨트롤러의 Roulet / RouletEmpty / Dead 상태에서 가져온다.
// 메뉴: Tools / 원작 캐릭터 / 룰렛 상태 추가 (Bristle_OG)
//       Tools / 원작 캐릭터 / Sit 프리팹 전체 Override 다시 만들기
public static class RouletteAnimatorSetup
{
    private const string MenuRoot = "Tools/원작 캐릭터/";
    private const string ControllerPath = "Assets/Art/Anim/OG_Override/Bristle_OG.controller";
    private const string ClipFolder = "Assets/2.Model/Original/Bristle/AnimationClip/";
    private const string SitFolder = "Assets/2.Model/Original/Characters/_Export/Sit_Character";
    private const string BuildOverrideMenu = MenuRoot + "선택한 Sit 프리팹 Override 만들기";

    // 런타임 코드(RoulettePresenter)와 같은 이름을 써야 한다
    public const string RouletteParam = "Roulette";
    public const string SurvivedParam = "Survived";
    public const string AimState = "Roulette";
    public const string EmptyState = "RouletteEmpty";
    public const string DeadState = "RouletteDead";
    public const string WinnerParam = "Winner";
    public const string VictoryState = "Victory";
    public const string ReloadParam = "Reload";
    public const string ReloadState = "GunReload";
    private const string IdleState = "NoCards";

    // 다시 만들 때 지울 상태 (이전에 손으로 넣은 Cupcake 상태 포함)
    private static readonly string[] ReplacedStates =
    {
        AimState, EmptyState, DeadState, VictoryState, ReloadState, "Cupcake_beforefire", "Cupcake_silah_bos", "Cupcake_silah_dolu",
    };

    [MenuItem(MenuRoot + "룰렛 상태 추가 (Bristle_OG)")]
    private static void Setup()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) { Debug.LogError($"[룰렛 애니메이터] 컨트롤러 없음: {ControllerPath}"); return; }

        AnimationClip aim = LoadClip("Bristle_silah_trigger");
        AnimationClip empty = LoadClip("Bristle_silah_bos_0");
        AnimationClip dead = LoadClip("Bristle_silah_dolu_uzun");
        AnimationClip win = LoadClip("bristle_win_deck");   // 마지막 1인 승리 (원작 Blorf: Winner 상태)
        AnimationClip reload = LoadClip("Bristle_reloadyeni"); // 게임 시작 장전 (원작 Blorf: ScubyReload 상태)
        if (aim == null || empty == null || dead == null || win == null || reload == null) return;

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState idle = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == IdleState);
        if (idle == null) { Debug.LogError($"[룰렛 애니메이터] '{IdleState}' 상태가 없음"); return; }

        // 1) 이전 룰렛 상태 정리 (상태를 지우면 그 상태로 가는 전이도 같이 지워진다)
        foreach (ChildAnimatorState child in sm.states.ToArray())
            if (ReplacedStates.Contains(child.state.name))
                sm.RemoveState(child.state);

        // 2) 파라미터
        AddParameter(controller, RouletteParam, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, SurvivedParam, AnimatorControllerParameterType.Bool);
        AddParameter(controller, WinnerParam, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, ReloadParam, AnimatorControllerParameterType.Trigger);

        // 3) 상태 (Animator 창에서 NoCards 왼쪽 위에 세로로 배치)
        Vector3 basePos = sm.states.First(s => s.state == idle).position;
        AnimatorState aimState = sm.AddState(AimState, basePos + new Vector3(-260f, -200f, 0f));
        AnimatorState emptyState = sm.AddState(EmptyState, basePos + new Vector3(-420f, -100f, 0f));
        AnimatorState deadState = sm.AddState(DeadState, basePos + new Vector3(-100f, -100f, 0f));
        AnimatorState victoryState = sm.AddState(VictoryState, basePos + new Vector3(260f, -200f, 0f));
        victoryState.motion = win;
        AnimatorState reloadState = sm.AddState(ReloadState, basePos + new Vector3(260f, -100f, 0f));
        reloadState.motion = reload;
        aimState.motion = aim;
        emptyState.motion = empty;
        deadState.motion = dead;

        // 4) 전이
        AnimatorStateTransition toAim = sm.AddAnyStateTransition(aimState);
        toAim.hasExitTime = false;
        toAim.duration = 0.15f;
        toAim.canTransitionToSelf = false;
        toAim.AddCondition(AnimatorConditionMode.If, 0f, RouletteParam);

        AnimatorStateTransition toEmpty = aimState.AddTransition(emptyState);
        SetExit(toEmpty, 0.1f);
        toEmpty.AddCondition(AnimatorConditionMode.If, 0f, SurvivedParam);

        AnimatorStateTransition toDead = aimState.AddTransition(deadState);
        SetExit(toDead, 0.1f);
        toDead.AddCondition(AnimatorConditionMode.IfNot, 0f, SurvivedParam);

        AnimatorStateTransition backToIdle = emptyState.AddTransition(idle);
        SetExit(backToIdle, 0.2f);

        // 승리: 어느 상태에서든 바로 (끝나면 마지막 자세 유지 → 대기실 복귀 때 캐릭터가 지워짐)
        AnimatorStateTransition toVictory = sm.AddAnyStateTransition(victoryState);
        toVictory.hasExitTime = false;
        toVictory.duration = 0.25f;
        toVictory.canTransitionToSelf = false;
        toVictory.AddCondition(AnimatorConditionMode.If, 0f, WinnerParam);

        // 장전: 어느 상태에서든 바로 → 끝나면 NoCards (총은 테이블에 놓인 자세로 끝남)
        AnimatorStateTransition toReload = sm.AddAnyStateTransition(reloadState);
        toReload.hasExitTime = false;
        toReload.duration = 0.25f;
        toReload.canTransitionToSelf = false;
        toReload.AddCondition(AnimatorConditionMode.If, 0f, ReloadParam);

        AnimatorStateTransition reloadToIdle = reloadState.AddTransition(idle);
        SetExit(reloadToIdle, 0.2f);

        // 5) 기본 상태는 NoCards
        sm.defaultState = idle;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[룰렛 애니메이터] {controller.name}: 상태 {AimState} / {EmptyState} / {DeadState} / {VictoryState} / {ReloadState}, 파라미터 {RouletteParam} / {SurvivedParam} / {WinnerParam} / {ReloadParam} 추가, 기본 상태 {IdleState}");
    }

    [MenuItem(MenuRoot + "Sit 프리팹 전체 Override 다시 만들기")]
    private static void RebuildAllOverrides()
    {
        if (!EditorUtility.DisplayDialog("Override 다시 만들기",
                "Sit 프리팹 전체의 *_OG.overrideController를 지우고 다시 만듭니다.\n(손으로 바꾼 오버라이드가 있으면 사라집니다)",
                "다시 만들기", "취소"))
            return;

        Object[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { SitFolder })
            .Select(g => (Object)AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(o => o != null)
            .ToArray();

        Selection.objects = prefabs;
        if (!EditorApplication.ExecuteMenuItem(BuildOverrideMenu))
            Debug.LogError($"[룰렛 애니메이터] 메뉴를 못 찾음: {BuildOverrideMenu}");
    }

    private static void SetExit(AnimatorStateTransition t, float duration)
    {
        t.hasExitTime = true;
        t.exitTime = 1f;          // 클립이 끝까지 재생된 뒤
        t.hasFixedDuration = true;
        t.duration = duration;
    }

    private static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        AnimatorControllerParameter existing = controller.parameters.FirstOrDefault(p => p.name == name);
        if (existing != null)
        {
            if (existing.type == type) return;
            controller.RemoveParameter(existing);   // 이름은 같은데 종류가 다르면 다시 만든다
        }
        controller.AddParameter(name, type);
    }

    private static AnimationClip LoadClip(string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipFolder + name + ".anim");
        if (clip == null) Debug.LogError($"[룰렛 애니메이터] 클립 없음: {ClipFolder}{name}.anim");
        return clip;
    }
}
