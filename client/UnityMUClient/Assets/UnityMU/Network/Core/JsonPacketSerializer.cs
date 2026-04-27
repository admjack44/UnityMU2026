using UnityMU.Core.App;
using UnityMU.Network.Protocol;

namespace UnityMU.Network.Core
{
    public sealed class JsonPacketSerializer
    {
        private readonly AppConfig config;

        public JsonPacketSerializer(AppConfig config)
        {
            this.config = config;
        }

        public string Serialize<T>(string packetName, T payload)
        {
            var envelope = new PacketEnvelope
            {
                protocol = config.mobileProtocolId,
                packet = packetName,
                payload = UnityEngine.JsonUtility.ToJson(payload)
            };

            return UnityEngine.JsonUtility.ToJson(envelope);
        }

        public bool TryDeserializeEnvelope(string json, out PacketEnvelope envelope)
        {
            try
            {
                envelope = UnityEngine.JsonUtility.FromJson<PacketEnvelope>(json);

                return envelope != null &&
                       !string.IsNullOrWhiteSpace(envelope.packet);
            }
            catch
            {
                envelope = null;
                return false;
            }
        }

        public T DeserializePayload<T>(PacketEnvelope envelope)
        {
            return UnityEngine.JsonUtility.FromJson<T>(envelope.payload);
        }
    }
}