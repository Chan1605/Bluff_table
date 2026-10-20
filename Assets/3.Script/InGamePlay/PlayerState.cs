using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using LiarsBar.Server;

public class PlayerState : NetworkBehaviour
{
    public static PlayerState Local { get; private set; }
    
    public int SeatNum;

    [SyncVar] public bool isDead;
    [SyncVar] public bool isTurn;
    [SyncVar] public bool isEscaped;

    [SyncVar] public int currentChamber;

    [SyncVar(hook =nameof(OnPitchChanged))] public float syncHeadPitch;
    [SyncVar(hook = nameof(OnYawChanged))] public float syncHeadYaw;


    public int bulletPosition;


    [SerializeField] public DeckGamePlayManager deckManager;
    public HeadController headController;

    public override void OnStartLocalPlayer()
    {
        Local = this;
        InGameEvent.OnCardsPlayed += SubmitCards;
        InGameEvent.OnPlayerEliminated += HandlePlayerEliminated;


    }

    public override void OnStopClient()
    {
        if (Local == this) Local = null;
        InGameEvent.OnCardsPlayed -= SubmitCards;
        InGameEvent.OnPlayerEliminated -= HandlePlayerEliminated;
    }

    [Server]
    public void ResetState()
    {
        isDead = false;
        isTurn = false;
        isEscaped = false;

        currentChamber = 0;
        bulletPosition = UnityEngine.Random.Range(0, 6);

    }

    [Server]
    public void RoundReset()
    {
        isEscaped = false;
    }

    [TargetRpc]
    public void TargetGiveCards(List<CardType> handCards, bool hideUI)
    {
        if (!isLocalPlayer) return;
        if (hideUI) return;

        Debug.Log("[클라이언트] 나의 패 도착");
        for (int i = 0; i < handCards.Count; i++)
        {
            Debug.Log($"손패 {i} 번째의 카드 : {handCards[i]}");
        }
        InGameEvent.TriggerOnCardDealt(handCards);
    }

    [Command]
    public void SubmitCards(int[] indexes)
    {
        deckManager.PlayerSubmitCard(SeatNum, indexes);
    }

    [Command]
    public void CallLiar()
    {
        deckManager.CallLiar(SeatNum);
    }

    // 스페이스(Look) → 패 펼치기 / 덮기를 모든 클라이언트에 알린다 (연출 전용, 게임 로직 영향 없음)
    [Command]
    public void CmdSetCardsOpen(bool open)
    {
        RpcCardsOpened(SeatNum, open);
    }

    [ClientRpc]
    private void RpcCardsOpened(int seat, bool open)
    {
        InGameEvent.TriggerOnCardsOpened(seat, open);
    }

    #region 플레이어 고개 움직임
    public void AssignHeadController(HeadController _headController)
    {
        headController = _headController;
        GetComponent<PlayerController>().headController = _headController;

        // 캐릭터 프리팹에 HeadController가 없으면 null로 온다. 여기서 예외가 나면 Mirror가 접속을 끊는다
        if (headController == null)
        {
            Debug.LogWarning($"[PlayerState] HeadController 없음 - 고개 · 1인칭 카메라 생략 (좌석 {SeatNum})", this);
            return;
        }

        // 남의 캐릭터 카메라는 꺼 둔다. 켜져 있으면 내 캐릭터에 카메라가 없을 때 남의 시점으로 화면이 넘어간다
        if (headController.firstPersonCamera != null)
        {
            headController.firstPersonCamera.enabled = isLocalPlayer;
            if (isLocalPlayer) headController.firstPersonCamera.Priority = 20;
        }
    }

    private void OnPitchChanged(float oldPitch, float newPitch)
    {
        UpdateVisualHead();
    }

    private void OnYawChanged(float oldYaw, float newYaw)
    {
        UpdateVisualHead();
    }

    private void UpdateVisualHead()
    {
        if (headController == null) return;
        if (isLocalPlayer) return;

        headController.SetAbsoluteRotation(syncHeadPitch, syncHeadYaw);
    }
    #endregion

    private void HandlePlayerEliminated(int eliminatedSeat)
    {

        if (!isLocalPlayer) return;

        if (!TryGetComponent<PlayerProfile>(out PlayerProfile pProfile)) return;

        if (eliminatedSeat != pProfile.Seat) return;

        StartCoroutine(TransitionToSpectator_co());

    }

    private IEnumerator TransitionToSpectator_co()
    {
        yield return new WaitForSeconds(GameFlowTiming.TransitionSpectator); // 처형 애니메이션 재생 시간

        if (!TryGetComponent<PlayerProfile>(out PlayerProfile pProfile) ||
            pProfile.Phase != RoomPhase.Playing)
            yield break;

        if (headController != null && headController.firstPersonCamera != null)
        {
            headController.firstPersonCamera.Priority = 0;
        }

        if (TryGetComponent(out PlayerSpectator spectator))
        {
            spectator.StartSpectating();
        }
    }
}
