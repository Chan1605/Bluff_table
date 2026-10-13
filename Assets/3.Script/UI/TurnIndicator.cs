using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum SpinDirection
{
    Clockwise,
    CounterClockwise,
    Random
}

public class TurnIndicator : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] Transform[] seats;
    [SerializeField] GameObject visual;

    [Header("회전")]
    [SerializeField] SpinDirection defaultDirection = SpinDirection.Random;
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

    public void SetTurn(int seat) => SetTurn(seat, defaultDirection);

    public void SetTurn(int seat, SpinDirection dir)
    {
        if (!IsValidSeat(seat))
        {
            return;
        }
        if (!visual.activeSelf) 
        { 
            BeginRound(seat);
            return; 
        }   
        if (seat == Seat_cur) 
        { 
            return;
        }
        Seat_cur = seat;
        float delta = ComputeDelta(currentAngle, AngleToSeat(seat), dir);

        StopSpin();
        spin_co = StartCoroutine(SpinBy_co(delta));
    }

    public void Hide()
    {
        StopSpin();
        Seat_cur = -1;
        visual.SetActive(false);
    }

    private bool IsValidSeat(int seat) => seat >= 0 && seat < seats.Length && seats[seat] != null;

    private float AngleToSeat(int seat)
    {
        Vector3 d = seats[seat].position - transform.position;
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + angleOffset;
    }

    void ApplyAngle(float angle)
        => transform.rotation = Quaternion.AngleAxis(angle, Vector3.up) * baseRotation;

    void StopSpin()
    {
        if (spin_co != null) { StopCoroutine(spin_co); spin_co = null; }
    }

    static float ComputeDelta(float from, float to, SpinDirection dir)
    {
        if (dir == SpinDirection.Random)
            dir = Random.value < 0.5f ? SpinDirection.Clockwise : SpinDirection.CounterClockwise;

        float cw = Mathf.Repeat(to - from, 360f);
        float delta = dir switch
        {
            SpinDirection.Clockwise => cw,
            SpinDirection.CounterClockwise => cw == 0f ? 0f : cw - 360f,
            _ => Mathf.DeltaAngle(from, to),
        };

        return delta;
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
