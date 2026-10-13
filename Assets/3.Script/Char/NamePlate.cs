using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NamePlate : MonoBehaviour
{
    [SerializeField] private Text NameText;
    [SerializeField] private Text ShotText;
    [SerializeField] private Image PlateImg;

    [SerializeField] private Sprite RouletteSprite;
    [SerializeField] private Sprite DeathSprite;

    private void Start()
    {
        SetShots(0);
        PlateImg.sprite = RouletteSprite;
    }

    public void SetName(string nick)
    {
        NameText.text = nick;
    }
    public void SetShots(int fired)
    {
        ShotText.text = $"({fired}/6)";
    }
    public void SetDead(bool isDead)
    {
        if (!isDead) return;

        PlateImg.sprite = DeathSprite;
    }

    //void LateUpdate() { /* 카메라 쪽으로 빌보드 */ }

}
