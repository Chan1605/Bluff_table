using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugEventKEys : MonoBehaviour
{
    [SerializeField] NamePlateBoard board;
    [SerializeField] TurnIndicator t_indicator;

    int selected;
    readonly int[] shots = new int[4];   // 진짜로는 게임 로직이 가진 값
    readonly bool[] dead = new bool[4];
    int turn;

    void Start() => t_indicator.SetTurn(turn);

    void Update()
    {
        for (int i = 0; i < 4; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) selected = i;

        if (Input.GetKeyDown(KeyCode.F) && shots[selected] < 6)
            board.SetShots(selected, ++shots[selected]);

        if (Input.GetKeyDown(KeyCode.K))
            board.SetDead(selected, dead[selected] = !dead[selected]);

        if (Input.GetKeyDown(KeyCode.T))
        {
            turn = (turn + 1) % 4;
            t_indicator.SetTurn(turn);
        }
    }
}
