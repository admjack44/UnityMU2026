using System;

namespace UnityMU.Network.Packets
{
    [Serializable]
    public sealed class LoginRequest
    {
        public string username;
        public string password;
        public string deviceId;
        public string clientVersion;
    }

    [Serializable]
    public sealed class LoginResponse
    {
        public bool success;
        public string message;
        public string sessionToken;
        public int accountId;
        public string accountName;
    }
}