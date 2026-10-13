using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NamePlate : MonoBehaviour
{
    [SerializeField] private Text NameText;
    [SerializeField] private Text ShotText;
    [SerializeField] private Image PlateImg;
    [SerializeField] private GameObject turnImg;

    [SerializeField] private Sprite RouletteSprite;
    [SerializeField] private Sprite DeathSprite;

    private void Awake()
    {
        turnImg.SetActive(false);
    }

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

    public void SetTurn(bool isTurn)
    {
        turnImg.SetActive(isTurn);
    }


}
