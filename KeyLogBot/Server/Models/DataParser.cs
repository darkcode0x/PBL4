using System.Text;

namespace Server.Models
{
    /// <summary>
    /// Manages data from a single client connection
    /// </summary>
    public class DataParser
    {
        public string ClientIP { get; }
        public DateTime CreatedAt { get; }
        public int LastReceivedPacket { get; private set; }
        private readonly List<byte> _data;

        public DataParser(string clientIP)
        {
            ClientIP = clientIP;
            CreatedAt = DateTime.Now;
            LastReceivedPacket = -1;
            _data = new List<byte>();
        }

        public int DataLength => _data.Count;


        public void AddData(int packetNumber, byte[] data)
        {
            // Check for duplicate packet
            if (packetNumber == LastReceivedPacket)
            {
                throw new DuplicatePacketException();
            }

            // Check for out of order
            if (!(packetNumber > LastReceivedPacket || packetNumber == 0))
            {
                LastReceivedPacket = 0;
                throw new OutOfOrderException();
            }

            _data.AddRange(data);
            LastReceivedPacket = packetNumber;
        }


        public string GetAllData()
        {
            // Use ASCII with replacement for invalid chars (avoid box characters)
            return Encoding.ASCII.GetString(_data.ToArray());
        }

        /// <summary>
        /// Save data to file
        /// </summary>
        public void SaveToFile(string logPath, int connectionId)
        {
            Directory.CreateDirectory(logPath);
            string filename = Path.Combine(logPath, 
                $"client_{connectionId}_{ClientIP}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.log");
            File.WriteAllText(filename, GetAllData(), Encoding.ASCII);
        }
    }

    // Custom exceptions
    public class DuplicatePacketException : Exception { }
    public class OutOfOrderException : Exception { }
}
