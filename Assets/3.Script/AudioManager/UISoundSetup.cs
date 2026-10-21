using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UISoundSetup : MonoBehaviour
{
    [SerializeField] private AudioClip sceneBGM;
    [SerializeField] private AudioClip sceneAMB;

    [Header("공통 UI 사운드")]
    [SerializeField] private AudioClip defaultHoverSound;
    [SerializeField] private AudioClip defaultClickSound;
    void Start()
    {
        if (AudioManager.Instance != null)
        {
            if(sceneBGM !=null)
            {
                AudioManager.Instance.PlayBGM(sceneBGM);
            }
            if (sceneAMB != null)
            {
                AudioManager.Instance.PlayAMB(sceneAMB);
            }
        }

        Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach(Button btn in allButtons)
        {
            if(!btn.TryGetComponent<BtnSound>(out BtnSound btnSound))
            {
                btnSound = btn.gameObject.AddComponent<BtnSound>();
            }

            btnSound.Setup(defaultHoverSound, defaultClickSound);
        }

        Debug.Log($"[UISoundSetup] {allButtons.Length}개의 버튼에 사운드를 적용했습니다.");
    }

}
