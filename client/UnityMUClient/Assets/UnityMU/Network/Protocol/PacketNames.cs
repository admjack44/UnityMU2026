namespace UnityMU.Network.Protocol
{
    public static class PacketNames
    {
        public const string ServerListRequest = "ServerListRequest";
        public const string ServerListResponse = "ServerListResponse";

        public const string LoginRequest = "LoginRequest";
        public const string LoginResponse = "LoginResponse";

        public const string CharacterListRequest = "CharacterListRequest";
        public const string CharacterListResponse = "CharacterListResponse";

        public const string EnterWorldRequest = "EnterWorldRequest";
        public const string EnterWorldResponse = "EnterWorldResponse";

        public const string Ping = "Ping";
        public const string Pong = "Pong";
    }
}