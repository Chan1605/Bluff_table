using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;
using LiarsBar.Server;

public class PlayerController : NetworkBehaviour
{
    private float nextSyncTime = 0f;
    private const float SYNC_INTERVAL = 0.1f;
    public Vector2 LookInput { get; private set; }
    public HeadController headController;
    public PlayerInput inputActions { get; private set; }
    public PlayerState pState;

    // 손패 고르기 (로컬 플레이어만). 인덱스는 서버 손패 기준(남은 카드 0 ~ n-1)
    private const int MaxSelect = 3;                 // 한 번에 낼 수 있는 최대 장수 (서버 PlayerSubmitCard와 같게)
    private readonly List<int> selected = new List<int>();
    private int handCount;
    private int cursor = -1;
    private bool myTurn;
    private Vector2 lastSelectInput;

    private void OnEnable()
    {
        InGameEvent.OnEntryInGame += TransitionToInGame;
        InGameEvent.OnReEntryInLobby += TransitionToLobby;

        InGameEvent.OnRoundStarted += HandleRoundStarted;
        InGameEvent.OnCardDealt += HandleCardDealt;
        InGameEvent.OnTurnChanged += HandleTurnChanged;
        InGameEvent.OnCardsSubmitted += HandleCardsSubmitted;
        InGameEvent.OnLiarCalled += HandleLiarCalled;
    }

    private void OnDisable()
    {
        InGameEvent.OnEntryInGame -= TransitionToInGame;
        InGameEvent.OnReEntryInLobby -= TransitionToLobby;

        InGameEvent.OnRoundStarted -= HandleRoundStarted;
        InGameEvent.OnCardDealt -= HandleCardDealt;
        InGameEvent.OnTurnChanged -= HandleTurnChanged;
        InGameEvent.OnCardsSubmitted -= HandleCardsSubmitted;
        InGameEvent.OnLiarCalled -= HandleLiarCalled;
    }

    public override void OnStartLocalPlayer()
    {
        inputActions = new PlayerInput();
        pState = GetComponent<PlayerState>();

        inputActions.Player.Select.performed += OnSelectPerformed;
        inputActions.Player.Select.canceled += OnSelectCanceled;
        inputActions.Player.Emote.performed += OnEmotePerformed;
        inputActions.Player.Confirm.performed += OnConfirmPerformed;
        inputActions.Player.Liar.performed += OnLiarPerformed;
        inputActions.Player.Look.performed += OnLookPerformed;
        inputActions.Player.Voice.performed += OnVoicePerformed;
        inputActions.Player.Escape.performed += OnEscapePerformed;

        inputActions.Player.Disable();
        inputActions.UI.Enable();

        Debug.Log("[클라이언트] 로컬 인풋 시스템 연결 완료");
    }

    public override void OnStopClient()
    {
        if (inputActions != null)
        {
            inputActions.Player.Select.performed -= OnSelectPerformed;
            inputActions.Player.Select.canceled -= OnSelectCanceled;
            inputActions.Player.Emote.performed -= OnEmotePerformed;
            inputActions.Player.Confirm.performed -= OnConfirmPerformed;
            inputActions.Player.Liar.performed -= OnLiarPerformed;
            inputActions.Player.Look.performed -= OnLookPerformed;
            inputActions.Player.Voice.performed -= OnVoicePerformed;
            inputActions.Player.Escape.performed -= OnEscapePerformed;

            inputActions.Disable();
        }
    }

    private void Update()
    {
        if (!isLocalPlayer ||
            inputActions == null ||
            !inputActions.Player.enabled ||
            headController == null||
            (pState != null && pState.isDead))
            return;


        Vector2 delta = inputActions.Player.MouseDelta.ReadValue<Vector2>();

        if (delta.sqrMagnitude > 0.01f)
        {
            headController.AddLookInput(delta);

            if (Time.time > nextSyncTime)
            {
                Vector2 currentAngles = headController.GetcurrentAngles();
                CmdSyncHeadRotation(currentAngles.x, currentAngles.y);
                nextSyncTime = Time.time + SYNC_INTERVAL;
            }
        }
    }

    // TODO 최적화 체크
    [Command]
    private void CmdSyncHeadRotation(float pitch, float yaw)
    {
        // pState는 OnStartLocalPlayer에서만 채워져서 서버에 있는 남의 플레이어 쪽은 비어 있다 → 여기서 찾는다
        if (pState == null) pState = GetComponent<PlayerState>();
        if (pState == null) return;

        pState.syncHeadPitch = pitch;
        pState.syncHeadYaw = yaw;
    }

    private void TransitionToInGame()
    {
        if (!isLocalPlayer || inputActions == null) return;

        inputActions.UI.Disable();
        inputActions.Player.Enable();

        Debug.Log("[PlayerInput] : 인게임 인풋 전환");

    }

    private void TransitionToLobby()
    {
        if (!isLocalPlayer || inputActions == null) return;

        inputActions.Player.Disable();
        inputActions.UI.Enable();
        ClearHand();
    }

    // A / D: 가리키는 카드 이동, W: 고르기 ↔ 해제, S: 해제 (스페이스도 내 차례엔 고르기)
    // 2D 벡터라 키를 겹쳐 누르면 값이 바뀔 때마다 들어온다 → 새로 눌린 방향만 처리
    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        if (PlayerState.Local == null) return;

        Vector2 direction = context.ReadValue<Vector2>();
        Vector2 last = lastSelectInput;
        lastSelectInput = direction;

        if (direction.x < 0 && last.x >= 0) MoveCursor(-1);       // 좌
        else if (direction.x > 0 && last.x <= 0) MoveCursor(1);   // 우

        if (direction.y > 0 && last.y <= 0) ToggleSelected();     // 상
        else if (direction.y < 0 && last.y >= 0) Deselect();      // 하
    }

    private void OnSelectCanceled(InputAction.CallbackContext context)
    {
        lastSelectInput = Vector2.zero;
    }

    private void OnEmotePerformed(InputAction.CallbackContext context)
    {
        if (PlayerState.Local == null || PlayerState.Local.isTurn) return;

        string keyName = context.control.name;

        if(int.TryParse(keyName, out int emoteIndex))
        {
            // TODO 이모트 인덱스와 이모트 재생 연결
        }
    }

    // E: 고른 카드 내기. 선택은 서버가 확정(OnCardsSubmitted)하면 비운다 (거절되면 그대로 남음)
    private void OnConfirmPerformed(InputAction.CallbackContext context)
    {
        if (PlayerState.Local == null || !myTurn) return;
        if (selected.Count == 0)
        {
            Debug.Log("[PlayerInput] : 고른 카드가 없습니다.");
            return;
        }

        InGameEvent.TriggerOnCardsPlayed(selected.ToArray());   // PlayerState.SubmitCards → 서버
    }

    // X: LIAR (앞사람이 낸 게 없거나 내 차례가 아니면 서버가 거절)
    private void OnLiarPerformed(InputAction.CallbackContext context)
    {
        if (PlayerState.Local == null || !myTurn) return;
        PlayerState.Local.CallLiar();
    }

    // 스페이스: 내 차례면 가리키는 카드 고르기, 아니면 패 펼치기 / 덮기
    private void OnLookPerformed(InputAction.CallbackContext context)
    {
        if (PlayerState.Local == null) return;

        if (myTurn && cursor >= 0)
        {
            ToggleSelected();
            return;
        }
        InGameEvent.TriggerOnLookRequested();   // 패 펼치기 (HandCardsSetup이 가능 여부 판단)
    }

    private void OnVoicePerformed(InputAction.CallbackContext context)
    {
        if (PlayerState.Local == null) return;
    }

    private void OnEscapePerformed(InputAction.CallbackContext context)
    {
        if (PlayerState.Local == null) return;

        inputActions.Player.Disable();
        inputActions.UI.Enable();

        InGameEvent.TriggerOnMenuOpened();
    }

    #region 손패 고르기

    private static int MySeat => PlayerProfile.Local != null ? PlayerProfile.Local.Seat : -1;

    private void HandleRoundStarted(CardType card)
    {
        if (!isLocalPlayer) return;
        handCount = 0;
        myTurn = false;
        ClearHand();
    }

    // 라운드 시작 손패 / 제출 후 남은 손패
    private void HandleCardDealt(List<CardType> hand)
    {
        if (!isLocalPlayer) return;

        handCount = hand != null ? hand.Count : 0;
        selected.Clear();
        PublishSelection();

        cursor = myTurn && handCount > 0 ? Mathf.Clamp(cursor, 0, handCount - 1) : -1;
        PublishCursor();
    }

    private void HandleTurnChanged(int seat)
    {
        if (!isLocalPlayer) return;

        myTurn = seat == MySeat;
        if (!myTurn)
        {
            ClearHand();
            return;
        }

        cursor = handCount > 0 ? 0 : -1;
        PublishCursor();
    }

    private void HandleCardsSubmitted(int seat, int count, int[] indices)
    {
        if (!isLocalPlayer || seat != MySeat) return;
        selected.Clear();
        PublishSelection();
    }

    private void HandleLiarCalled(int callerSeat, int loserSeat)
    {
        if (!isLocalPlayer) return;
        myTurn = false;
        ClearHand();
    }

    private void MoveCursor(int step)
    {
        if (!myTurn || handCount <= 0) return;

        cursor = cursor < 0 ? 0 : (cursor + step + handCount) % handCount;
        PublishCursor();
    }

    private void ToggleSelected()
    {
        if (!myTurn || cursor < 0 || cursor >= handCount) return;

        if (selected.Contains(cursor)) selected.Remove(cursor);
        else if (selected.Count < MaxSelect) selected.Add(cursor);
        else return;

        PublishSelection();
    }

    private void Deselect()
    {
        if (!myTurn || !selected.Remove(cursor)) return;
        PublishSelection();
    }

    private void ClearHand()
    {
        selected.Clear();
        cursor = -1;
        lastSelectInput = Vector2.zero;
        PublishSelection();
        PublishCursor();
    }

    private void PublishSelection() => InGameEvent.TriggerOnHandSelectionChanged(selected);
    private void PublishCursor() => InGameEvent.TriggerOnHandCursorChanged(cursor);

    #endregion
}
