using System;

namespace UnityMU.Network.Packets
{
    [Serializable]
    public sealed class ServerListRequest
    {
        public string clientVersion;
        public string platform;
    }

    [Serializable]
    public sealed class ServerListResponse
    {
        public bool success;
        public string message;
        public ServerInfoDto[] servers;
    }
}