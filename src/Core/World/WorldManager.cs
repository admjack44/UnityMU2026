using Microsoft.Extensions.Logging;
using MUServer.Core.Models;
using MUServer.Core.Services;

namespace MUServer.Core.World;

/// <summary>
/// Responsable de gestionar el estado del mundo:
/// - jugadores activos
/// - mapas
/// - movimiento
/// - entrada/salida del mundo
/// </summary>
public sealed class WorldManager
{
    private readonly ILogger _logger;
    private readonly CharacterService _characterService;

    private readonly Dictionary<byte, Map> _maps = new();
    private readonly Dictionary<int, Player> _players = new();
    private readonly Dictionary<int, WorldItem> _items = new();
    private readonly VisionService _visionService = new();

    private int _nextPlayerId = 1;
    private int _nextItemId = 1;

    public WorldManager(ILogger logger, CharacterService characterService)
    {
        _logger = logger;
        _characterService = characterService;

        InitializeMaps();
    }

    // =========================
    // 🌍 MAPAS
    // =========================
    private void InitializeMaps()
    {
        var lorencia = new Map(0, "Lorencia");
        _maps[lorencia.Id] = lorencia;

        _logger.LogInformation("Mapas inicializados: {Count}", _maps.Count);
    }

    public Map? GetMap(byte mapId)
    {
        return _maps.TryGetValue(mapId, out var map) ? map : null;
    }

    public IEnumerable<Player> GetVisiblePlayersFromMonster(Monster monster)
    {
        return _players.Values
            .Where(player =>
                player.IsOnline &&
                player.CurrentMapId == monster.MapId &&
                Math.Abs(player.X - monster.X) <= 10 &&
                Math.Abs(player.Y - monster.Y) <= 10);
    }

    // =========================
    // 👤 JUGADORES
    // =========================
    public Player EnterWorld(string accountName, Character character)
    {
        var player = new Player
        {
            PlayerId = _nextPlayerId++,
            AccountName = accountName,
            Character = character
        };

        player.SetPosition(character.MapId, character.X, character.Y, 0);

        _players[player.PlayerId] = player;

        _logger.LogInformation(
            "EnterWorld => Player:{Player} Id:{PlayerId} Map:{Map} Pos:{X},{Y}",
            character.Name,
            player.PlayerId,
            player.CurrentMapId,
            player.X,
            player.Y);

        return player;
    }

    public Player? GetPlayer(int playerId)
    {
        return _players.TryGetValue(playerId, out var player)
            ? player
            : null;
    }

    public IReadOnlyList<Player> GetPlayersInMap(byte mapId)
    {
        return _players.Values
            .Where(p => p.CurrentMapId == mapId && p.IsOnline)
            .ToList();
    }

    public IEnumerable<Player> GetOtherPlayersInMap(Player player)
    {
        return _players.Values
            .Where(p =>
                p.CurrentMapId == player.CurrentMapId &&
                p.IsOnline &&
                p.PlayerId != player.PlayerId);
    }

    public Player? GetNearestPlayer(byte mapId, byte x, byte y, int range)
    {
        return _players.Values
            .Where(player =>
                player.CurrentMapId == mapId &&
                player.Character.CurrentLife > 0 // ← aquí faltaba continuar bien
            )
            .OrderBy(player =>
                Math.Abs(player.X - x) + Math.Abs(player.Y - y))
            .FirstOrDefault(player =>
                Math.Abs(player.X - x) <= range &&
                Math.Abs(player.Y - y) <= range);
    }

    public WorldItem SpawnItem(byte mapId, byte x, byte y)
    {
        var item = new WorldItem
        {
            ItemId = _nextItemId++,
            ItemType = 1,
            MapId = mapId,
            X = x,
            Y = y
        };

        _items[item.ItemId] = item;

        _logger.LogInformation(
            "Item drop => ItemId:{ItemId} Map:{Map} Pos:{X},{Y}",
            item.ItemId,
            item.MapId,
            item.X,
            item.Y);

        return item;
    }

    public IEnumerable<Player> GetPlayersNearItem(WorldItem item)
    {
        return _players.Values
            .Where(p =>
                p.IsOnline &&
                p.CurrentMapId == item.MapId &&
                Math.Abs(p.X - item.X) <= 10 &&
                Math.Abs(p.Y - item.Y) <= 10);
    }

    public WorldItem? GetItem(int itemId)
    {
        return _items.TryGetValue(itemId, out var item)
            ? item
            : null;
    }

    public bool RemoveItem(int itemId)
    {
        return _items.Remove(itemId);
    }

    // =========================
    // 🚶 MOVIMIENTO
    // =========================
    public bool MovePlayer(int playerId, byte newX, byte newY)
    {
        if (!_players.TryGetValue(playerId, out var player))
        {
            return false;
        }

        if (!player.IsAlive)
        {
            _logger.LogWarning("Move bloqueado: player muerto. Id={PlayerId}", playerId);
            return false;
        }

        var map = GetMap(player.CurrentMapId);
        if (map is null)
        {
            return false;
        }

        if (!map.CanWalk(newX, newY))
        {
            _logger.LogWarning(
                "Move bloqueado => Player:{Player} Target:{X},{Y}",
                player.Character.Name,
                newX,
                newY);

            return false;
        }

        player.MoveTo(newX, newY);

        // sincronizar con el Character (persistencia)
        player.Character.X = newX;
        player.Character.Y = newY;

        _logger.LogInformation(
            "Move OK => Player:{Player} Pos:{X},{Y}",
            player.Character.Name,
            newX,
            newY);

        return true;
    }

    // =========================
    // 🔌 DESCONEXIÓN
    // =========================
    public void DisconnectPlayer(int playerId)
    {
        if (!_players.TryGetValue(playerId, out var player))
        {
            return;
        }

        player.Disconnect();

        _characterService.UpdateCharacterPosition(
            player.Character.Id,
            player.CurrentMapId,
            player.X,
            player.Y);

        _players.Remove(playerId);

        _logger.LogInformation(
            "Disconnect => Player:{Player} Id:{PlayerId}",
            player.Character.Name,
            player.PlayerId);
    }

    public IReadOnlyList<Player> GetVisiblePlayers(Player viewer)
    {
        return _visionService.GetVisiblePlayers(viewer, _players.Values);
    }

    public IReadOnlyList<Monster> GetVisibleMonsters(Player viewer, IEnumerable<Monster> monsters)
    {
        return _visionService.GetVisibleMonsters(viewer, monsters);
    }
}
