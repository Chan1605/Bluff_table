using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugNicknameSource : MonoBehaviour
{
    [SerializeField] NamePlateBoard board;
    [SerializeField] string[] fakeNames = { "플레이어1", "Player2", "플레이어3",
        "CheckEnglish" };
    [SerializeField] int[] fakeSeats = { 0, 1, 2, 3 };

    public int[] Seats => fakeSeats;

    void Start()
    {
        board.ShowSeats(fakeSeats);
        foreach (int seat in fakeSeats)
        {
            board.SetName(seat, fakeNames[seat]);
        }
    }
}
