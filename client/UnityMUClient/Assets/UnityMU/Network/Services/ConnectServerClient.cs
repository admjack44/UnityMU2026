using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityMU.Core.App;
using UnityMU.Network.Core;
using UnityMU.Network.Packets;
using UnityMU.Network.Protocol;

namespace UnityMU.Network.Services
{
    public sealed class ConnectServerClient
    {
        public event Action<ServerListResponse> OnServerListReceived;
        public event Action<string> OnError;

        private readonly AppConfig config;
        private readonly TcpJsonClient tcp;
        private readonly JsonPacketSerializer serializer;

        public ConnectServerClient(AppConfig config)
        {
            this.config = config;

            tcp = new TcpJsonClient();
            serializer = new JsonPacketSerializer(config);

            tcp.OnMessageReceived += HandleMessage;
            tcp.OnError += error => OnError?.Invoke(error);
        }

        public async Task ConnectAsync()
        {
            await tcp.ConnectAsync(
                config.connectServerHost,
                config.connectServerPort);
        }

        public async Task RequestServerListAsync()
        {
            if (!tcp.IsConnected)
                await ConnectAsync();

            var request = new ServerListRequest
            {
                clientVersion = Application.version,
                platform = Application.platform.ToString()
            };

            string json = serializer.Serialize(
                PacketNames.ServerListRequest,
                request);

            if (config.logNetworkPackets)
                Debug.Log($"[CS SEND] {json}");

            await tcp.SendAsync(json);
        }

        private void HandleMessage(string json)
        {
            if (config.logNetworkPackets)
                Debug.Log($"[CS RECV] {json}");

            if (!serializer.TryDeserializeEnvelope(json, out var envelope))
            {
                OnError?.Invoke("Paquete inválido recibido desde ConnectServer.");
                return;
            }

            switch (envelope.packet)
            {
                case PacketNames.ServerListResponse:
                    var response = serializer.DeserializePayload<ServerListResponse>(envelope);
                    OnServerListReceived?.Invoke(response);
                    break;

                default:
                    Debug.LogWarning($"[ConnectServerClient] Packet no manejado: {envelope.packet}");
                    break;
            }
        }

        public Task DisconnectAsync()
        {
            return tcp.DisconnectAsync();
        }
    }
}