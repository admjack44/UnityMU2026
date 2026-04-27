using System;

namespace UnityMU.Network.Packets
{
    [Serializable]
    public sealed class ServerInfoDto
    {
        public int id;
        public string name;
        public string host;
        public int port;
        public int onlineCount;
        public int maxPlayers;
        public string status;
    }
}