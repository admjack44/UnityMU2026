using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityMU.Network.Core
{
    public sealed class TcpJsonClient
    {
        public event Action<string> OnMessageReceived;
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;

        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private StreamReader reader;
        private StreamWriter writer;
        private CancellationTokenSource cancellation;

        public bool IsConnected =>
            tcpClient != null &&
            tcpClient.Connected;

        public async Task ConnectAsync(string host, int port)
        {
            if (IsConnected)
                return;

            try
            {
                cancellation = new CancellationTokenSource();

                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(host, port);

                networkStream = tcpClient.GetStream();

                reader = new StreamReader(
                    networkStream,
                    Encoding.UTF8,
                    false,
                    1024,
                    true);

                writer = new StreamWriter(
                    networkStream,
                    Encoding.UTF8,
                    1024,
                    true)
                {
                    AutoFlush = true
                };

                OnConnected?.Invoke();

                _ = ReceiveLoopAsync(cancellation.Token);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
                await DisconnectAsync();
            }
        }

        public async Task SendAsync(string json)
        {
            if (!IsConnected || writer == null)
            {
                OnError?.Invoke("No conectado al servidor.");
                return;
            }

            try
            {
                await writer.WriteLineAsync(json);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
                await DisconnectAsync();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && IsConnected)
                {
                    string line = await reader.ReadLineAsync();

                    if (line == null)
                        break;

                    OnMessageReceived?.Invoke(line);
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    OnError?.Invoke(ex.Message);
            }

            await DisconnectAsync();
        }

        public async Task DisconnectAsync()
        {
            try
            {
                cancellation?.Cancel();

                writer?.Dispose();
                reader?.Dispose();
                networkStream?.Dispose();
                tcpClient?.Close();

                writer = null;
                reader = null;
                networkStream = null;
                tcpClient = null;

                OnDisconnected?.Invoke();

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TcpJsonClient] Error cerrando conexión: {ex.Message}");
            }
        }
    }
}