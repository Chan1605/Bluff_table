using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class DeckGamePlayManager : NetworkBehaviour 
{
    // 서버 권한 스크립트입니다
    private List<CardType> serverDeck = new List<CardType>();

    [Server]
    public void StartRound()
    {
        //TODO 1. 상태 초기화

        //TODO 2. 덱 셔플

        //TODO 3. 카드 분배
    }

    [Server]
    private void ShuffleTurn_normal()
    {

    }

    [Server]
    private void ShuffleDeck()
    {
        serverDeck.Clear();

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                serverDeck.Add((CardType)i);
            }
        }
        serverDeck.Add(CardType.Joker);
        serverDeck.Add(CardType.Joker);

        for (int i = serverDeck.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);

            CardType tempCard = serverDeck[i];
            serverDeck[i] = serverDeck[randomIndex];
            serverDeck[randomIndex] = tempCard;
        }
    }

    [Server]
    private void devideDeck()
    {
        List<CardType[]> devidedDeck = new List<CardType[]>();

        for (int i = 0; i<4; i++)
        {
            CardType[] nowPlayerDeck = new CardType[5];

            for(int j = 0; j<5; j++)
            {
                nowPlayerDeck[j] = serverDeck[(i * 5) + j];
            }

            devidedDeck.Add(nowPlayerDeck);
        }
    }

    [TargetRpc]
    public void TargetGiveCards(NetworkConnectionToClient conn, CardType[] handCards)
    {

    }

}
