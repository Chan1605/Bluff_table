using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnPresenter : MonoBehaviour
{
    private TurnIndicator indicator;
    private NamePlateBoard nameplates;

    private void Awake()
    {
        indicator = GetComponentInChildren<TurnIndicator>(true);
        nameplates = GetComponent<NamePlateBoard>();
    }

    // 서버가 알려 주는 턴에 맞춰 테이블 화살표 · 이름판 턴 표시를 움직인다
    private void OnEnable()
    {
        InGameEvent.OnTurnChanged += SetTurn;
        InGameEvent.OnRoundStarted += HandleRoundStarted;
        InGameEvent.OnLiarCalled += HandleLiarCalled;
        InGameEvent.OnReEntryInLobby += Hide;
    }

    private void OnDisable()
    {
        InGameEvent.OnTurnChanged -= SetTurn;
        InGameEvent.OnRoundStarted -= HandleRoundStarted;
        InGameEvent.OnLiarCalled -= HandleLiarCalled;
        InGameEvent.OnReEntryInLobby -= Hide;
    }

    private void HandleRoundStarted(CardType card) => Hide();          // 배분 연출 중에는 숨김 (첫 턴에 다시 나타남)
    private void HandleLiarCalled(int callerSeat, int loserSeat) => Hide();

    public void BeginRound(int startSeat)
    {
        if (indicator != null) indicator.BeginRound(startSeat);
        if (nameplates != null) nameplates.SetTurn(startSeat);
    }

    public void SetTurn(int seat)
    {
        if (indicator != null) indicator.SetTurn(seat);
        if (nameplates != null) nameplates.SetTurn(seat);
    }

    public void Hide()
    {
        if (indicator != null) indicator.Hide();
        if (nameplates != null) nameplates.SetTurn(-1);
    }
}
