using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugCardTrigger : MonoBehaviour
{
    [SerializeField] CardPlacer placer;
    [SerializeField] CardType tableCard = CardType.Queen;

    void OnGUI()
    {
        if (GUI.Button(new Rect(10, 10, 160, 40), "라운드 시작"))
            InGameEvent.TriggerOnRoundStarted(tableCard);

        if (GUI.Button(new Rect(10, 60, 160, 40), "내 손패 도착"))
            InGameEvent.TriggerOnCardDealt(RandomHand());

        if (GUI.Button(new Rect(10, 110, 160, 40), "덱으로 리셋"))
            placer.ResetToDeck();
    }

    static List<CardType> RandomHand()
    {
        var hand = new List<CardType>();
        for (int i = 0; i < 5; i++)
            hand.Add((CardType)Random.Range(0, 4));
        return hand;
    }
}

