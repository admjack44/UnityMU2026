using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MUServer.ConnectServer
{
    class Program
    {
        // Estructura de un servidor en la lista MU
        public class ServerInfo
        {
            public byte ServerCode { get; set; } = 0;  // ID del servidor
            public byte LoadPercentage { get; set; } = 0;  // 0 = vacío, 100 = lleno
            public byte[] IP { get; set; } = new byte[4];  // IP del GameServer
            public ushort Port { get; set; } = 55901;  // Puerto del GameServer
            public byte Type { get; set; } = 0;  // 0 = normal, 1 = PVP, etc.
        }

        static async Task Main(string[] args)
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            var logger = loggerFactory.CreateLogger<Program>();

            int port = 44405;  // Puerto estándar del ConnectServer MU
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            logger.LogInformation("=== CONNECT SERVER INICIADO ===");
            logger.LogInformation("Escuchando en puerto {Port}", port);
            logger.LogInformation("Sirviendo lista de servidores MU...");

            // Configurar nuestro GameServer
            var serverList = new ServerInfo[]
            {
                new ServerInfo
                {
                    ServerCode = 0,  // Servidor #0
                    LoadPercentage = 0,  // 0% carga (vacío)
                    IP = new byte[] { 127, 0, 0, 1 },  // localhost
                    Port = 55901,  // Puerto de nuestro GameServer
                    Type = 0  // Servidor normal
                }
            };

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                logger.LogInformation("Cerrando ConnectServer...");
                cts.Cancel();
                listener.Stop();
            };

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(cts.Token);
                    _ = HandleClientAsync(client, serverList, logger);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("ConnectServer detenido.");
            }
        }

        static async Task HandleClientAsync(TcpClient client, ServerInfo[] servers, ILogger logger)
        {
            var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            logger.LogInformation("Cliente preguntando servidores: {Endpoint}", endpoint);

            try
            {
                var stream = client.GetStream();
                var pipe = new Pipe();

                var readTask = FillPipeAsync(stream, pipe.Writer);
                var processTask = ProcessPipeAsync(pipe.Reader, stream, servers, logger);

                await Task.WhenAll(readTask, processTask);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error con cliente {Endpoint}", endpoint);
            }
            finally
            {
                client.Close();
                logger.LogInformation("Cliente desconectado: {Endpoint}", endpoint);
            }
        }

        static async Task FillPipeAsync(NetworkStream stream, PipeWriter writer)
        {
            while (true)
            {
                var memory = writer.GetMemory(1024);
                try
                {
                    int bytesRead = await stream.ReadAsync(memory);
                    if (bytesRead == 0) break;
                    writer.Advance(bytesRead);
                    var result = await writer.FlushAsync();
                    if (result.IsCompleted) break;
                }
                catch { break; }
            }
            await writer.CompleteAsync();
        }

        static async Task ProcessPipeAsync(PipeReader reader, NetworkStream stream, ServerInfo[] servers, ILogger logger)
        {
            while (true)
            {
                var result = await reader.ReadAsync();
                var buffer = result.Buffer;

                if (TryParsePacket(ref buffer, out var packet))
                {
                    HandlePacket(packet, stream, servers, logger);
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted) break;
            }
            await reader.CompleteAsync();
        }

        static bool TryParsePacket(ref ReadOnlySequence<byte> buffer, out byte[] packet)
        {
            packet = Array.Empty<byte>();
            if (buffer.Length < 2) return false;

            var reader = new SequenceReader<byte>(buffer);
            if (!reader.TryRead(out byte header)) return false;
            if (header != 0xC1) return false;

            if (!reader.TryRead(out byte length)) return false;
            if (buffer.Length < length) return false;

            packet = buffer.Slice(0, length).ToArray();
            buffer = buffer.Slice(length);
            return true;
        }

        static void HandlePacket(byte[] packet, NetworkStream stream, ServerInfo[] servers, ILogger logger)
        {
            if (packet.Length < 3) return;
            byte code = packet[2];

            logger.LogInformation("Packet recibido - Code: {Code:X2}", code);

            switch (code)
            {
                case 0x00: // Petición de lista de servidores
                    logger.LogInformation("→ Server list request");
                    SendServerList(stream, servers, logger);
                    break;

                default:
                    logger.LogDebug("→ Packet no manejado: {Code:X2}", code);
                    break;
            }
        }

        static void SendServerList(NetworkStream stream, ServerInfo[] servers, ILogger logger)
        {
            // Protocolo MU ConnectServer:
            // C1 [length] F4 00 [count] [server_data...]

            int packetLength = 5 + (servers.Length * 4);  // Header + count + servers

            var response = new byte[packetLength];
            response[0] = 0xC1;           // Header
            response[1] = (byte)packetLength;  // Length
            response[2] = 0xF4;           // Code ConnectServer
            response[3] = 0x00;           // Sub-code (server list)
            response[4] = (byte)servers.Length;  // Cantidad de servidores

            int offset = 5;
            foreach (var server in servers)
            {
                response[offset++] = server.ServerCode;
                response[offset++] = server.LoadPercentage;
                response[offset++] = (byte)(server.Port >> 8);   // Port high byte
                response[offset++] = (byte)(server.Port & 0xFF); // Port low byte
            }

            stream.Write(response);
            logger.LogInformation("← Server list enviada: {Count} servidor(es)", servers.Length);

            // También enviar info detallada del servidor (IP, etc.)
            foreach (var server in servers)
            {
                SendServerInfo(stream, server, logger);
            }
        }

        static void SendServerInfo(NetworkStream stream, ServerInfo server, ILogger logger)
        {
            // C1 [length] F4 03 [server_code] [ip...] [port]
            var response = new byte[10];
            response[0] = 0xC1;
            response[1] = 10;  // Length
            response[2] = 0xF4;
            response[3] = 0x03;  // Server info

            response[4] = server.ServerCode;
            response[5] = server.IP[0];
            response[6] = server.IP[1];
            response[7] = server.IP[2];
            response[8] = server.IP[3];
            response[9] = (byte)(server.Type);

            stream.Write(response);
            logger.LogInformation("← Server info enviada: {IP}:{Port}",
                string.Join(".", server.IP), server.Port);
        }
    }
}