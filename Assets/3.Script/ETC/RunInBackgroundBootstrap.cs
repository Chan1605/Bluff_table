using UnityEngine;

// 포커스가 없어도 게임이 계속 돈다 (멀티 테스트 · 알트탭 중 인트로 영상 · 씬 로딩 · 네트워크가 멈추지 않게)
// Mirror NetworkManager도 같은 설정을 켜지만 타이틀 씬부터 존재하므로, 첫 씬(인트로) 로드 전에 미리 켠다
public static class RunInBackgroundBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply() => Application.runInBackground = true;
}
