using System;
using System.Threading.Tasks;
using MySqlConnector;
using UnityEngine;

namespace LiarsBar.Server
{
    // 서버 전용 DB 입구
    public static class Db
    {
        private static string connectionString;

        public static bool IsReady => connectionString != null;

        // PingAsync 성공 여부
        public static bool IsAvailable { get; private set; }

        public static bool Initialize()
        {
            IsAvailable = false;
            if (!DbSettings.TryLoad(out DbSettings settings, out string source))
            {
                connectionString = null;
                return false;
            }

            MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
            {
                Server = settings.host,
                Port = (uint)settings.port,
                Database = settings.database,
                UserID = settings.user,
                Password = settings.password,
                ConnectionTimeout = 5,
            };
            connectionString = builder.ConnectionString;

            // 비밀번호는 로그 금지
            Debug.Log($"[DB] 접속 정보 로드 ({source}) - {settings.user}@{settings.host}:{settings.port}/{settings.database}");
            return true;
        }

        // using으로 닫을 것 (풀 반납)
        public static async Task<MySqlConnection> OpenAsync()
        {
            MySqlConnection connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            return connection;
        }

        public static async Task<bool> PingAsync()
        {
            try
            {
                using MySqlConnection connection = await OpenAsync();
                using MySqlCommand command = new MySqlCommand("SELECT COUNT(*) FROM users", connection);
                object count = await command.ExecuteScalarAsync();

                Debug.Log($"[DB] 접속 성공 - MariaDB {connection.ServerVersion}, users {count}명");
                IsAvailable = true;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DB] 접속 실패: {e.Message}");
                IsAvailable = false;
                return false;
            }
        }
    }
}
