using System.Collections.Generic;
using System.Text;
using LiarsBar.Server;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


public class RoomChatUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField chatInput;  
    [SerializeField] private TMP_Text logText;           // 메시지가 쌓이는 텍스트
    [SerializeField] private int maxLines = 8;

    private readonly Queue<string> lines = new();
    private readonly StringBuilder sb = new();
    private int lastSubmitFrame = -1;

    private void Awake()
    {
        if (chatInput != null) chatInput.characterLimit = RoomChat.MaxLength;
        if (logText != null) logText.richText = false;   
    }

    private void OnEnable()
    {
        RoomChat.RegisterClient();
        RoomChat.Received += OnReceived;
        if (chatInput != null) chatInput.onSubmit.AddListener(OnSubmit);

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null) Keyboard.current.SetIMEEnabled(true);
#endif

        lines.Clear();
        Redraw();
    }

    private void OnDisable()
    {
        RoomChat.Received -= OnReceived;
        RoomChat.UnregisterClient();
        if (chatInput != null) chatInput.onSubmit.RemoveListener(OnSubmit);
    }

    private void Update()
    {
        if (chatInput == null || chatInput.isFocused) return;
        if (LoadingUI.IsShowing) return;                  // 로딩 화면 뒤에서 채팅창이 열리지 않게
        if (Time.frameCount == lastSubmitFrame) return;   // 전송한 같은 프레임의 Enter는 무시
        if (!EnterPressed()) return;

        chatInput.ActivateInputField();
    }

    private static bool EnterPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    private void OnSubmit(string text)
    {
        lastSubmitFrame = Time.frameCount;
        RoomChat.Send(text);   // 빈 문자열은 무시됨 (Enter만 누르면 입력창만 닫힘)

        chatInput.text = string.Empty;
        chatInput.DeactivateInputField();
    }

    private void OnReceived(string sender, string text)
    {
        lines.Enqueue($"{sender}: {text}");
        while (lines.Count > maxLines) lines.Dequeue();
        Redraw();
    }

    private void Redraw()
    {
        if (logText == null) return;

        sb.Clear();
        foreach (string line in lines) sb.AppendLine(line);
        logText.text = sb.ToString();
    }
}