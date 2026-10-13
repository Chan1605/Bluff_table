using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnPresenter : MonoBehaviour
{
    private TurnIndicator indicator;
    private NamePlateBoard nameplates;

    private void Awake()
    {
        indicator = GetComponentInChildren<TurnIndicator>();
        nameplates = GetComponent<NamePlateBoard>();
    }

    public void BeginRound(int startSeat)
    {
        indicator.BeginRound(startSeat);
        nameplates.SetTurn(startSeat);
    }

    public void SetTurn(int seat)
    {
        indicator.SetTurn(seat);
        nameplates.SetTurn(seat);
    }

    public void Hide()
    {
        indicator.Hide();
        nameplates.SetTurn(-1);
    }
}
