using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityMU.Core.App;
using UnityMU.Network.Core;
using UnityMU.Network.Packets;
using UnityMU.Network.Protocol;

namespace UnityMU.Network.Services
{
    public sealed class GameServerClient
    {
        public event Action<LoginResponse> OnLoginReceived;
        public event Action<CharacterListResponse> OnCharacterListReceived;
        public event Action<EnterWorldResponse> OnEnterWorldReceived;
        public event Action<string> OnError;

        private readonly AppConfig config;
        private readonly TcpJsonClient tcp;
        private readonly JsonPacketSerializer serializer;

        public bool IsConnected => tcp.IsConnected;

        public GameServerClient(AppConfig config)
        {
            this.config = config;
            tcp = new TcpJsonClient();
            serializer = new JsonPacketSerializer(config);

            tcp.OnMessageReceived += HandleMessage;
            tcp.OnError += error => OnError?.Invoke(error);
        }

        public async Task ConnectAsync(string host, int port)
        {
            await tcp.ConnectAsync(host, port);
        }

        public async Task LoginAsync(string username, string password)
        {
            if (!tcp.IsConnected)
                await tcp.ConnectAsync(config.gameServerHost, config.gameServerPort);

            var request = new LoginRequest
            {
                username = username,
                password = password,
                deviceId = SystemInfo.deviceUniqueIdentifier,
                clientVersion = Application.version
            };

            string json = serializer.Serialize(PacketNames.LoginRequest, request);
            Debug.Log($"[GS SEND] {json}");
            await tcp.SendAsync(json);
        }

        public async Task RequestCharacterListAsync()
        {
            var request = new CharacterListRequest
            {
                sessionToken = SessionState.Current.SessionToken,
                accountId = SessionState.Current.AccountId
            };

            string json = serializer.Serialize(PacketNames.CharacterListRequest, request);
            Debug.Log($"[GS SEND] {json}");
            await tcp.SendAsync(json);
        }

        public async Task EnterWorldAsync(int characterId)
        {
            var request = new EnterWorldRequest
            {
                sessionToken = SessionState.Current.SessionToken,
                characterId = characterId
            };

            string json = serializer.Serialize(PacketNames.EnterWorldRequest, request);
            Debug.Log($"[GS SEND] {json}");
            await tcp.SendAsync(json);
        }

        private void HandleMessage(string json)
        {
            Debug.Log($"[GS RECV] {json}");

            if (!serializer.TryDeserializeEnvelope(json, out PacketEnvelope envelope))
            {
                OnError?.Invoke("Paquete inválido desde GameServer.");
                return;
            }

            switch (envelope.packet)
            {
                case PacketNames.LoginResponse:
                    OnLoginReceived?.Invoke(serializer.DeserializePayload<LoginResponse>(envelope));
                    break;

                case PacketNames.CharacterListResponse:
                    OnCharacterListReceived?.Invoke(serializer.DeserializePayload<CharacterListResponse>(envelope));
                    break;

                case PacketNames.EnterWorldResponse:
                    OnEnterWorldReceived?.Invoke(serializer.DeserializePayload<EnterWorldResponse>(envelope));
                    break;

                default:
                    Debug.LogWarning($"[GS] Packet no manejado: {envelope.packet}");
                    break;
            }
        }

        public Task DisconnectAsync()
        {
            return tcp.DisconnectAsync();
        }
    }
}