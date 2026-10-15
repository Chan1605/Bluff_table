using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] PlayerInput p_input;
    [SerializeField] PlayerLook look;

    InputAction lookAction;
    public bool isInputBlocked { get; set; }

    private void Awake()
    {
        //player input 델리게이트 방식으로 새로 생성함으로 null exeption 뜨는 해당 줄 비활성화 하였습니다. - 지은
        //lookAction = p_input.actions["Look"];
    }

    private void Update()
    {
        if (isInputBlocked) return;
        look.AddLookDelta(lookAction.ReadValue<Vector2>());
    }
}
