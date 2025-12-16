using System.Text;
using Server.Models;

namespace Server.Logic
{

    public class ProtocolHandler
    {
        private readonly string _domain;

        public event Action<int, string>? OnDataReceived;

        public ProtocolHandler(string domain)
        {
            _domain = domain;
        }


        public string GetData(string full)
        {
            string stripped = full.TrimEnd('.');

            if (!(stripped == _domain || stripped.EndsWith("." + _domain)))
            {
                throw new ShortCircuitException();
            }

            // Check packet type first
            string[] initialParts = stripped.Split('.');
            if (initialParts.Length > 0)
            {
                string packetType = initialParts[0];
                
                // Type A packets: a.[IP].domain.com (e.g., a.100.123.123.123.example.com)
                // Has 5 parts before domain instead of 4
                if (packetType == "a")
                {
                    // For type A, extract: a.IP1.IP2.IP3.IP4
                    int domainIndex = stripped.LastIndexOf("." + _domain);
                    if (domainIndex > 0)
                    {
                        return stripped.Substring(0, domainIndex);
                    }
                }
                
                // Type C packets can have variable length due to hex data
                if (packetType == "c")
                {
                    // For type C, extract everything before domain
                    int domainIndex = stripped.LastIndexOf("." + _domain);
                    if (domainIndex > 0)
                    {
                        return stripped.Substring(0, domainIndex);
                    }
                }
            }
            
            // For other packet types (b, p), use original logic
            int expectedDots = _domain.Count(c => c == '.') + 4;
            int actualDots = stripped.Count(c => c == '.');

            if (actualDots != expectedDots)
            {
                // Should not happen - BIND9 only forwards protocol queries
                throw new ShortCircuitException();
            }
            
            return full.Substring(0, IndexOfSecondDot(stripped));
        }
        
        public (int PacketNumber, int ConnectionId) ParseDataPacket(string data, ClientManager clientManager)
        {
 
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
            catch (PacketsOutOfOrderException)
            {
                // Re-throw to be caught by ServerLogic
                throw;
            }

            return (packetNumber, connectionId);
        }
        

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
