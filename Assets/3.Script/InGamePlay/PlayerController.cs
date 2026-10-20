using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    private float nextSyncTime = 0f;
    private const float SYNC_INTERVAL = 0.1f;
    public Vector2 LookInput { get; private set; }
    public HeadController headController;
    public PlayerInput inputActions { get; private set; }
    public PlayerState pState;

    private void OnEnable()
    {
        InGameEvent.OnEntryInGame += TransitionToInGame;
        InGameEvent.OnReEntryInLobby += TransitionToLobby;
    }

    private void OnDisable()
    {
        InGameEvent.OnEntryInGame -= TransitionToInGame;
        InGameEvent.OnReEntryInLobby -= TransitionToLobby;
    }

    public override void OnStartLocalPlayer()
    {
        inputActions = new PlayerInput();
        pState = GetComponent<PlayerState>();

        inputActions.Player.Select.performed += OnSelectPerformed;
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
            headController == null)
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
    }

    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        if (PlayerState.Local == null) return;

        Vector2 direction = context.ReadValue<Vector2>();

        // TODO 인게임 UI와 연동

        if (direction.x < 0) // 좌
        {

        }
        else if (direction.x > 0) // 우
        {

        }

        if (direction.y > 0) // 상
        {

        }
        else if (direction.y < 0) // 하
        {

        }
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

    private void OnConfirmPerformed(InputAction.CallbackContext context)
    {
        if (PlayerState.Local == null) return;
    }

    private void OnLiarPerformed(InputAction.CallbackContext context)
    {
        if (PlayerState.Local == null) return;
    }

    private void OnLookPerformed(InputAction.CallbackContext context)
    {
        if (PlayerState.Local == null) return;
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
}
