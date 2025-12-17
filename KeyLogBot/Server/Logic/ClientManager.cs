using Server.Models;

namespace Server.Logic
{

    public class ClientManager
    {
        private readonly List<DataParser> _dataParsers;
        private readonly string _logPath;

        public event Action<ClientInfo>? OnClientAdded;
        public event Action<int>? OnClientCountChanged;

        public ClientManager(string logPath)
        {
            _dataParsers = new List<DataParser>();
            _logPath = logPath;
        }

        public int ClientCount => _dataParsers.Count;


        public int AddClient(string clientIp)
        {
            _dataParsers.Add(new DataParser(clientIp, _logPath));
            int connectionId = _dataParsers.Count;

            var clientInfo = new ClientInfo
            {
                ConnectionId = connectionId,
                IpAddress = clientIp,
                ConnectedAt = DateTime.Now,
                PacketsReceived = 0,
                DataLength = 0,
                LastActivity = DateTime.Now
            };

            OnClientAdded?.Invoke(clientInfo);
            OnClientCountChanged?.Invoke(_dataParsers.Count);

            return connectionId;
        }


        public DataParser? GetParser(int connectionId)
        {
            if (connectionId < 1 || connectionId > _dataParsers.Count)
                return null;

            return _dataParsers[connectionId - 1];
        }
        
        public bool ConnectionExists(int connectionId)
        {
            return connectionId >= 1 && connectionId <= _dataParsers.Count;
        }

        public int GetConnectionIdByIp(string clientIp)
        {
            for (int i = 0; i < _dataParsers.Count; i++)
            {
                if (_dataParsers[i].ClientIP == clientIp)
                {
                    return i + 1; // connectionId is 1-based
                }
            }
            return -1; // Not found
        }
        
        public void SaveAllLogs(Action<string> logCallback)
        {
            logCallback("\n[Shutdown] Saving all logs...");
            
            for (int i = 0; i < _dataParsers.Count; i++)
            {
                var parser = _dataParsers[i];
                if (parser.DataLength > 0)
                {
                    parser.SaveToFile(_logPath, i + 1);
                    logCallback($"[Saved] Connection #{i + 1} ({parser.DataLength} bytes)");
                }
            }
            
            logCallback($"[Done] Saved {_dataParsers.Count} connection(s)");
        }
    }
}

