using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiarsBar.Server
{
    // GameManager 대체용 테스트 구현
    public class DummyGameFlow : MonoBehaviour, IGameFlow
    {
        [SerializeField] private float autoFinishSeconds = 5f;

        public event Action<int> GameOver;

        private readonly List<int> remainingSeats = new();
        private Coroutine running;

        public void StartGame(int[] seats)
        {
            remainingSeats.Clear();
            remainingSeats.AddRange(seats);
            Debug.Log($"[Dummy] StartGame - 좌석 {string.Join(", ", seats)} / {autoFinishSeconds}초 뒤 종료");

            if (running != null) StopCoroutine(running);
            running = StartCoroutine(FinishAfterDelay());
        }

        public void HandleDisconnect(int seat)
        {
            remainingSeats.Remove(seat);
            Debug.Log($"[Dummy] HandleDisconnect - 좌석 {seat}, 남은 인원 {remainingSeats.Count}");

            // 1명 이하 남으면 종료
            if (remainingSeats.Count <= 1)
            {
                int winner = remainingSeats.Count == 1 ? remainingSeats[0] : -1;
                EndGame(winner, "남은 인원 부족");
            }
        }

        private IEnumerator FinishAfterDelay()
        {
            yield return new WaitForSeconds(autoFinishSeconds);
            running = null;

            int winner = remainingSeats.Count > 0 ? remainingSeats[0] : -1;
            EndGame(winner, "시간 종료");
        }

        private void EndGame(int winner, string reason)
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
            Debug.Log($"[Dummy] GameOver - 승자 좌석 {winner} ({reason})");
            GameOver?.Invoke(winner);
        }
    }
}
