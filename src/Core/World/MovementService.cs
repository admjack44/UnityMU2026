using Microsoft.Extensions.Logging;
using MUServer.Core.Services;

namespace MUServer.Core.World;

public sealed class MovementService
{
    private const int TickMilliseconds = 250;
    private const int SaveEveryTicks = 4;

    private readonly WorldManager _worldManager;
    private readonly CharacterService _characterService;
    private readonly BroadcastService _broadcastService;
    private readonly ILogger<MovementService> _logger;

    private readonly Dictionary<int, MoveTarget> _targets = new();
    private readonly object _lock = new();

    private CancellationTokenSource? _cts;

    public MovementService(
        WorldManager worldManager,
        CharacterService characterService,
        BroadcastService broadcastService,
        ILogger<MovementService> logger)
    {
        _worldManager = worldManager;
        _characterService = characterService;
        _broadcastService = broadcastService;
        _logger = logger;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => TickLoopAsync(_cts.Token));
        _logger.LogInformation("MovementService iniciado.");
    }

    public void Stop()
    {
        _cts?.Cancel();
        _logger.LogInformation("MovementService detenido.");
    }

    public void SetMoveTarget(int playerId, byte mapId, byte targetX, byte targetY)
    {
        var player = _worldManager.GetPlayer(playerId);
        if (player is null)
            return;

        if (player.CurrentMapId != mapId)
            return;

        if (player.IsDead || player.Character.CurrentLife <= 0)
            return;

        lock (_lock)
        {
            _targets[playerId] = new MoveTarget
            {
                PlayerId = playerId,
                MapId = mapId,
                TargetX = targetX,
                TargetY = targetY,
                SaveTickCounter = 0
            };
        }

        _logger.LogInformation(
            "MoveTarget asignado Player:{PlayerId} Map:{Map} Target:{X},{Y}",
            playerId,
            mapId,
            targetX,
            targetY);
    }

    private async Task TickLoopAsync(CancellationToken token)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(TickMilliseconds));

        while (await timer.WaitForNextTickAsync(token))
        {
            Tick();
        }
    }

    private void Tick()
    {
        List<int> completed = new();

        lock (_lock)
        {
            foreach (var pair in _targets)
            {
                int playerId = pair.Key;
                MoveTarget target = pair.Value;

                var player = _worldManager.GetPlayer(playerId);
                if (player is null)
                {
                    completed.Add(playerId);
                    continue;
                }

                if (player.IsDead || player.Character.CurrentLife <= 0)
                {
                    completed.Add(playerId);
                    continue;
                }

                if (player.X == target.TargetX && player.Y == target.TargetY)
                {
                    SavePosition(player);
                    completed.Add(playerId);
                    continue;
                }

                byte nextX = StepToward(player.X, target.TargetX);
                byte nextY = StepToward(player.Y, target.TargetY);

                bool moved = _worldManager.MovePlayer(playerId, nextX, nextY);
                if (!moved)
                {
                    completed.Add(playerId);
                    continue;
                }

                player.Character.MapId = player.CurrentMapId;
                player.Character.X = player.X;
                player.Character.Y = player.Y;

                SendAuthoritativeMove(player);

                var receivers = _worldManager.GetVisiblePlayers(player);
                _broadcastService.BroadcastPlayerMove(player, receivers);

                target.SaveTickCounter++;

                if (target.SaveTickCounter >= SaveEveryTicks)
                {
                    SavePosition(player);
                    target.SaveTickCounter = 0;
                }
            }

            foreach (int playerId in completed)
            {
                _targets.Remove(playerId);
            }
        }
    }

    private void SavePosition(Player player)
    {
        _characterService.UpdateCharacterPosition(
            player.Character.Id,
            player.CurrentMapId,
            player.X,
            player.Y);
    }

    private void SendAuthoritativeMove(Player player)
    {
        if (player.Stream is null || !player.Stream.CanWrite)
            return;

        byte[] packet =
        {
            0xC1, 0x06,
            0xD4, 0x00,
            player.X,
            player.Y
        };

        player.Stream.Write(packet, 0, packet.Length);
    }

    private static byte StepToward(byte current, byte target)
    {
        if (current < target)
            return (byte)(current + 1);

        if (current > target)
            return (byte)(current - 1);

        return current;
    }

    private sealed class MoveTarget
    {
        public int PlayerId { get; set; }
        public byte MapId { get; set; }
        public byte TargetX { get; set; }
        public byte TargetY { get; set; }
        public int SaveTickCounter { get; set; }
    }
}