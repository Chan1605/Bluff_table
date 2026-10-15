using UnityEngine;
using UnityEngine.UI;

public class NamePlate : MonoBehaviour
{
    [Header("자리 번호 (0~3). 스폰 코드에서 SetSeat으로 지정해도 됩니다.")]
    [SerializeField] private int seat = -1;

    [SerializeField] private Text NameText;
    [SerializeField] private Text ShotText;
    [SerializeField] private Image PlateImg;
    [SerializeField] private GameObject turnImg;

    [SerializeField] private Sprite RouletteSprite;
    [SerializeField] private Sprite DeathSprite;

    public int Seat => seat;

    private void Awake()
    {
        turnImg.SetActive(false);
        PlateImg.sprite = RouletteSprite;
        SetShots(0);
    }

    private void Start()
    {
        if (NamePlateBoard.Instance != null)
            NamePlateBoard.Instance.Register(this);
    }

    private void OnDestroy()
    {
        if (NamePlateBoard.Instance != null)
            NamePlateBoard.Instance.Unregister(this);
    }

    public void SetSeat(int newSeat)
    {
        if (seat == newSeat) return;

        var board = NamePlateBoard.Instance;
        if (board != null) board.Unregister(this);
        seat = newSeat;
        if (board != null) board.Register(this);
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