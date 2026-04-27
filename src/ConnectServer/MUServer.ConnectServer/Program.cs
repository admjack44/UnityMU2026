using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MUServer.ConnectServer
{
    internal static class Program
    {
        private const int ConnectServerPort = 44405;
        private const byte MuClassicHeader = 0xC1;
        private const byte MobileHeader = 0xA1;
        private const int MobileHeaderSize = 5;

        public sealed class ServerInfo
        {
            public byte ServerCode { get; set; } = 0;
            public byte LoadPercentage { get; set; } = 0;
            public byte[] IP { get; set; } = new byte[4];
            public ushort Port { get; set; } = 55901;
            public byte Type { get; set; } = 0;

            public string IpText => string.Join(".", IP);
        }

        private enum MobileOpCode : ushort
        {
            ServerListRequest = 100,
            ServerListResponse = 101
        }

        private sealed class MobileServerDto
        {
            public byte Code { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Ip { get; set; } = string.Empty;
            public ushort Port { get; set; }
            public byte Load { get; set; }
            public byte Type { get; set; }
        }

        private enum PacketProtocol
        {
            MuClassic,
            Mobile
        }

        private readonly record struct ParsedPacket(PacketProtocol Protocol, byte[] Data);

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = false
        };

        private static async Task Main(string[] args)
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            ILogger logger = loggerFactory.CreateLogger(typeof(Program));

            ServerInfo[] serverList =
            {
                new ServerInfo
                {
                    ServerCode = 0,
                    LoadPercentage = 0,
                    IP = new byte[] { 127, 0, 0, 1 },
                    Port = 55901,
                    Type = 0
                }
            };

            using var cts = new CancellationTokenSource();
            var listener = new TcpListener(IPAddress.Any, ConnectServerPort);

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                logger.LogInformation("Cerrando ConnectServer...");
                cts.Cancel();
                listener.Stop();
            };

            listener.Start();

            logger.LogInformation("=== CONNECT SERVER INICIADO ===");
            logger.LogInformation("Escuchando en puerto {Port}", ConnectServerPort);
            logger.LogInformation("Protocolos activos: MU clasico 0xC1 + Unity Mobile 0xA1");
            logger.LogInformation("GameServer principal: {Ip}:{Port}", serverList[0].IpText, serverList[0].Port);

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync(cts.Token);
                    _ = Task.Run(() => HandleClientAsync(client, serverList, logger, cts.Token), cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("ConnectServer detenido correctamente.");
            }
            catch (ObjectDisposedException)
            {
                logger.LogInformation("ConnectServer detenido correctamente.");
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task HandleClientAsync(
            TcpClient client,
            ServerInfo[] servers,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            string endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            logger.LogInformation("Cliente preguntando servidores: {Endpoint}", endpoint);

            try
            {
                client.NoDelay = true;

                NetworkStream stream = client.GetStream();
                var pipe = new Pipe();

                Task readTask = FillPipeAsync(stream, pipe.Writer, logger, cancellationToken);
                Task processTask = ProcessPipeAsync(pipe.Reader, stream, servers, logger, cancellationToken);

                await Task.WhenAll(readTask, processTask);
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug("Conexion cancelada: {Endpoint}", endpoint);
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

        private static async Task FillPipeAsync(
            NetworkStream stream,
            PipeWriter writer,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Memory<byte> memory = writer.GetMemory(1024);
                    int bytesRead = await stream.ReadAsync(memory, cancellationToken);

                    if (bytesRead == 0)
                        break;

                    writer.Advance(bytesRead);

                    FlushResult result = await writer.FlushAsync(cancellationToken);
                    if (result.IsCompleted || result.IsCanceled)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // Cierre normal.
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Lectura de cliente finalizada.");
            }
            finally
            {
                await writer.CompleteAsync();
            }
        }

        private static async Task ProcessPipeAsync(
            PipeReader reader,
            NetworkStream stream,
            ServerInfo[] servers,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    ReadResult result = await reader.ReadAsync(cancellationToken);
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    while (TryParsePacket(ref buffer, out ParsedPacket packet))
                    {
                        await HandlePacketAsync(packet, stream, servers, logger, cancellationToken);
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);

                    if (result.IsCompleted || result.IsCanceled)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // Cierre normal.
            }
            finally
            {
                await reader.CompleteAsync();
            }
        }

        private static bool TryParsePacket(ref ReadOnlySequence<byte> buffer, out ParsedPacket packet)
        {
            packet = default;

            if (buffer.Length < 2)
                return false;

            byte header = buffer.Slice(0, 1).FirstSpan[0];

            if (header == MuClassicHeader)
                return TryParseMuClassicPacket(ref buffer, out packet);

            if (header == MobileHeader)
                return TryParseMobilePacket(ref buffer, out packet);

            buffer = buffer.Slice(1);
            return false;
        }

        private static bool TryParseMuClassicPacket(ref ReadOnlySequence<byte> buffer, out ParsedPacket packet)
        {
            packet = default;

            if (buffer.Length < 2)
                return false;

            byte length = buffer.Slice(1, 1).FirstSpan[0];

            if (length < 3)
            {
                buffer = buffer.Slice(1);
                return false;
            }

            if (buffer.Length < length)
                return false;

            byte[] data = buffer.Slice(0, length).ToArray();
            buffer = buffer.Slice(length);

            packet = new ParsedPacket(PacketProtocol.MuClassic, data);
            return true;
        }

        private static bool TryParseMobilePacket(ref ReadOnlySequence<byte> buffer, out ParsedPacket packet)
        {
            packet = default;

            if (buffer.Length < MobileHeaderSize)
                return false;

            byte[] header = buffer.Slice(0, MobileHeaderSize).ToArray();
            ushort length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(1, 2));

            if (length < MobileHeaderSize)
            {
                buffer = buffer.Slice(1);
                return false;
            }

            if (buffer.Length < length)
                return false;

            byte[] data = buffer.Slice(0, length).ToArray();
            buffer = buffer.Slice(length);

            packet = new ParsedPacket(PacketProtocol.Mobile, data);
            return true;
        }

        private static async Task HandlePacketAsync(
            ParsedPacket packet,
            NetworkStream stream,
            ServerInfo[] servers,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            switch (packet.Protocol)
            {
                case PacketProtocol.MuClassic:
                    await HandleMuClassicPacketAsync(packet.Data, stream, servers, logger, cancellationToken);
                    break;

                case PacketProtocol.Mobile:
                    await HandleMobilePacketAsync(packet.Data, stream, servers, logger, cancellationToken);
                    break;
            }
        }

        private static async Task HandleMuClassicPacketAsync(
            byte[] packet,
            NetworkStream stream,
            ServerInfo[] servers,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (packet.Length < 3)
                return;

            byte code = packet[2];
            logger.LogInformation("MU packet recibido - Code: {Code:X2}", code);

            switch (code)
            {
                case 0x00:
                    logger.LogInformation("-> Server list request MU clasico");
                    await SendServerListAsync(stream, servers, logger, cancellationToken);
                    break;

                default:
                    logger.LogDebug("-> Packet MU no manejado: {Code:X2}", code);
                    break;
            }
        }

        private static async Task HandleMobilePacketAsync(
            byte[] packet,
            NetworkStream stream,
            ServerInfo[] servers,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (packet.Length < MobileHeaderSize)
                return;

            ushort opCodeValue = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(3, 2));
            var opCode = (MobileOpCode)opCodeValue;

            logger.LogInformation("Mobile packet recibido - OpCode: {OpCode}", opCode);

            switch (opCode)
            {
                case MobileOpCode.ServerListRequest:
                    await SendMobileServerListAsync(stream, servers, logger, cancellationToken);
                    break;

                default:
                    logger.LogWarning("Mobile OpCode no manejado: {OpCodeValue}", opCodeValue);
                    break;
            }
        }

        private static async Task SendServerListAsync(
            NetworkStream stream,
            ServerInfo[] servers,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            int packetLength = 5 + servers.Length * 4;
            byte[] response = new byte[packetLength];

            response[0] = MuClassicHeader;
            response[1] = (byte)packetLength;
            response[2] = 0xF4;
            response[3] = 0x00;
            response[4] = (byte)servers.Length;

            int offset = 5;
            foreach (ServerInfo server in servers)
            {
                response[offset++] = server.ServerCode;
                response[offset++] = server.LoadPercentage;
                response[offset++] = (byte)(server.Port >> 8);
                response[offset++] = (byte)(server.Port & 0xFF);
            }

            await stream.WriteAsync(response, cancellationToken);
            logger.LogInformation("<- Server list MU enviada: {Count} servidor(es)", servers.Length);

            foreach (ServerInfo server in servers)
                await SendServerInfoAsync(stream, server, logger, cancellationToken);
        }

        private static async Task SendServerInfoAsync(
            NetworkStream stream,
            ServerInfo server,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            byte[] response = new byte[12];

            response[0] = MuClassicHeader;
            response[1] = 12;
            response[2] = 0xF4;
            response[3] = 0x03;
            response[4] = server.ServerCode;
            response[5] = server.IP[0];
            response[6] = server.IP[1];
            response[7] = server.IP[2];
            response[8] = server.IP[3];
            response[9] = (byte)(server.Port >> 8);
            response[10] = (byte)(server.Port & 0xFF);
            response[11] = server.Type;

            await stream.WriteAsync(response, cancellationToken);
            logger.LogInformation("<- Server info MU enviada: {Ip}:{Port}", server.IpText, server.Port);
        }

        private static async Task SendMobileServerListAsync(
            NetworkStream stream,
            ServerInfo[] servers,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var dto = new MobileServerDto[servers.Length];

            for (int i = 0; i < servers.Length; i++)
            {
                ServerInfo server = servers[i];
                dto[i] = new MobileServerDto
                {
                    Code = server.ServerCode,
                    Name = $"MU Mobile {server.ServerCode}",
                    Ip = server.IpText,
                    Port = server.Port,
                    Load = server.LoadPercentage,
                    Type = server.Type
                };
            }

            string json = JsonSerializer.Serialize(dto, JsonOptions);
            byte[] body = Encoding.UTF8.GetBytes(json);
            byte[] response = BuildMobilePacket(MobileOpCode.ServerListResponse, body);

            await stream.WriteAsync(response, cancellationToken);
            logger.LogInformation("<- Mobile server list enviada: {Json}", json);
        }

        private static byte[] BuildMobilePacket(MobileOpCode opCode, byte[] body)
        {
            int length = MobileHeaderSize + body.Length;

            if (length > ushort.MaxValue)
                throw new InvalidOperationException("Mobile packet demasiado grande.");

            byte[] packet = new byte[length];
            packet[0] = MobileHeader;
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(1, 2), (ushort)length);
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(3, 2), (ushort)opCode);

            if (body.Length > 0)
                Buffer.BlockCopy(body, 0, packet, MobileHeaderSize, body.Length);

            return packet;
        }
    }
}
