using LiarsBar.Server;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


[RequireComponent(typeof(PlayerProfile), typeof(LobbyCharacterView))]
public class LobbyCharacterLabelUI : MonoBehaviour
{

    [SerializeField] private Canvas changeLabel;           // ChangeLabelCanvas 
    [SerializeField] private TMP_Text characterNameText;   // 캐릭터 이름
    [SerializeField] private Animator lineAnimator;        // 지시선 프레임 애니메이션 (선택)
    [SerializeField] private string lineState = "SelAnimation";
    [SerializeField] private Vector3 labelAnchor = new Vector3(0.3f, 0.68f, 0f);


    [SerializeField] private Canvas nameplate;             // NameplateCanvas 
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private GameObject readyMark;         // 체크 아이콘
    [SerializeField] private Vector3 nameplateAnchor = new Vector3(0f, -1f, 0f);
    [Tooltip("이름표가 내려갈 수 있는 최저 높이 (1080 기준 픽셀). 하단 채팅/로비 정보 바와 겹치지 않게")]
    [SerializeField] private float nameplateMinY = 110f;

    private PlayerProfile profile;
    private LobbyCharacterView view;
    private ScreenFollower labelFollower;
    private ScreenFollower nameplateFollower;
    private bool labelVisible;
    private int shownCharacter = -1;

    // Overlay Canvas 자신은 화면 전체로 고정되므로, 자식들을 기준점 위치로 옮긴다
    private class ScreenFollower
    {
        private readonly Canvas canvas;
        private readonly RectTransform[] children;
        private readonly Vector2[] basePositions;   // 기준점에서의 픽셀 오프셋 (1920x1080 기준)

        public ScreenFollower(Canvas canvas)
        {
            this.canvas = canvas;
            Transform root = canvas.transform;
            children = new RectTransform[root.childCount];
            basePositions = new Vector2[root.childCount];

            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform rt = (RectTransform)root.GetChild(i);
                rt.anchorMin = rt.anchorMax = Vector2.zero;   // 화면 왼쪽 아래 기준
                children[i] = rt;
                basePositions[i] = rt.anchoredPosition;
            }
        }

        public void Follow(Camera cam, Vector3 worldPoint, float minY = float.NegativeInfinity)
        {
            Vector3 screen = cam.WorldToScreenPoint(worldPoint);
            bool inFront = screen.z > 0f;
            canvas.enabled = inFront;
            if (!inFront) return;

            float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Vector2 origin = new Vector2(screen.x, screen.y) / scale;

            // 가장 아래 자식이 minY 밑으로 내려가면 전체를 올린다 (화면 아래 UI와 겹치지 않게)
            float lowest = float.PositiveInfinity;
            for (int i = 0; i < basePositions.Length; i++) lowest = Mathf.Min(lowest, basePositions[i].y);
            if (basePositions.Length > 0 && origin.y + lowest < minY) origin.y = minY - lowest;

            for (int i = 0; i < children.Length; i++)
                children[i].anchoredPosition = origin + basePositions[i];
        }
    }

    private void Awake()
    {
        profile = GetComponent<PlayerProfile>();
        view = GetComponent<LobbyCharacterView>();

        if (changeLabel != null)
        {
            labelFollower = new ScreenFollower(changeLabel);
            changeLabel.gameObject.SetActive(false);   // 프리팹에 켜진 채 저장돼 있어도 호버 전에는 숨김
        }
        if (nameplate != null) nameplateFollower = new ScreenFollower(nameplate);

        // 누를 일이 없는 UI가 캐릭터 호버/클릭을 가리지 않게
        DisableRaycast(changeLabel);
        DisableRaycast(nameplate);
    }

    private void OnEnable()
    {
        PlayerProfile.Added += OnProfileEvent;
        PlayerProfile.Changed += OnProfileEvent;
        view.HighlightChanged += OnHighlightChanged;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerProfile.Added -= OnProfileEvent;
        PlayerProfile.Changed -= OnProfileEvent;
        view.HighlightChanged -= OnHighlightChanged;

        SetLabelVisible(false);
    }

    private void OnProfileEvent(PlayerProfile changed)
    {
        if (changed == profile) Refresh();
    }

    private void OnHighlightChanged(bool _) { Refresh(); }

    private void Refresh()
    {
        bool lobby = profile.IsLoggedIn && profile.IsInRoom && profile.Seat >= 0
                     && profile.Phase == RoomPhase.Lobby;

        if (nameplate != null) nameplate.gameObject.SetActive(lobby);
        if (lobby)
        {
            if (nicknameText != null) nicknameText.text = NicknameWithRecord();
            if (readyMark != null) readyMark.SetActive(profile.IsReady);
        }

        SetLabelVisible(lobby && profile.isLocalPlayer && view.IsHighlighted);
        if (!labelVisible || profile.CharacterId == shownCharacter) return;

        // 라벨이 막 켜졌거나, 호버 중에 클릭해서 캐릭터가 바뀐 경우
        shownCharacter = profile.CharacterId;
        if (characterNameText != null) characterNameText.text = view.GetDisplayName(shownCharacter);
        if (lineAnimator != null) lineAnimator.Play(lineState, 0, 0f);   // 지시선 처음부터 다시 그리기
        LateUpdate();   // 켜진 첫 프레임부터 제자리에 보이게
    }

    // 대기실 이름표: "닉네임 12W 5L" (전적 없는 계정은 닉네임만)
    private string NicknameWithRecord()
    {
        if (!profile.HasStats) return profile.Nickname;
        return $"{profile.Nickname} <size=65%><color=#D9CDB8>{RecordFormat.Short(profile.Wins, profile.GamesPlayed)}</color></size>";
    }

    private void SetLabelVisible(bool visible)
    {
        if (changeLabel == null) visible = false;
        if (visible == labelVisible) return;

        labelVisible = visible;
        if (changeLabel != null) changeLabel.gameObject.SetActive(visible);
        if (!visible) shownCharacter = -1;   // 다음에 켜질 때 지시선을 처음부터 재생
    }

    private void LateUpdate()
    {
        Camera cam = view.LobbyCamera;
        if (cam == null) return;

        if (labelVisible && labelFollower != null)
        {
            Vector3 local = labelAnchor + Vector3.up * view.GetLabelOffset(shownCharacter);
            labelFollower.Follow(cam, transform.TransformPoint(local));
        }

        if (nameplateFollower != null && nameplate.gameObject.activeSelf)
            nameplateFollower.Follow(cam, transform.TransformPoint(nameplateAnchor), nameplateMinY);
    }

    private static void DisableRaycast(Canvas root)
    {
        if (root == null) return;

        foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;
    }
}
