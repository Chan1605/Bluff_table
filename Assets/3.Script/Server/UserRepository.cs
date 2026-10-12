using System;
using System.Threading.Tasks;
using MySqlConnector;
using UnityEngine;

namespace LiarsBar.Server
{
    public struct LoginOutcome
    {
        public bool Success;
        public string Message;
        public int UserId;
        public bool IsNewUser;

        public static LoginOutcome Fail(string message) => new LoginOutcome { Success = false, Message = message, UserId = -1 };
    }

    public readonly struct UserStats
    {
        public readonly int GamesPlayed;
        public readonly int Wins;

        public UserStats(int gamesPlayed, int wins)
        {
            GamesPlayed = gamesPlayed;
            Wins = wins;
        }
    }

    // users / user_stats SQL (서버 전용)
    public static class UserRepository
    {
        private const int DuplicateKeyError = 1062;   // UNIQUE 위반

        // 없으면 자동 가입, 있으면 비밀번호 확인
        public static async Task<LoginOutcome> LoginOrRegisterAsync(string nickname, string password)
        {
            try
            {
                using MySqlConnection connection = await Db.OpenAsync();

                int userId = -1;
                byte[] savedHash = null;
                byte[] savedSalt = null;

                using (MySqlCommand find = new MySqlCommand(
                    "SELECT user_id, password_hash, password_salt FROM users WHERE nickname = @nick", connection))
                {
                    find.Parameters.AddWithValue("@nick", nickname);
                    using MySqlDataReader reader = await find.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        userId = reader.GetInt32(0);
                        savedHash = (byte[])reader["password_hash"];
                        savedSalt = (byte[])reader["password_salt"];
                    }
                }

                // 기존 계정
                if (userId != -1)
                {
                    byte[] inputHash = await Task.Run(() => PasswordHasher.Hash(password, savedSalt));
                    if (!PasswordHasher.AreEqual(inputHash, savedHash))
                        return LoginOutcome.Fail("비밀번호가 올바르지 않습니다.");

                    using (MySqlCommand touch = new MySqlCommand(
                        "UPDATE users SET last_login_at = NOW() WHERE user_id = @id", connection))
                    {
                        touch.Parameters.AddWithValue("@id", userId);
                        await touch.ExecuteNonQueryAsync();
                    }

                    return new LoginOutcome { Success = true, Message = "로그인 성공", UserId = userId, IsNewUser = false };
                }

                // 자동 가입
                byte[] salt = PasswordHasher.CreateSalt();
                byte[] hash = await Task.Run(() => PasswordHasher.Hash(password, salt));

                using MySqlTransaction transaction = await connection.BeginTransactionAsync();

                using (MySqlCommand insertUser = new MySqlCommand(
                    "INSERT INTO users (nickname, password_hash, password_salt, last_login_at) VALUES (@nick, @hash, @salt, NOW())",
                    connection, transaction))
                {
                    insertUser.Parameters.AddWithValue("@nick", nickname);
                    insertUser.Parameters.AddWithValue("@hash", hash);
                    insertUser.Parameters.AddWithValue("@salt", salt);
                    await insertUser.ExecuteNonQueryAsync();
                    userId = (int)insertUser.LastInsertedId;
                }

                using (MySqlCommand insertStats = new MySqlCommand(
                    "INSERT INTO user_stats (user_id) VALUES (@id)", connection, transaction))
                {
                    insertStats.Parameters.AddWithValue("@id", userId);
                    await insertStats.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return new LoginOutcome { Success = true, Message = "새 계정으로 가입되었습니다.", UserId = userId, IsNewUser = true };
            }
            catch (MySqlException e) when (e.Number == DuplicateKeyError)
            {
                // 동시 가입 경쟁
                return LoginOutcome.Fail("방금 다른 사람이 가입한 닉네임입니다. 다시 시도해 주세요.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DB] 로그인 처리 오류: {e.Message}");
                return LoginOutcome.Fail("서버 오류로 로그인하지 못했습니다.");
            }
        }

        // 실패 시 null
        public static async Task<UserStats?> GetStatsAsync(int userId)
        {
            try
            {
                using MySqlConnection connection = await Db.OpenAsync();
                using MySqlCommand command = new MySqlCommand(
                    "SELECT games_played, wins FROM user_stats WHERE user_id = @id", connection);
                command.Parameters.AddWithValue("@id", userId);

                using MySqlDataReader reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return null;
                return new UserStats(reader.GetInt32(0), reader.GetInt32(1));
            }
            catch (Exception e)
            {
                Debug.LogError($"[DB] 전적 조회 오류: {e.Message}");
                return null;
            }
        }
    }
}
