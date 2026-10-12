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
        lookAction = p_input.actions["Look"];
    }

    private void Update()
    {
        if (isInputBlocked) return;
        look.AddLookDelta(lookAction.ReadValue<Vector2>());
    }
}
