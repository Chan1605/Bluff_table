using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] InputActionReference lookActionRef;
    [SerializeField] PlayerLook look;

    InputAction lookAction;
    public bool isInputBlocked { get; set; }

    private void Awake()
    {
        if (lookActionRef != null)
            lookAction = lookActionRef.action;
        else
            Debug.LogError("PlayerInputHandler: Look 액션이 연결되지 않았습니다.", this);
    }

    private void OnEnable()
    {
        lookAction?.Enable();
    }

    private void OnDisable()
    {
        lookAction?.Disable();
    }

    private void Update()
    {
        if (isInputBlocked || lookAction == null) return;
        look.AddLookDelta(lookAction.ReadValue<Vector2>());
    }
}