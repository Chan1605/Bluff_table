using TMPro;
using UnityEngine;


[RequireComponent(typeof(TMP_Text))]
[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string key;

    private TMP_Text text;

    public string Key => key;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        if (string.IsNullOrEmpty(key)) key = text.text;
    }

    private void OnEnable()
    {
        Localization.LanguageChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        Localization.LanguageChanged -= Apply;
    }

    public void SetKey(string newKey)
    {
        key = newKey;
        Apply();
    }

    private void Apply()
    {
        if (text != null) text.text = Localization.Get(key);
    }
}
