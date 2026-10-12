using UnityEngine;

namespace LiarsBar.Server
{
    // 서버 설정 에셋 (Create → LiarsBar → Server Config)
    [CreateAssetMenu(fileName = "ServerConfig", menuName = "LiarsBar/Server Config")]
    public class ServerConfig : ScriptableObject
    {
        [Header("Network")]
        [Tooltip("클라이언트가 접속할 주소. 로컬: localhost / 같은 공유기: 192.168.x.x / AWS: EC2 퍼블릭 IP")]
        [SerializeField] private string networkAddress = "localhost";

        [Tooltip("게임 서버 포트 (KCP, UDP)")]
        [SerializeField] private ushort port = 7777;

        [Header("Room")]
        [Tooltip("최대 인원 = 좌석 수. Mirror의 Max Connections에도 이 값이 들어간다")]
        [SerializeField, Range(1, 4)] private int maxSeats = 4;

        [Tooltip("게임 시작 최소 인원. 개발 중 2, 실제 4")]
        [SerializeField, Range(1, 4)] private int minPlayersToStart = 2;

        [Header("Database")]
        [Tooltip("끄면 DB 없이 실행 (개발용). 접속 정보는 Database/db.local.json 또는 환경변수")]
        [SerializeField] private bool useDatabase = false;

        [Header("Nickname")]
        [SerializeField] private int nicknameMinLength = 2;
        [SerializeField] private int nicknameMaxLength = 12;

        [Header("Password (Use Database 켜졌을 때만 사용)")]
        [SerializeField] private int passwordMinLength = 4;
        [SerializeField] private int passwordMaxLength = 32;

        public string NetworkAddress => networkAddress;
        public ushort Port => port;
        public int MaxSeats => maxSeats;
        public int MinPlayersToStart => minPlayersToStart;
        public int NicknameMinLength => nicknameMinLength;
        public int NicknameMaxLength => nicknameMaxLength;
        public bool UseDatabase => useDatabase;
        public int PasswordMinLength => passwordMinLength;
        public int PasswordMaxLength => passwordMaxLength;

        private void OnValidate()
        {
            if (minPlayersToStart > maxSeats) minPlayersToStart = maxSeats;
            if (nicknameMinLength < 1) nicknameMinLength = 1;
            if (nicknameMaxLength < nicknameMinLength) nicknameMaxLength = nicknameMinLength;
            if (passwordMinLength < 1) passwordMinLength = 1;
            if (passwordMaxLength < passwordMinLength) passwordMaxLength = passwordMinLength;
        }
    }
}
