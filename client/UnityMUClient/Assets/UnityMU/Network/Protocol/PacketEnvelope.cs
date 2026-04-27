using System;

namespace UnityMU.Network.Protocol
{
    [Serializable]
    public sealed class PacketEnvelope
    {
        public byte protocol;
        public string packet;
        public string payload;
    }
}