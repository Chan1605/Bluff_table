using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LiarsBar.Server
{
    // 클라 전용: 내 방 상태에 맞춰 대기실 · 테이블 씬을 Additive로 로드/언로드 (NetworkManager 오브젝트에 추가)
    public class RoomScenes : MonoBehaviour
    {
        [Tooltip("방에서 대기 중일 때 불러올 씬 (Build Settings 등록). 비우면 안 불러옴")]
        [SerializeField] private string waitingRoomScene;

        [Tooltip("게임 중 · 결과 때 불러올 테이블 씬 (Build Settings 등록). NetworkManager · 네트워크 오브젝트가 없어야 함")]
        [SerializeField] private string tableScene;

        private string loadedScene = "";
        private Coroutine routine;

        public static bool IsLoading { get; private set; }

        // 씬 전환이 끝났을 때 (지금 불러와 둔 씬 이름, 없으면 "")
        public static event Action<string> SceneChanged;

        private void OnEnable()
        {
            PlayerProfile.Changed += OnProfileChanged;
            PlayerProfile.Removed += OnProfileChanged;
        }

        private void OnDisable()
        {
            PlayerProfile.Changed -= OnProfileChanged;
            PlayerProfile.Removed -= OnProfileChanged;
            StopAllCoroutines();
            routine = null;
            IsLoading = false;
        }

        private void OnProfileChanged(PlayerProfile player)
        {
            if (player != PlayerProfile.Local || routine != null) return;
            routine = StartCoroutine(SyncScenes());
        }

        private string GetTargetScene()
        {
            PlayerProfile me = PlayerProfile.Local;
            if (me == null || !me.IsInRoom) return "";

            string scene = SeatAnchors.AreaFor(me.Phase) == SeatArea.Table ? tableScene : waitingRoomScene;
            return scene ?? "";
        }

        private IEnumerator SyncScenes()
        {
            IsLoading = true;

            // 같은 프레임에 들어온 SyncVar가 모두 반영된 뒤 판단
            yield return null;

            while (true)
            {
                string target = GetTargetScene();
                if (target == loadedScene) break;

                if (loadedScene.Length > 0)
                {
                    Scene old = SceneManager.GetSceneByName(loadedScene);
                    loadedScene = "";
                    if (old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
                    continue;
                }

                loadedScene = target;
                if (!Application.CanStreamedLevelBeLoaded(target))
                {
                    Debug.LogError($"[Scene] '{target}' 씬을 불러올 수 없습니다. Build Settings에 추가하세요.");
                    continue;
                }

                Debug.Log($"[Scene] '{target}' 로드");
                yield return SceneManager.LoadSceneAsync(target, LoadSceneMode.Additive);
            }

            routine = null;
            IsLoading = false;
            SceneChanged?.Invoke(loadedScene);
        }
    }
}
