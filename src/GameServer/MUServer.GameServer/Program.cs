using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MUServer.Core.Network;
using MUServer.Core.Repositories;
using MUServer.Core.Services;
using MUServer.Core.World;

namespace MUServer.GameServer;

internal sealed class ServerConfig
{
    public string ServerName { get; set; } = "UnityMU Mobile Dev";
    public string PublicIp { get; set; } = "127.0.0.1";
    public int GameServerPort { get; set; } = 55901;
    public int MaxPlayers { get; set; } = 500;

    public string ConnectionString { get; set; } =
        "Server=127.0.0.1;Port=3306;Database=unitymu;User=root;Password=;";
}

internal static class Program
{
    private static async Task Main()
    {
        ServerConfig config = LoadConfig();

        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        ILogger logger = loggerFactory.CreateLogger("GameServer");

        CharacterRepository characterRepository = new(config.ConnectionString);
        CharacterService characterService = new(characterRepository);

        BroadcastService broadcastService = new(
            loggerFactory.CreateLogger<BroadcastService>()
        );

        WorldManager worldManager = new(
            loggerFactory.CreateLogger<WorldManager>(),
            characterService
        );

        MonsterManager monsterManager = new(
            loggerFactory.CreateLogger<MonsterManager>()
        );

        // PRIMERO crear movementService
        MovementService movementService = new(
            worldManager,
            characterService,
            broadcastService,
            loggerFactory.CreateLogger<MovementService>()
        );

        movementService.Start();

        AutoCombatService autoCombatService = new(
            worldManager,
            monsterManager,
            movementService,
            characterService,
            loggerFactory.CreateLogger<AutoCombatService>()
        );

        autoCombatService.Start();

        MonsterAIService monsterAIService = new(
            monsterManager,
            worldManager,
            loggerFactory.CreateLogger<MonsterAIService>()
        );

        monsterAIService.Start();

        // DESPUÉS crear packetHandler
        MUPacketHandler packetHandler = new(
            logger,
            characterService,
            broadcastService,
            monsterManager,
            worldManager,
            movementService,
            autoCombatService
        );

        TcpListener listener = new(IPAddress.Any, config.GameServerPort);
        listener.Start();

        logger.LogInformation("==================================");
        logger.LogInformation(" GAME SERVER INICIADO");
        logger.LogInformation(" Nombre: {ServerName}", config.ServerName);
        logger.LogInformation(" IP Pública: {PublicIp}", config.PublicIp);
        logger.LogInformation(" Puerto: {Port}", config.GameServerPort);
        logger.LogInformation(" Máximo jugadores: {MaxPlayers}", config.MaxPlayers);
        logger.LogInformation("==================================");

        using CancellationTokenSource cts = new();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            listener.Stop();
        };

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(cts.Token);

                _ = Task.Run(
                    () => packetHandler.ProcessClientAsync(client),
                    cts.Token
                );
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("GameServer detenido correctamente.");
        }
        catch (SocketException)
        {
            logger.LogInformation("Socket cerrado. GameServer detenido.");
        }
        finally
        {
            listener.Stop();
        }
    }

    private static ServerConfig LoadConfig()
    {
        string currentDirPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "config",
            "server.json"
        );

        string repoRootPath = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "config",
                "server.json"
            )
        );

        string gameServerLocalPath = Path.Combine(
            AppContext.BaseDirectory,
            "config",
            "server.json"
        );

        string configPath =
            File.Exists(currentDirPath) ? currentDirPath :
            File.Exists(repoRootPath) ? repoRootPath :
            File.Exists(gameServerLocalPath) ? gameServerLocalPath :
            string.Empty;

        if (string.IsNullOrWhiteSpace(configPath))
        {
            Console.WriteLine("WARNING: No se encontró config/server.json. Usando configuración por defecto.");
            return new ServerConfig();
        }

        Console.WriteLine($"Config cargada desde: {configPath}");

        string json = File.ReadAllText(configPath);

        return JsonSerializer.Deserialize<ServerConfig>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        ) ?? new ServerConfig();
    }
}