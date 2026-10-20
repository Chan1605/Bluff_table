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
            visualSetup.RpcSpawnVisual(pProfile, pProfile.CharacterId);

        }

        accumTurnIndex = 0;
        nextRoundStartSeat = UnityEngine.Random.Range(0, Players.Length);
        StartCoroutine(StartRound_co());
    }

    // 모든 플레이어가 테이블 씬을 다 불러오고 로딩 화면 최소 시간이 지나면(PlayerState.CmdSceneReady)
    // 동시에 게임에 들어가고(RpcEntryInGame → 로딩 화면 닫힘), 잠깐 뒤 카드 연출 시작
    private bool waitingLoad;

    [Server]
    private IEnumerator StartRound_co()
    {
        waitingLoad = true;
        float giveUp = Time.time + GameFlowTiming.SceneLoadTimeout;
        while (!AllPlayersLoaded() && Time.time < giveUp) yield return null;
        waitingLoad = false;

        if (!AllPlayersLoaded()) Debug.Log("[인게임] : 씬 로딩 대기 시간 초과 - 그대로 시작");
        else Debug.Log("[인게임] : 모든 플레이어 로딩 완료");

        RpcEntryInGame();
        yield return new WaitForSeconds(GameFlowTiming.GameEntry);

        StartRound();
    }

    [Server]
    private bool AllPlayersLoaded()
    {
        foreach (PlayerState p in Players)
            if (p != null && !p.SceneReady) return false;
        return true;
    }

    #region 게임 로직

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

        //TODO 3. 턴 선정 - 모든 클라이언트의 배분 연출이 끝난 뒤에 첫 턴 (연출 중에 타이머가 흐르지 않게)
        StopTurnTimer();
        currentTurnPlayer = null;
        dealReadySeats.Clear();
        waitingDeal = true;
        if (dealWait_co != null) StopCoroutine(dealWait_co);
        dealWait_co = StartCoroutine(DealWaitTimeout_co());
    }

    #region 배분 연출 대기

    private readonly HashSet<int> dealReadySeats = new HashSet<int>();
    private bool waitingDeal;
    private Coroutine dealWait_co;

    // PlayerState.CmdDealFinished → 클라이언트에서 손패 배분 연출이 끝남
    [Server]
    public void ReportDealFinished(int seat)
    {
        if (!waitingDeal) return;
        dealReadySeats.Add(seat);
        Debug.Log($"[인게임] : {seat}번 좌석 배분 연출 완료 ({dealReadySeats.Count}명)");
        if (AllAliveDealt()) BeginFirstTurn();
    }

    // 신호가 안 오는 클라이언트(연출 오류 · 끊김 직전 등)가 있어도 게임이 멈추지 않게
    [Server]
    private IEnumerator DealWaitTimeout_co()
    {
        yield return new WaitForSeconds(GameFlowTiming.DealWaitTimeout);
        dealWait_co = null;
        if (!waitingDeal) yield break;

        Debug.Log("[인게임] : 배분 연출 대기 시간 초과 - 첫 턴 시작");
        BeginFirstTurn();
    }

    [Server]
    private bool AllAliveDealt()
    {
        foreach (PlayerState p in Players)
            if (p != null && !p.isDead && !dealReadySeats.Contains(p.SeatNum)) return false;
        return true;
    }

    [Server]
    private void BeginFirstTurn()
    {
        if (!waitingDeal) return;
        waitingDeal = false;
        if (dealWait_co != null) { StopCoroutine(dealWait_co); dealWait_co = null; }
        Debug.Log("[인게임] : 배분 연출이 모두 끝나 첫 턴 시작");

        accumTurnIndex = TurnPosition(nextRoundStartSeat);
        NextTurn();
    }

    // 턴 순서 = 테이블 시계 방향 (SeatOrder). accumTurnIndex는 이 순서 안의 위치
    private int[] turnOrder;
    private int[] TurnOrder
    {
        get
        {
            if (turnOrder == null || turnOrder.Length != Players.Length) turnOrder = SeatOrder.Clockwise(Players.Length);
            return turnOrder;
        }
    }

    private int TurnPosition(int seat)
    {
        int index = System.Array.IndexOf(TurnOrder, seat);
        return index >= 0 ? index : 0;
    }

    [Server]
    private void CancelDealWait()
    {
        waitingDeal = false;
        if (dealWait_co != null) { StopCoroutine(dealWait_co); dealWait_co = null; }
    }

    #endregion

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
            int seatIndex = TurnOrder[accumTurnIndex % maxPlayerCount];   // 좌석 번호가 아니라 테이블 시계 방향 순서
            PlayerState curPlayer = Players[seatIndex];

            accumTurnIndex++;

            if (curPlayer != null && curPlayer.isDead == false && !curPlayer.isEscaped)
            {
                curPlayer.isTurn = true;
                currentTurnPlayer = curPlayer;

                Debug.Log($"[인게임] : 다음 턴 {seatIndex}번 좌석");

                RpcTurnChanged(seatIndex);
                StopTurnTimer();
                turnTimer_co = StartCoroutine(TurnTimer_co(turnSerial, seatIndex));
                return;
            }
        }

        Debug.Log("[인게임] : 다음 턴을 받을 플레이어가 없습니다.");
    }

    // 턴 타이머는 한 번에 하나만. 턴이 바뀌거나 LIAR · 새 라운드 · 게임 종료 때 멈춘다.
    // (예전에는 턴 번호만 비교해서, 이전 라운드 타이머가 새 라운드 첫 턴에 터져 강제 제출되는 일이 있었음)
    private int turnSerial;
    private Coroutine turnTimer_co;

    [Server]
    private void StopTurnTimer()
    {
        turnSerial++;
        if (turnTimer_co != null)
        {
            StopCoroutine(turnTimer_co);
            turnTimer_co = null;
        }
    }

    [Server]
    private IEnumerator TurnTimer_co(int serial, int seatNum)
    {
        yield return new WaitForSeconds(GameFlowTiming.TurnTimer);
        if (serial != turnSerial) yield break;
        turnTimer_co = null;

        if (!isResolvingLiar && !waitingDeal && currentTurnPlayer != null && currentTurnPlayer == Players[seatNum])
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

        if (submitIndex == null || submitIndex.Length < 1 || submitIndex.Length > 3)
        {
            Debug_RpcLog("[인게임] : 요청 거절 (제출한 카드가 없거나, 범위 초과. 1장 이상 3장 이하로 제출 요함)");
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

        RpcSubmitCard(playerSeat, submitIndex.Length, submitIndex);
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


        StopTurnTimer();   // LIAR 연출 중에는 턴 타이머 정지
        StartCoroutine(ResolveLiarSequence_co(callerSeat, loserSeat));
    }

    [Server]
    private IEnumerator ResolveLiarSequence_co(int callerSeat, int loserSeat)
    {
        bool isBullet;
        RpcAnnounceLiar(callerSeat, loserSeat);
        yield return new WaitForSeconds(GameFlowTiming.RouletteAnimation);

        // 룰렛을 돌릴 사람이 연출 중에 나갔으면 룰렛 없이 다음 라운드 (남은 인원은 StartRound가 다시 셈)
        if (Players[loserSeat] == null)
        {
            nextRoundStartSeat = callerSeat;
            StartRound();
            yield break;
        }

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

    #endregion 

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
    public void RpcSubmitCard(int seatNum, int cardCount, int[] listIndices)
    {
        Debug.Log($"[인게임] : {seatNum}번 플레이어 카드 {cardCount}장 제출 완료");
        InGameEvent.TriggerOnCardsSubmitted(seatNum, cardCount, listIndices);
    }

    [ClientRpc]
    private void RpcTurnChanged(int seatIndex)
    {
        InGameEvent.TriggerOnTurnChanged(seatIndex);
    }

    [ClientRpc]
    private void RpcAnnounceLiar(int callerSeat, int seat)
    {
        // 해당 유저 총 드는 이벤트 or 연출
        InGameEvent.TriggerOnLiarCalled(callerSeat, seat);
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
        InGameEvent.TriggerOnPlayerSurvived(seat);
    }

    [ClientRpc]
    private void RpcAnnounceWinner(int winner)
    {
        Debug.Log($"{winner} 번 플레이어 승");
        InGameEvent.TriggerOnWinnerAnnounced(winner);
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
        CancelDealWait();
        StopTurnTimer();
        RpcAnnounceWinner(winner);
        RpcReEntryInLobby();
        GameOver?.Invoke(winner);
    }

    // 게임 중 나간 사람 처리 (LiarsNetworkManager.LeaveRoom → 여기). 남은 사람끼리 계속 진행
    [Server]
    public void HandleDisconnect(int seat)
    {
        if (seat < 0 || seat >= Players.Length || Players[seat] == null) return;

        bool wasTurn = currentTurnPlayer == Players[seat];
        Players[seat] = null;
        if (wasTurn) currentTurnPlayer = null;
        Debug.Log($"[인게임] : {seat}번 좌석 나감 - 남은 인원으로 진행");

        // 나간 사람이 마지막으로 낸 카드에는 LIAR를 걸 수 없으므로 다음 사람은 새로 내야 함
        if (lastSubmittedSeat == seat)
        {
            lastSubmittedSeat = -1;
            lastSubmittedcards = 0;
        }

        // 로딩 대기 중이거나 LIAR 연출 중이면, 끝난 뒤 StartRound에서 인원을 다시 셈
        if (waitingLoad || isResolvingLiar) return;

        int alive = 0;
        PlayerState survivor = null;
        foreach (PlayerState p in Players)
        {
            if (p == null || p.isDead) continue;
            alive++;
            survivor = p;
        }

        if (alive <= 1)
        {
            EndGame(survivor != null ? survivor.SeatNum : -1);   // 혼자 남으면 그 사람이 승리
            return;
        }

        // 배분 연출을 기다리는 중이면 나간 사람은 빼고 다시 확인
        if (waitingDeal)
        {
            if (AllAliveDealt()) BeginFirstTurn();
            return;
        }

        // 이번 라운드에서 카드를 낼 수 있는 사람이 1명 이하(나머지는 카드를 다 털어 탈출)면 라운드를 새로 시작
        int active = 0;
        PlayerState activePlayer = null;
        foreach (PlayerState p in Players)
        {
            if (p == null || p.isDead || p.isEscaped) continue;
            active++;
            activePlayer = p;
        }

        if (active <= 1)
        {
            nextRoundStartSeat = activePlayer != null ? activePlayer.SeatNum : survivor.SeatNum;
            StartRound();
            return;
        }

        if (wasTurn) NextTurn();   // 나간 사람 차례였으면 바로 다음 사람에게 (타이머를 기다리지 않음)
    }


}
