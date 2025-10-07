using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Server.Models;

namespace Server.Logic
{
    public class ServerLogic
    {
        private TcpListener? tcpListener;
        private Thread? listenerThread;
        private bool isRunning = false;
        private List<ClientInfo> connectedClients = new List<ClientInfo>();
        private object clientsLock = new object();
        
        public event Action<string, Color>? OnLogMessage;
        public event Action<string>? OnClientAdded;
        public event Action<string>? OnClientRemoved;
        public event Action<int>? OnClientCountChanged;

        public void StartServer()
        {
            try
            {
                isRunning = true;
                tcpListener = new TcpListener(IPAddress.Any, 53);
                tcpListener.Start();

                listenerThread = new Thread(ListenForClients);
                listenerThread.IsBackground = true;
                listenerThread.Start();

                OnLogMessage?.Invoke("Server started on port 53 (DNS Tunnel Mode)", Color.Cyan);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to start server: {ex.Message}\n\nNote: Port 53 requires Administrator privileges.");
            }
        }

        public void StopServer()
        {
            isRunning = false;
            tcpListener?.Stop();
            OnLogMessage?.Invoke("Server stopped", Color.Yellow);
        }

        private void ListenForClients()
        {
            while (isRunning)
            {
                try
                {
                    if (tcpListener!.Pending())
                    {
                        TcpClient client = tcpListener.AcceptTcpClient();
                        Thread clientThread = new Thread(() => HandleClient(client));
                        clientThread.IsBackground = true;
                        clientThread.Start();
                    }
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    if (isRunning)
                    {
                        OnLogMessage?.Invoke($"Error accepting client: {ex.Message}", Color.Red);
                    }
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            string clientId = "";
            try
            {
                NetworkStream stream = client.GetStream();
                OnLogMessage?.Invoke($"New client connected: {client.Client.RemoteEndPoint}", Color.Yellow);

                while (isRunning && client.Connected)
                {
                    if (stream.DataAvailable)
                    {
                        byte[] lengthBuffer = new byte[4];
                        int bytesRead = stream.Read(lengthBuffer, 0, 4);
                        if (bytesRead != 4) break;

                        int length = BitConverter.ToInt32(lengthBuffer, 0);
                        if (length <= 0 || length > 1048576) break;

                        byte[] dataBuffer = new byte[length];
                        int totalRead = 0;
                        while (totalRead < length)
                        {
                            bytesRead = stream.Read(dataBuffer, totalRead, length - totalRead);
                            if (bytesRead == 0) break;
                            totalRead += bytesRead;
                        }

                        if (totalRead == length)
                        {
                            string jsonData = Encoding.UTF8.GetString(dataBuffer);
                            ProcessReceivedData(jsonData, ref clientId);
                        }
                    }
                    Thread.Sleep(50);
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Client error: {ex.Message}", Color.Red);
            }
            finally
            {
                client.Close();
                RemoveClient(clientId);
                OnLogMessage?.Invoke($"Client disconnected: {clientId}", Color.Yellow);
            }
        }

        private void ProcessReceivedData(string jsonData, ref string clientId)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonData);
                JsonElement root = doc.RootElement;

                string botId = root.GetProperty("bot_id").GetString() ?? "Unknown";
                string timestamp = root.GetProperty("timestamp").GetString() ?? "";
                string messageType = root.GetProperty("message_type").GetString() ?? "";

                if (string.IsNullOrEmpty(clientId))
                {
                    clientId = botId;
                    AddClient(botId);
                }

                JsonElement payload = root.GetProperty("payload");
                string payloadType = payload.GetProperty("type").GetString() ?? "";
                string content = payload.GetProperty("content").GetString() ?? "";

                string formattedJson = $"[{timestamp}] {botId} - {messageType}\n" +
                                     $"Type: {payloadType}\n" +
                                     $"Content: {content}\n" +
                                     $"Raw JSON: {jsonData}\n" +
                                     new string('-', 80) + "\n";

                Color color = messageType switch
                {
                    "DATA_REPORT" => Color.LimeGreen,
                    "HEARTBEAT" => Color.Cyan,
                    _ => Color.White
                };

                OnLogMessage?.Invoke(formattedJson, color);
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Error parsing JSON: {ex.Message}\nData: {jsonData}", Color.Red);
            }
        }

        private void AddClient(string clientId)
        {
            lock (clientsLock)
            {
                if (!connectedClients.Any(c => c.BotId == clientId))
                {
                    connectedClients.Add(new ClientInfo
                    {
                        BotId = clientId,
                        ConnectedAt = DateTime.Now
                    });

                    OnClientAdded?.Invoke($"{clientId} - {DateTime.Now:HH:mm:ss}");
                    OnClientCountChanged?.Invoke(connectedClients.Count);
                }
            }
        }

        private void RemoveClient(string clientId)
        {
            lock (clientsLock)
            {
                connectedClients.RemoveAll(c => c.BotId == clientId);
                OnClientRemoved?.Invoke(clientId);
                OnClientCountChanged?.Invoke(connectedClients.Count);
            }
        }
    }
}
