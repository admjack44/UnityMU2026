using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using MUServer.Core.Configuration;
using MUServer.Core.Data;
using MUServer.Core.Network;
using MUServer.Core.Repositories;
using MUServer.Core.Services;
using MUServer.Core.World;

namespace MUServer.Core;

internal static class Program
{
    private const int Port = 55901;

    private static async Task Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var logger = loggerFactory.CreateLogger("MUServer");

        var dbSettings = new DatabaseSettings
        {
            Server = "localhost",
            Port = 3306,
            Database = "muonline",
            User = "root",
            Password = "MaxJhasper2024"
        };

        var dbInitializer = new DatabaseInitializer(dbSettings.ConnectionString, logger);
        dbInitializer.Initialize();

        var characterRepository = new CharacterRepository(dbSettings.ConnectionString);
        var characterService = new CharacterService(characterRepository);

        var visionService = new VisionService();

        var broadcastService = new BroadcastService(
            loggerFactory.CreateLogger<BroadcastService>());

        var monsterManager = new MonsterManager(logger);
        var worldManager = new WorldManager(logger, characterService);

        var monsterAiService = new MonsterAiService(
            monsterManager,
            worldManager,
            broadcastService,
            logger);

        var handler = new MUPacketHandler(
            logger,
            characterService,
            broadcastService,
            monsterManager,
            worldManager);

        var listener = new TcpListener(IPAddress.Any, Port);
        var cancellationTokenSource = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;

            logger.LogInformation("Cerrando servidor...");

            cancellationTokenSource.Cancel();
            listener.Stop();
        };

        try
        {
            listener.Start();

            logger.LogInformation("=== MU PEGASO SERVER INICIADO ===");
            logger.LogInformation("Escuchando en puerto {Port}", Port);
            logger.LogInformation("Monster AI iniciando...");
            logger.LogInformation("Esperando conexiones...");
            logger.LogInformation("Ctrl+C para salir");

            _ = monsterAiService.RunAsync(cancellationTokenSource.Token);

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationTokenSource.Token);
                _ = handler.ProcessClientAsync(client);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Servidor detenido.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fatal en listener.");
        }
        finally
        {
            listener.Stop();
        }
    }
}