using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace LiarsBar.Server
{
    // 클라 → 서버: 채팅 요청
    public struct ChatRequestMessage : NetworkMessage
    {
        public string Text;
    }

    // 서버 → 같은 방 클라: 채팅 전달
    public struct ChatMessage : NetworkMessage
    {
        public string Sender;
        public string Text;
    }

    // 방 채팅. 서버가 보낸 사람과 같은 방의 접속자에게만 전달한다.
    public static class RoomChat
    {
        public const int MaxLength = 100;
        private const float MinInterval = 0.3f;   // 도배 방지 (서버)

        private static readonly Dictionary<int, float> lastSent = new();

        // 클라 전용: (보낸 사람, 내용)
        public static event Action<string, string> Received;

        // ---- 서버 ----

        // LiarsNetworkManager.OnStartServer에서 한 번 호출
        public static void RegisterServer()
        {
            lastSent.Clear();
            NetworkServer.ReplaceHandler<ChatRequestMessage>(OnChatRequest);
        }

        private static void OnChatRequest(NetworkConnectionToClient conn, ChatRequestMessage message)
        {
            PlayerProfile sender = conn.identity != null ? conn.identity.GetComponent<PlayerProfile>() : null;
            if (sender == null || !sender.IsLoggedIn || !sender.IsInRoom) return;

            string text = Clean(message.Text);
            if (text.Length == 0) return;

            float now = Time.unscaledTime;
            if (lastSent.TryGetValue(conn.connectionId, out float last) && now - last < MinInterval) return;
            lastSent[conn.connectionId] = now;

            ChatMessage outgoing = new ChatMessage { Sender = sender.Nickname, Text = text };
            foreach (NetworkConnectionToClient other in NetworkServer.connections.Values)
            {
                if (other.identity == null) continue;

                PlayerProfile p = other.identity.GetComponent<PlayerProfile>();
                if (p != null && p.RoomId == sender.RoomId) other.Send(outgoing);
            }
        }

        // ---- 클라 ----

        // 채팅창이 여러 개(로비 · 인게임)일 수 있어서, 마지막 채팅창이 꺼질 때만 해제한다
        // (테이블 씬이 먼저 켜지고 로비 씬이 나중에 꺼져도 인게임 채팅이 끊기지 않게)
        private static int clientUsers;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            clientUsers = 0;
            Received = null;
        }

        public static void RegisterClient()
        {
            clientUsers++;
            NetworkClient.ReplaceHandler<ChatMessage>(OnChatMessage);
        }

        public static void UnregisterClient()
        {
            clientUsers = Mathf.Max(0, clientUsers - 1);
            if (clientUsers == 0) NetworkClient.UnregisterHandler<ChatMessage>();
        }

        public static void Send(string text)
        {
            string clean = Clean(text);
            if (clean.Length == 0 || !NetworkClient.isConnected) return;

            NetworkClient.Send(new ChatRequestMessage { Text = clean });
        }

        private static void OnChatMessage(ChatMessage message)
        {
            Received?.Invoke(message.Sender, message.Text);
        }

        private static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            string s = raw.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return s.Length > MaxLength ? s.Substring(0, MaxLength) : s;
        }
    }
}