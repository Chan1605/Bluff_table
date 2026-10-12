using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

using LiarsBar.Server;

public class DeckGamePlayManager : NetworkBehaviour, IGameFlow
{
    // 서버 권한 스크립트입니다
    private PlayerState[] Players = new PlayerState[4];

    private List<CardType> serverDeck = new List<CardType>();
    private Dictionary<int, List<CardType>> playerHands;

    private int accumTurnIndex = 0;
    private PlayerState currentTurnPlayer;

    [SyncVar]
    private CardType tableCard;

    public event Action<int> GameOver;

    [Server]
    public void StartGame(int[] seats)
    {
        Debug.Log($"[인게임] StartGame 진입 성공. 전달받은 좌석 수 : {seats.Length}");

        PlayerState[] foundPlayers = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);

        Debug.Log($"[인게임] 씬에서 찾은 PlayerState 개수 : {foundPlayers.Length}");

        foreach (PlayerState pState in foundPlayers)
        {
            int seatNum = pState.GetComponent<PlayerProfile>().Seat;
            Players[seatNum] = pState;
        }

        accumTurnIndex = 0;
        StartRound();
    }

    [Server]
    public void StartRound()
    {
        playerHands = new Dictionary<int, List<CardType>>();

        //TODO 1. 테이블 선정
        SelectTable_normal();

        //TODO 2. 덱 셔플
        ShuffleDeck_normal();

        //TODO 3. 카드 분배
        devideDeck();

        //TODO 3. 턴 선정
        NextTurn();
    }

    [Server]
    private void SelectTable_normal()
    {
        tableCard = (CardType)UnityEngine.Random.Range(0, 3);

        RpcStartRound(tableCard);
    }

    [ClientRpc]
    private void RpcStartRound(CardType card)
    {
        Debug.Log($"[인게임] 이번 테이블 :  {card}");
        InGameEvent.TriggerOnRoundStarted(card);
    }

    [Server]
    private void ShuffleDeck_normal()
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
        //List<CardType[]> devidedDeck = new List<CardType[]>();
        Debug.Log($"[인게임] devideDeck 루프 시작");

        for (int i = 0; i<4; i++)
        {
            if (Players[i] == null) continue;
            PlayerProfile profile = Players[i].GetComponent<PlayerProfile>();
            NetworkConnectionToClient conn = profile.connectionToClient;

            Debug.Log($"[인게임] {i} 번 좌석 발견. 커넥션 상태 : {(conn!=null ? "정상(NotNull" : "Null")}");
            List<CardType> nowPlayerDeck = new List<CardType>();

            for(int j = 0; j<5; j++)
            {
                nowPlayerDeck.Add(serverDeck[(i * 5) + j]);
            }

            //devidedDeck.Add(nowPlayerDeck);
            Debug.Log($"[인게임] {i} 번 좌석 플레이어에게 TargetGiveCards 호출 시도");

            playerHands.Add(i,new List<CardType>(nowPlayerDeck));
            Players[i].TargetGiveCards(nowPlayerDeck, Players[i].isDead);
        }
    }

    [Server]
    public void NextTurn()
    {
        int maxPlayerCount = Players.Length;
        currentTurnPlayer = null;

        foreach (PlayerState player in Players)
        {
            if (player != null) player.isTurn = false;
        }

        for (int i = 0; i < maxPlayerCount; i++)
        {
            int seatIndex = accumTurnIndex % maxPlayerCount;
            PlayerState curPlayer = Players[seatIndex];

            accumTurnIndex++;

            if (curPlayer != null && curPlayer.isDead == false)
            {
                curPlayer.isTurn = true;
                currentTurnPlayer = curPlayer;

                Debug.Log($"[인게임] : 다음 턴 {seatIndex}번 좌석");

                RpcTurnChanged(seatIndex);
                return;
            }
        }

        Debug.Log("[인게임] : 다음 턴을 받을 플레이어가 없습니다.");
    }

    [ClientRpc]
    private void RpcTurnChanged(int seatIndex)
    {
        InGameEvent.TriggerOnTurnChanged(seatIndex);
    }

    public void HandleDisconnect(int seat)
    {
    }


}
