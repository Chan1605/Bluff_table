using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NamePlateBoard : MonoBehaviour
{
    [Header("이름표들: 0부터 3까지 순서대로 배치해주세요.")]
    [SerializeField] private NamePlate[] plates;

    int turn = -1;

    public void SetName(int seat, string nick)
    {
        plates[seat].SetName(nick);
    }
    public void SetShots(int seat, int fired)
    {
        plates[seat].SetShots(fired);
    }
    public void SetDead(int seat, bool isDead)
    {
        plates[seat].SetDead(isDead);
    }
    public void SetTurn(int seat)
    {
        for (int i = 0; i < plates.Length; i++)
        {
            plates[i].SetTurn(i == seat);
        }
    }

    public void ShowSeats(int[] seats)
    {
        for (int i = 0; i < plates.Length; i++)
            plates[i].gameObject.SetActive(System.Array.IndexOf(seats, i) >= 0);
    }
}
