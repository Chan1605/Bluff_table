using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugNicknameSource : MonoBehaviour
{
    [SerializeField] NamePlateBoard board;
    [SerializeField] string[] fakeNames = { "플레이어1", "플레이어2", "플레이어3",
        "닉네임이얼마나길어질수있는지확인하는것은매우중대한사항이다그렇기에일부러길게늘린이글의글자수는오십하고도여섯이다" };
    [SerializeField] int[] fakeSeats = { 0, 1, 2, 3 };

    void Start()
    {
        board.ShowSeats(fakeSeats);
        foreach (int seat in fakeSeats)
        {
            board.SetName(seat, fakeNames[seat]);
        }
    }
}
