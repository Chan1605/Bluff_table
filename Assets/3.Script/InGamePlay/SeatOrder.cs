using System.Collections.Generic;
using LiarsBar.Server;
using UnityEngine;

// 테이블 좌석의 시계 방향 순서 (위에서 내려다본 기준).
// 좌석 번호(0~3)는 의자 배치와 무관하게 정해져 있어서, 턴 · 배분 순서는 이 순서를 따른다.
// 테이블 씬의 SeatAnchors(Table)가 있으면 의자 위치로 계산하고, 없으면(전용 서버 등) Zino_Table 배치 기준 기본값을 쓴다.
public static class SeatOrder
{
    // Zino_Table: 0 = 남, 3 = 서, 1 = 북, 2 = 동
    private static readonly int[] DefaultClockwise = { 0, 3, 1, 2 };

    public static int[] Clockwise(int seatCount)
    {
        var seats = new List<(int seat, float heading)>();
        Vector3 center = Vector3.zero;
        for (int i = 0; i < seatCount; i++)
        {
            Transform chair = SeatAnchors.Get(SeatArea.Table, i);
            if (chair == null) return Fallback(seatCount);
            center += chair.position;
            seats.Add((i, 0f));
        }
        center /= seatCount;

        for (int i = 0; i < seats.Count; i++)
        {
            Vector3 d = SeatAnchors.Get(SeatArea.Table, i).position - center;
            float heading = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;   // 북 0 → 동 90 → 남 180 (시계 방향으로 커짐)
            seats[i] = (i, heading);
        }

        // 0번 좌석부터 시계 방향
        float start = seats[0].heading;
        seats.Sort((a, b) => Mathf.Repeat(a.heading - start, 360f).CompareTo(Mathf.Repeat(b.heading - start, 360f)));

        var order = new int[seatCount];
        for (int i = 0; i < seatCount; i++) order[i] = seats[i].seat;
        return order;
    }

    private static int[] Fallback(int seatCount)
    {
        if (seatCount == DefaultClockwise.Length) return (int[])DefaultClockwise.Clone();

        var order = new int[seatCount];
        for (int i = 0; i < seatCount; i++) order[i] = i;
        return order;
    }
}
