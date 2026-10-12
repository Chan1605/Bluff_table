using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;
using UnityEngine;

namespace LiarsBar.Server
{
    // 게임 시작 시점 참가자
    public readonly struct MatchParticipant
    {
        public readonly int Seat;
        public readonly int UserId;

        public MatchParticipant(int seat, int userId)
        {
            Seat = seat;
            UserId = userId;
        }
    }

    // matches / match_players / user_stats SQL (서버 전용)
    public static class MatchRepository
    {
        // 성공 시 match_id, 실패 시 -1
        public static async Task<int> SaveMatchAsync(DateTime startedAt, DateTime endedAt,
                                                     IReadOnlyList<MatchParticipant> participants, int winnerUserId)
        {
            try
            {
                using MySqlConnection connection = await Db.OpenAsync();
                using MySqlTransaction transaction = await connection.BeginTransactionAsync();

                int matchId;
                using (MySqlCommand insertMatch = new MySqlCommand(
                    "INSERT INTO matches (started_at, ended_at, winner_user_id) VALUES (@start, @end, @winner)",
                    connection, transaction))
                {
                    insertMatch.Parameters.AddWithValue("@start", startedAt);
                    insertMatch.Parameters.AddWithValue("@end", endedAt);
                    // 승자 없음 → NULL
                    insertMatch.Parameters.AddWithValue("@winner", winnerUserId > 0 ? (object)winnerUserId : DBNull.Value);
                    await insertMatch.ExecuteNonQueryAsync();
                    matchId = (int)insertMatch.LastInsertedId;
                }

                foreach (MatchParticipant participant in participants)
                {
                    using (MySqlCommand insertPlayer = new MySqlCommand(
                        "INSERT INTO match_players (match_id, user_id, seat) VALUES (@match, @user, @seat)",
                        connection, transaction))
                    {
                        insertPlayer.Parameters.AddWithValue("@match", matchId);
                        insertPlayer.Parameters.AddWithValue("@user", participant.UserId);
                        insertPlayer.Parameters.AddWithValue("@seat", participant.Seat);
                        await insertPlayer.ExecuteNonQueryAsync();
                    }

                    // DB 안에서 +1
                    using (MySqlCommand updateStats = new MySqlCommand(
                        "UPDATE user_stats SET games_played = games_played + 1, wins = wins + @win WHERE user_id = @user",
                        connection, transaction))
                    {
                        updateStats.Parameters.AddWithValue("@win", participant.UserId == winnerUserId ? 1 : 0);
                        updateStats.Parameters.AddWithValue("@user", participant.UserId);
                        await updateStats.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();
                return matchId;
            }
            catch (Exception e)
            {
                // Commit 전 예외 → 자동 롤백
                Debug.LogError($"[DB] 전적 저장 실패: {e.Message}");
                return -1;
            }
        }
    }
}
