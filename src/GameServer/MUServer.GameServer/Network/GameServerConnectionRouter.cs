using System.Net.Sockets;
using MUServer.Core.Network;

namespace MUServer.GameServer.Network;

public sealed class GameServerConnectionRouter
{
    private readonly MUPacketHandler _legacyHandler;
    private readonly MobileGamePacketDispatcher _mobileDispatcher;

    public GameServerConnectionRouter(
        MUPacketHandler legacyHandler,
        MobileGamePacketDispatcher mobileDispatcher
    )
    {
        _legacyHandler = legacyHandler;
        _mobileDispatcher = mobileDispatcher;
    }

    public async Task ProcessClientAsync(TcpClient client)
    {
        var session = new MobileClientSession(client, _mobileDispatcher);
        await session.RunAsync();
    }
}