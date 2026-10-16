using UnityEngine;

public class CustomCursor : MonoBehaviour
{
    public static CustomCursor Instance;
    private RectTransform cursorRt;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
        }
        else
        {
            Destroy(transform.root.gameObject);
            return;
        }

        Cursor.visible = false;
        TryGetComponent(out cursorRt);
    }

    private void Update()
    {
        cursorRt.position = Input.mousePosition;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible); // 비활성화되면 Update nonono
    }
}