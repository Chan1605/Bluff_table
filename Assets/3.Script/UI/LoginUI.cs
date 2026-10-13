using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoginUI : MonoBehaviour
{
    [SerializeField] GameObject LoginPanel;
    [SerializeField] Button LoginBtn;


    void Start()
    {
        LoginBtn.onClick.AddListener(() => { LoginPanel.gameObject.SetActive(false); });    
    }


}
