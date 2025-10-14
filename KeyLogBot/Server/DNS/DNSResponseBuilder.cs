using System.Text;

namespace Server.DNS
{
    /// <summary>
    /// Build DNS response packets
    /// </summary>
    public class DNSResponseBuilder
    {
        /// <summary>
        /// Create simple A record response
        /// </summary>
        public static byte[] CreateSimpleAResponse(byte[] originalQuery, DNSQueryInfo query, string ipAddress)
        {
            var response = new List<byte>();

            // Header
            response.Add((byte)(query.TransactionId >> 8));
            response.Add((byte)(query.TransactionId & 0xFF));
            response.Add(0x81); response.Add(0x80);
            response.Add(0x00); response.Add(0x01); // Questions: 1
            response.Add(0x00); response.Add(0x01); // Answers: 1
            response.Add(0x00); response.Add(0x00); // Authority: 0
            response.Add(0x00); response.Add(0x00); // Additional: 0

            // Question (copy from original)
            response.AddRange(originalQuery.Skip(12).TakeWhile(b => b != 0));
            response.Add(0x00);
            response.Add(0x00); response.Add(0x01); // Type A
            response.Add(0x00); response.Add(0x01); // Class IN

            // Answer
            response.Add(0xC0); response.Add(0x0C); // Name pointer
            response.Add(0x00); response.Add(0x01); // Type A
            response.Add(0x00); response.Add(0x01); // Class IN
            response.Add(0x00); response.Add(0x00); response.Add(0x00); response.Add(0x3C); // TTL 60
            response.Add(0x00); response.Add(0x04); // Data length
            foreach (var octet in ipAddress.Split('.'))
                response.Add(byte.Parse(octet));

            return response.ToArray();
        }

        /// <summary>
        /// Create empty DNS response (for unknown queries)
        /// </summary>
        public static byte[] CreateEmptyResponse(byte[] originalQuery, DNSQueryInfo query)
        {
            var response = new List<byte>();
            response.Add((byte)(query.TransactionId >> 8));
            response.Add((byte)(query.TransactionId & 0xFF));
            response.Add(0x81); response.Add(0x80);
            response.Add(0x00); response.Add(0x01);
            response.Add(0x00); response.Add(0x00); // No answers
            response.Add(0x00); response.Add(0x00);
            response.Add(0x00); response.Add(0x00);

            // Copy question
            response.AddRange(originalQuery.Skip(12).TakeWhile(b => b != 0));
            response.Add(0x00);
            response.Add(0x00); response.Add(0x01);
            response.Add(0x00); response.Add(0x01);

            return response.ToArray();
        }

        /// <summary>
        /// Create authoritative response with NS and SOA records
        /// </summary>
        public static byte[] CreateAuthoritativeResponse(byte[] originalQuery, DNSQueryInfo query, 
            string ipAddress, string domain)
        {
            var response = new List<byte>();

            // DNS Header
            response.Add((byte)(query.TransactionId >> 8));
            response.Add((byte)(query.TransactionId & 0xFF));
            response.Add(0x81); // QR=1, Opcode=0, AA=1
            response.Add(0x80); // RA=1
            response.Add(0x00); response.Add(0x01); // Questions: 1
            response.Add(0x00); response.Add(0x01); // Answers: 1
            response.Add(0x00); response.Add(0x02); // Authority RRs: 2 (NS records)
            response.Add(0x00); response.Add(0x01); // Additional RRs: 1 (SOA)

            // Question section (copy from original)
            int questionEnd = 12;
            while (questionEnd < originalQuery.Length && originalQuery[questionEnd] != 0)
            {
                questionEnd++;
            }
            questionEnd += 5; // +1 for null byte, +4 for type and class
            response.AddRange(originalQuery.Skip(12).Take(questionEnd - 12));

            // Answer section - A record
            response.Add(0xC0); response.Add(0x0C); // Name pointer
            response.Add(0x00); response.Add(0x01); // Type A
            response.Add(0x00); response.Add(0x01); // Class IN
            response.Add(0x00); response.Add(0x00); response.Add(0x0E); response.Add(0x10); // TTL 3600
            response.Add(0x00); response.Add(0x04); // Data length
            foreach (var octet in ipAddress.Split('.'))
                response.Add(byte.Parse(octet));

            // Authority section - NS records (simplified)
            for (int i = 1; i <= 2; i++)
            {
                response.Add(0xC0); response.Add(0x0C); // Name pointer
                response.Add(0x00); response.Add(0x02); // Type NS
                response.Add(0x00); response.Add(0x01); // Class IN
                response.Add(0x00); response.Add(0x00); response.Add(0x0E); response.Add(0x10); // TTL

                string nsName = $"ns{i}.{domain}";
                byte[] nsNameBytes = DNSParser.EncodeDomainName(nsName);
                response.Add(0x00); response.Add((byte)nsNameBytes.Length);
                response.AddRange(nsNameBytes);
            }

            // Additional section - SOA (simplified)
            response.Add(0xC0); response.Add(0x0C);
            response.Add(0x00); response.Add(0x06); // Type SOA
            response.Add(0x00); response.Add(0x01); // Class IN
            response.Add(0x00); response.Add(0x00); response.Add(0x0E); response.Add(0x10); // TTL
            response.Add(0x00); response.Add(0x14); // Data length (20 bytes simplified)
            // SOA data (simplified)
            response.AddRange(new byte[20]);

            return response.ToArray();
        }
    }
}

