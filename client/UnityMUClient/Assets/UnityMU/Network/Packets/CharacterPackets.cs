using System;

namespace UnityMU.Network.Packets
{
    [Serializable]
    public sealed class CharacterDto
    {
        public int id;
        public string name;
        public string characterClass;
        public int level;
        public int mapId;
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public sealed class CharacterListRequest
    {
        public string sessionToken;
        public int accountId;
    }

    [Serializable]
    public sealed class CharacterListResponse
    {
        public bool success;
        public string message;
        public CharacterDto[] characters;
    }

    [Serializable]
    public sealed class EnterWorldRequest
    {
        public string sessionToken;
        public int characterId;
    }

    [Serializable]
    public sealed class EnterWorldResponse
    {
        public bool success;
        public string message;
        public CharacterDto character;
    }
}