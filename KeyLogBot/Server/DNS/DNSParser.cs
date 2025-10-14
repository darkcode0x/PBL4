using System.Text;

namespace Server.DNS
{
    /// <summary>
    /// Parse DNS packets from raw UDP data
    /// </summary>
    public class DNSParser
    {
        /// <summary>
        /// Parse DNS query from raw bytes
        /// </summary>
        public static DNSQueryInfo ParseQuery(byte[] data)
        {
            int pos = 12;
            StringBuilder queryName = new StringBuilder();

            while (pos < data.Length && data[pos] != 0)
            {
                int len = data[pos++];
                if (len == 0) break;

                for (int i = 0; i < len && pos < data.Length; i++)
                {
                    queryName.Append((char)data[pos++]);
                }
                if (pos < data.Length && data[pos] != 0)
                    queryName.Append('.');
            }

            pos++; // Skip null terminator
            ushort queryType = pos + 1 < data.Length 
                ? (ushort)((data[pos] << 8) | data[pos + 1]) 
                : (ushort)0;

            return new DNSQueryInfo
            {
                QueryName = queryName.ToString(),
                TransactionId = (ushort)((data[0] << 8) | data[1]),
                QueryType = queryType
            };
        }

        /// <summary>
        /// Encode domain name to DNS format
        /// </summary>
        public static byte[] EncodeDomainName(string domain)
        {
            var result = new List<byte>();
            foreach (var label in domain.Split('.'))
            {
                result.Add((byte)label.Length);
                result.AddRange(Encoding.ASCII.GetBytes(label));
            }
            result.Add(0x00);
            return result.ToArray();
        }
    }

    /// <summary>
    /// DNS query information
    /// </summary>
    public class DNSQueryInfo
    {
        public string QueryName { get; set; } = string.Empty;
        public ushort TransactionId { get; set; }
        public ushort QueryType { get; set; }
    }
}

