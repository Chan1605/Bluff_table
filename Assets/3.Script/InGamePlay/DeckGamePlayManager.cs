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

    private List<CardType> previousCards = new List<CardType>();
    private int lastSubmittedcards;
    private int lastSubmittedSeat = -1;
    private bool isResolvingLiar = false;

    private int accumTurnIndex = 0;
    private int nextRoundStartSeat = 0;

    private PlayerState currentTurnPlayer;
    [SerializeField] private PlayerVisualSetup visualSetup;

    [SyncVar]
    private CardType tableCard;

    public event Action<int> GameOver;

    [Server]
    public void StartGame(int[] seats)
    {
        Debug.Log($"[인게임] StartGame 진입 성공. 전달받은 좌석 수 : {seats.Length}");
        Array.Clear(Players, 0, Players.Length);

        //NetworkServer.RebuildObservers(this.netIdentity, false);
        //PlayerState[] foundPlayers = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
        //List<PlayerProfile> foundPlayers = new List<PlayerProfile>();

        foreach (int seat in seats)
        {
            if (seat < 0 || seat >= Players.Length)
            {
                Debug.Log($"[인게임] : 좌석 범위 초과 {seat}");
                return;
            }

            if (!LiarsNetworkManager.Instance.TryGetPlayer(this, seat, out PlayerProfile pProfile) || pProfile == null)
            {
                Debug.Log($"[인게임] {seat} 번 플레이어 조회 실패");
                return;
            }

            if (!pProfile.TryGetComponent<PlayerState>(out PlayerState pState))
            {
                Debug.Log($"[인게임] {seat} 번 PlayerState 없음");
                return;
            }

            Players[seat] = pState;
            pState.SeatNum = seat;
            pState.deckManager = this;
            pState.ResetState();
            visualSetup.RpcSpawnVisual(pProfile);

        }

        accumTurnIndex = 0;
        nextRoundStartSeat = UnityEngine.Random.Range(0, Players.Length);
        RpcEntryInGame();
        StartCoroutine(StartRound_co());
    }

    [Server]
    private IEnumerator StartRound_co()
    {
        yield return new WaitForSeconds(GameFlowTiming.GameEntry);

        StartRound();
    }

    [Server]
    public void StartRound()
    {
        PlayerState AllivePlayer = null;
        int Allive = 0;

        for (int i = 0; i < Players.Length; i++)
        {
            PlayerState curPlayer = Players[i];

            if (curPlayer == null)
                continue;

            if (!curPlayer.isDead)
            {
                Allive++;
                AllivePlayer = curPlayer;
                curPlayer.RoundReset();
            }
        }

        if (Allive == 1 && AllivePlayer != null)
        {
            EndGame(AllivePlayer.SeatNum);
            return; // 게임 종료 로직
        }
        else if (Allive == 0)
        {
            Debug.Log("[인게임] 생존자 없음");
            return;
        }

        previousCards.Clear();
        lastSubmittedcards = 0;
        lastSubmittedSeat = -1;
        isResolvingLiar = false;
        playerHands = new Dictionary<int, List<CardType>>();

        //TODO 1. 테이블 선정
        SelectTable_normal();

        //TODO 2. 덱 셔플
        ShuffleDeck_normal();

        //TODO 3. 카드 분배
        devideDeck();

        //TODO 3. 턴 선정
        accumTurnIndex = nextRoundStartSeat;
        NextTurn();
    }

    [Server]
    private void SelectTable_normal()
    {
        tableCard = (CardType)UnityEngine.Random.Range(0, 3);
        RpcStartRound(tableCard);
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

        for (int i = 0; i < Players.Length; i++)
        {
            PlayerState player = Players[i];

            if (player == null) continue;

            List<CardType> hand = new List<CardType>();

            for (int j = 0; j < 5; j++)
            {
                hand.Add(serverDeck[(i * 5) + j]);
            }

            playerHands.Add(i, hand);
            player.TargetGiveCards(hand, player.isDead);
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

            if (curPlayer != null && curPlayer.isDead == false && !curPlayer.isEscaped)
            {
                curPlayer.isTurn = true;
                currentTurnPlayer = curPlayer;

                Debug.Log($"[인게임] : 다음 턴 {seatIndex}번 좌석");

                RpcTurnChanged(seatIndex);
                StartCoroutine(TurnTimer_co(accumTurnIndex, seatIndex));
                return;
            }
        }

        Debug.Log("[인게임] : 다음 턴을 받을 플레이어가 없습니다.");
    }

    [Server]
    private IEnumerator TurnTimer_co(int expectedTurnIndex, int seatNum)
    {
        yield return new WaitForSeconds(GameFlowTiming.TurnTimer);

        if (!isResolvingLiar && accumTurnIndex == expectedTurnIndex)
        {
            Debug_RpcLog("[인게임] : 타임아웃");

            if (!LastManStandingVerification())
            {
                Debug_RpcLog("[인게임] : 타임아웃, 강제 Liar 호출 (1대 1 )");
                CallLiar(seatNum);
            }
            else
            {
                if (playerHands.TryGetValue(seatNum, out List<CardType> hand) && hand.Count > 0)
                {
                    int randomIdx = UnityEngine.Random.Range(0, hand.Count);
                    Debug_RpcLog($"[인게임] : 타임아웃, 강제 패 제출 ({randomIdx})");

                    PlayerSubmitCard(seatNum, new int[] { randomIdx });
                }
            }
        }
    }

    [Server]
    public void PlayerSubmitCard(int playerSeat, int[] submitIndex)
    {
        if (!PlayerTurnVerification(playerSeat))
        {
            Debug_RpcLog("[인게임] : 요청 거절 (플레이어 턴이 아닌 제출 감지)");
            return;
        }

        if (submitIndex == null || submitIndex.Length < 1 || submitIndex.Length > 4)
        {
            Debug_RpcLog("[인게임] : 요청 거절 (제출한 카드가 없거나, 범위 초과. 1장 이상 4장 이하로 제출 요함)");
            return;
        }

        if (!playerHands.TryGetValue(playerSeat, out List<CardType> hands))
        {
            Debug_RpcLog("[인게임] : 요청 거절 (플레이어 손에서 해당 인덱스 카드를 찾을 수 없음)");
            return;
        }

        if (!LastManStandingVerification())
        {
            Debug_RpcLog("[인게임] : 요청 거절 (플레이어가 두명 남은 상태에서 앞 사람이 모든 카드를 냄)");
            return;
        }


        HashSet<int> usedIndex = new HashSet<int>();

        foreach (int idx in submitIndex)
        {
            if (idx < 0 || idx >= hands.Count || !usedIndex.Add(idx))
            {
                Debug.Log($"[인게임] : 유효하지 않은 카드 인덱스 {idx}");
                return;
            }
        }

        Array.Sort(submitIndex);
        Array.Reverse(submitIndex);


        foreach (int idx in submitIndex)
        {
            CardType curCard;

            curCard = hands[idx];

            previousCards.Add(curCard);
            hands.RemoveAt(idx);
        }

        if (lastSubmittedSeat != -1)
        {
            ConfirmEscape(lastSubmittedSeat);
        }

        lastSubmittedcards = submitIndex.Length;
        lastSubmittedSeat = playerSeat;

        RpcSubmitCard(playerSeat, submitIndex.Length);
        Players[playerSeat].TargetGiveCards(hands, false);
        NextTurn();
    }

    [Server]
    private bool PlayerTurnVerification(int trierSeat)
    {
        if (isResolvingLiar)
            return false;

        if (currentTurnPlayer == null)
            return false;

        if (trierSeat != currentTurnPlayer.SeatNum)
        {
            Debug.Log("[인게임] : 턴이 아닌 플레이어의 제출 감지됨");
            return false;
        }

        return true;
    }

    [Server]
    private bool LastManStandingVerification()
    {
        if (lastSubmittedSeat != -1)
        {
            if (playerHands.TryGetValue(lastSubmittedSeat, out List<CardType> prevHands))
            {
                if (prevHands.Count == 0)
                {
                    int aliveCount = 0;
                    foreach (PlayerState p in Players)
                    {
                        if (p != null && !p.isDead && !p.isEscaped)
                        {
                            aliveCount++;
                        }
                    }

                    if (aliveCount <= 2)
                    {
                        Debug.Log("[인게임] : 최후의 2인 상황에서 상대가 0장이 되었습니다.");
                        return false;
                    }
                }
            }
        }
        return true;
    }

    [Server]
    private void ConfirmEscape(int seat)
    {
        if (seat < 0 || seat >= Players.Length)
            return;

        PlayerState player = Players[seat];

        if (player == null || player.isDead || player.isEscaped)
            return;

        if (!playerHands.TryGetValue(seat, out List<CardType> hand))
            return;

        if (hand.Count != 0)
            return;

        player.isEscaped = true;
        Debug.Log($"[인게임] : {lastSubmittedSeat}번 유저 라운드 탈출");
    }

    [Server]
    public void CallLiar(int callerSeat)
    {
        if (!PlayerTurnVerification(callerSeat))
            return;

        if (lastSubmittedSeat == -1 || lastSubmittedcards <= 0)
            return;

        if (callerSeat == lastSubmittedSeat)
            return;

        isResolvingLiar = true;
        bool liar = false;

        int totalCards = previousCards.Count;
        int startIndex = totalCards - lastSubmittedcards;
        int loserSeat;

        for (int i = startIndex; i < totalCards; i++)
        {
            if (previousCards[i] != tableCard && previousCards[i] != CardType.Joker)
            {
                liar = true;
            }
        }

        if (liar)
        {
            loserSeat = lastSubmittedSeat;
        }
        else
        {
            loserSeat = callerSeat;
        }


        StartCoroutine(ResolveLiarSequence_co(loserSeat));
    }

    [Server]
    private IEnumerator ResolveLiarSequence_co(int loserSeat)
    {
        bool isBullet;
        RpcAnnounceLiar(loserSeat);
        yield return new WaitForSeconds(GameFlowTiming.RouletteAnimation);

        isBullet = ResolveRoulette(loserSeat);
        yield return new WaitForSeconds(GameFlowTiming.LierReveal);

        if (isBullet)
        {
            //처형
            RpcAnnounceExecute(loserSeat);
        }
        else
        {
            //생존
            RpcAnnounceSurvive(loserSeat);
        }
        yield return new WaitForSeconds(GameFlowTiming.ResultDisplay);

        nextRoundStartSeat = loserSeat;
        StartRound();
    }

    [Server]
    private bool ResolveRoulette(int seat)
    {
        PlayerState loserState = Players[seat];

        if (loserState.currentChamber == loserState.bulletPosition)
        {
            loserState.isDead = true;
            return true;
        }
        else
        {
            loserState.currentChamber++;
            return false;
        }
    }

    #region Client RPC

    [ClientRpc]
    private void RpcEntryInGame()
    {
        InGameEvent.TriggerOnEntryInGame();
    }

    [ClientRpc]
    private void RpcReEntryInLobby()
    {
        InGameEvent.TriggerOnReEntryInLobby();
    }

    [ClientRpc]
    private void RpcStartRound(CardType card)
    {
        InGameEvent.TriggerOnRoundStarted(card);
        Debug.Log($"이번 테이블 카드는 {card} 입니다.");
    }

    [ClientRpc]
    public void RpcSubmitCard(int seatNum, int cardCount)
    {
        Debug.Log($"[인게임] : {seatNum}번 플레이어 카드 {cardCount}장 제출 완료");
    }

    [ClientRpc]
    private void RpcTurnChanged(int seatIndex)
    {
        InGameEvent.TriggerOnTurnChanged(seatIndex);
    }

    [ClientRpc]
    private void RpcAnnounceLiar(int seat)
    {
        // 해당 유저 총 드는 이벤트 or 연출
    }

    [ClientRpc]
    private void RpcAnnounceExecute(int seat)
    {
        // 유저 처형 이벤트 실행
        Debug.Log($" [인게임] : {seat} 번째 유저 처형");
        InGameEvent.TriggerOnPlayerEliminated(seat);
    }

    [ClientRpc]
    private void RpcAnnounceSurvive(int seat)
    {
        // 유저 생존
        Debug.Log($" [인게임] : {seat} 번째 유저 생존");
    }

    [ClientRpc]
    private void RpcAnnounceWinner(int winner)
    {
        Debug.Log($"{winner} 번 플레이어 승");
    }

    [ClientRpc]
    private void Debug_RpcLog(string s)
    {
        Debug.Log(s);
    }

    #endregion

    [Server]
    private void EndGame(int winner)
    {
        RpcAnnounceWinner(winner);
        RpcReEntryInLobby();
        GameOver?.Invoke(winner);
    }

    public void HandleDisconnect(int seat)
    {
        Players[seat] = null;
        Debug.Log($"[Dummy] HandleDisconnect - 좌석 {seat}");
    }


}
