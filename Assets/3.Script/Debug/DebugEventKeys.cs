using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugEventKeys : MonoBehaviour
{
    [SerializeField] private NamePlateBoard board;
    [SerializeField] private TurnIndicator turnIndicator;
    [SerializeField] TurnPresenter show_turn;

    private DebugNicknameSource seatSource;
    private int selected;
    private int turn;
    private int[] fakeSeats;

    private readonly bool[] dead = new bool[4];
    private readonly int[] shots = new int[4];   // 진짜로는 게임 로직이 가진 값

    private void Awake()
    {
        seatSource = GetComponent<DebugNicknameSource>();
    }

    void Start()
    {
        turnIndicator.SetTurn(turn);
        fakeSeats = seatSource.Seats;
    }

    int RandomAliveSeat()
    {
        var alive = System.Array.FindAll(seatSource.Seats, s => !dead[s]);
        return alive.Length > 0 ? alive[Random.Range(0, alive.Length)] : -1;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;   // 키보드가 없는 환경 대비

        for (int i = 0; i < 4; i++)
            if (kb[Key.Digit1 + i].wasPressedThisFrame) selected = i;

        if (kb.fKey.wasPressedThisFrame)
            board.SetShots(selected, ++shots[selected]);

        if (kb.kKey.wasPressedThisFrame)
            board.SetDead(selected, dead[selected] = !dead[selected]);

        if (kb.rKey.wasPressedThisFrame)
        {
            int start = RandomAliveSeat();
            if (start >= 0)
            {
                show_turn.BeginRound(turn = fakeSeats[Random.Range(0, fakeSeats.Length)]);
            }
        }
        

        if (kb.tKey.wasPressedThisFrame) //다음 턴으로 넘기기
            show_turn.SetTurn(turn = NextSeat(turn));

        if (kb.hKey.wasPressedThisFrame) //턴 종료 모사
            show_turn.Hide();

        int NextSeat(int seat)
        {
            int[] seats = seatSource.Seats;
            int i = System.Array.IndexOf(seats, seat);

            for (int step = 1; step <= seats.Length; step++)
            {
                int next = seats[(i + step) % seats.Length];
                if (!dead[next]) return next;
            }
            return seat;  // 살아 있는 사람이 없으면 그대로
        }
    }
}
