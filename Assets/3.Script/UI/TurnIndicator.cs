using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LiarsBar.Server;


public class TurnIndicator : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] Transform[] seats;
    [SerializeField] GameObject visual;

    [Header("회전")]
    [SerializeField] float angleOffset = 0f;
    [SerializeField] float duration = 0.6f;
    [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Quaternion baseRotation;
    private float currentAngle;
    private int Seat_cur = -1;
    private Coroutine spin_co;

    private void Awake()
    {
        baseRotation = transform.rotation;
        currentAngle = 0f;
        visual.SetActive(false);
    }

    public void BeginRound(int startSeat)
    {
        if (!IsValidSeat(startSeat)) return;

        StopSpin();
        Seat_cur = startSeat;
        currentAngle = AngleToSeat(startSeat);
        ApplyAngle(currentAngle);
        visual.SetActive(true);
    }

    public void SetTurn(int seat)
    {
        if (!IsValidSeat(seat)) return;
        if (!visual.activeSelf) { BeginRound(seat); return; }
        if (seat == Seat_cur) return;

        Seat_cur = seat;
        float delta = Mathf.Repeat(AngleToSeat(seat) - currentAngle, 360f);   // 시계방향 각도

        StopSpin();
        spin_co = StartCoroutine(SpinBy_co(delta));
    }

    public void Hide()
    {
        StopSpin();
        Seat_cur = -1;
        visual.SetActive(false);
    }

    private bool IsValidSeat(int seat) => SeatTransform(seat) != null;

    // Inspector의 seats가 비어 있으면 테이블 의자(SeatAnchors)를 쓴다 (프리팹에서는 씬의 의자를 연결할 수 없어서)
    private Transform SeatTransform(int seat)
    {
        if (seat < 0) return null;
        if (seats != null && seat < seats.Length && seats[seat] != null) return seats[seat];
        return SeatAnchors.Get(SeatArea.Table, seat);
    }

    private float AngleToSeat(int seat)
    {
        Vector3 d = SeatTransform(seat).position - transform.position;
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + angleOffset;
    }

    void ApplyAngle(float angle)
        => transform.rotation = Quaternion.AngleAxis(angle, Vector3.up) * baseRotation;

    void StopSpin()
    {
        if (spin_co != null) { StopCoroutine(spin_co); spin_co = null; }
    }

    IEnumerator SpinBy_co(float delta)
    {
        float start = currentAngle;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            currentAngle = start + delta * ease.Evaluate(Mathf.Clamp01(t));
            ApplyAngle(currentAngle);
            yield return null;
        }
        spin_co = null;
    }
}
