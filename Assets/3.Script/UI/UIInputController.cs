using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class UIInputController : MonoBehaviour
{
    private PlayerController localPlayerController;
    private bool isInGameMenuOpen = false;

    private void OnEnable()
    {
        InGameEvent.OnMenuOpened += OpenInGameMenu;
        InGameEvent.OnReEntryInLobby += ReturnToLobby;
    }

    private void OnDisable()
    {
        InGameEvent.OnMenuOpened -= OpenInGameMenu;
        InGameEvent.OnReEntryInLobby -= ReturnToLobby;

        if(localPlayerController != null)
        {
            localPlayerController.inputActions.UI.Select.performed -= OnSelect;
            localPlayerController.inputActions.UI.Confirm.performed -= OnConfirm;
            localPlayerController.inputActions.UI.Escape.performed -= OnEscape;

            localPlayerController = null;
        }
    }

    private IEnumerator Start()
    {
        yield return new WaitUntil(() =>
            PlayerState.Local != null &&
            PlayerState.Local.TryGetComponent(out PlayerController controller) &&
            controller.inputActions != null
        );

        SetupUIInput();
    }

    private void ReturnToLobby()
    {
        if (isInGameMenuOpen)
        {
            isInGameMenuOpen = false;
            InGameEvent.TriggerOnMenuClosed();
        }

        Debug.Log("[UIInput] : UI 인풋 전환");

    }

    private void SetupUIInput() // ActionMap이 Player인 상태에서 UI로 전환되면서 호출되는 메서드입니다.
    {
        if (localPlayerController != null) return;

        if (PlayerState.Local == null) return;

        if (!PlayerState.Local.TryGetComponent(out PlayerController playerController)) return;

        if (playerController.inputActions == null) return;

        localPlayerController = playerController;

        localPlayerController.inputActions.UI.Select.performed += OnSelect;
        localPlayerController.inputActions.UI.Confirm.performed += OnConfirm;
        localPlayerController.inputActions.UI.Escape.performed += OnEscape;

        Debug.Log("[UIInput] : 입력 등록 완료");
    }
    private void OpenInGameMenu()
    {
        isInGameMenuOpen = true;
        SetupUIInput();
    }

    private void OnSelect(InputAction.CallbackContext context)
    {
        Vector2 navDir = context.ReadValue<Vector2>();
    }

    private void OnConfirm(InputAction.CallbackContext context)
    {

    }

    private void OnEscape(InputAction.CallbackContext context)
    {
        if (!isInGameMenuOpen)
        {
            //TODO 아웃게임 Esc 로직
        }
        else
        {
            isInGameMenuOpen = false;

            localPlayerController.inputActions.UI.Disable();
            localPlayerController.inputActions.Player.Enable();

            InGameEvent.TriggerOnMenuClosed();
        }
    }

}
