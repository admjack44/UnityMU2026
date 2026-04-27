namespace MUServer.Core.Network;

public enum MobileOpCode : ushort
{
    ServerListRequest = 100,
    ServerListResponse = 101,

    LoginRequest = 1000,
    LoginResponse = 1001,

    CharacterListRequest = 1100,
    CharacterListResponse = 1101,

    EnterWorldRequest = 1200,
    EnterWorldResponse = 1201,

    MoveRequest = 2000,
    MoveBroadcast = 2001,

    AttackRequest = 3000,
    DamageBroadcast = 3001,

    InventoryUpdate = 4000,

    Error = 9999
}