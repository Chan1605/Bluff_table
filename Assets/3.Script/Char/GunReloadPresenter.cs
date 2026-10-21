using UnityEngine;
using LiarsBar.Server;


public class GunReloadPresenter : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[GunReloadPresenter]");
        DontDestroyOnLoad(go);
        go.AddComponent<GunReloadPresenter>();
    }

    private static readonly int ReloadId = Animator.StringToHash("Reload");

    private void OnEnable() => InGameEvent.OnGunReload += HandleReload;
    private void OnDisable() => InGameEvent.OnGunReload -= HandleReload;

    private void HandleReload()
    {
        PlayerProfile me = PlayerProfile.Local;
        foreach (PlayerProfile p in PlayerProfile.All)
        {
            if (p == null || !p.IsInRoom || p.Seat < 0 || (me != null && p.RoomId != me.RoomId)) continue;
            if (!p.TryGetComponent(out PlayerState state) || state.isDead || state.headController == null) continue;

            Animator body = state.headController.GetComponentInParent<Animator>();
            if (body != null && HasParameter(body, ReloadId)) body.SetTrigger(ReloadId);
        }
    }

    private static bool HasParameter(Animator animator, int id)
    {
        if (animator.runtimeAnimatorController == null) return false;
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.nameHash == id) return true;
        return false;
    }
}
