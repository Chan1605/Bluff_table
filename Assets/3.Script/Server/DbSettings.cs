using System;
using System.IO;
using UnityEngine;

namespace LiarsBar.Server
{
    // DB 접속 정보: 환경변수(AWS) → Database/db.local.json(로컬) 순
    [Serializable]
    public class DbSettings
    {
        public string host = "127.0.0.1";
        public int port = 3306;
        public string database = "liarsbar";
        public string user = "liarsbar_server";
        public string password = "";

        // 에디터: 프로젝트/Database, 빌드: 실행파일 옆 Database
        private const string LocalFileRelativePath = "../Database/db.local.json";

        public static bool TryLoad(out DbSettings settings, out string source)
        {
            string envPassword = Environment.GetEnvironmentVariable("LIARSBAR_DB_PASS");
            if (!string.IsNullOrEmpty(envPassword))
            {
                settings = new DbSettings();
                settings.host = Environment.GetEnvironmentVariable("LIARSBAR_DB_HOST") ?? settings.host;
                settings.database = Environment.GetEnvironmentVariable("LIARSBAR_DB_NAME") ?? settings.database;
                settings.user = Environment.GetEnvironmentVariable("LIARSBAR_DB_USER") ?? settings.user;
                settings.password = envPassword;
                if (int.TryParse(Environment.GetEnvironmentVariable("LIARSBAR_DB_PORT"), out int envPort))
                    settings.port = envPort;

                source = "환경변수";
                return true;
            }

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, LocalFileRelativePath));
            if (File.Exists(path))
            {
                settings = JsonUtility.FromJson<DbSettings>(File.ReadAllText(path));
                if (settings != null && !string.IsNullOrEmpty(settings.password))
                {
                    source = path;
                    return true;
                }
                Debug.LogWarning($"[DB] {path} 에 password가 비어 있습니다.");
            }
            else
            {
                Debug.LogWarning($"[DB] 접속 정보 파일이 없습니다: {path}");
            }

            settings = null;
            source = null;
            return false;
        }
    }
}
