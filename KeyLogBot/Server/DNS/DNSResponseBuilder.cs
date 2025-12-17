using System.Text;

namespace Server.DNS
{
    
    public class DNSResponseBuilder
    {
        
        public static byte[] CreateSimpleAResponse(byte[] originalQuery, DNSQueryInfo query, string ipAddress)
        {
            var response = new List<byte>();

            response.Add((byte)(query.TransactionId >> 8));
            response.Add((byte)(query.TransactionId & 0xFF));
            response.Add(0x81); response.Add(0x80);
            response.Add(0x00); response.Add(0x01); 
            response.Add(0x00); response.Add(0x01); 
            response.Add(0x00); response.Add(0x00); 
            response.Add(0x00); response.Add(0x00); 

            
            response.AddRange(originalQuery.Skip(12).TakeWhile(b => b != 0));
            response.Add(0x00);
            response.Add(0x00); response.Add(0x01); 
            response.Add(0x00); response.Add(0x01); 
            
            response.Add(0xC0); response.Add(0x0C); 
            response.Add(0x00); response.Add(0x01); 
            response.Add(0x00); response.Add(0x01);
            response.Add(0x00); response.Add(0x00); response.Add(0x00); response.Add(0x00); // TTL = 0 (no cache)
            response.Add(0x00); response.Add(0x04); 
            foreach (var octet in ipAddress.Split('.'))
                response.Add(byte.Parse(octet));

            return response.ToArray();
        }

        
        
        public static byte[] CreateEmptyResponse(byte[] originalQuery, DNSQueryInfo query)
        {
            var response = new List<byte>();
            response.Add((byte)(query.TransactionId >> 8));
            response.Add((byte)(query.TransactionId & 0xFF));
            response.Add(0x81); response.Add(0x80);
            response.Add(0x00); response.Add(0x01);
            response.Add(0x00); response.Add(0x00); 
            response.Add(0x00); response.Add(0x00);
            response.Add(0x00); response.Add(0x00);
            
            response.AddRange(originalQuery.Skip(12).TakeWhile(b => b != 0));
            response.Add(0x00);
            response.Add(0x00); response.Add(0x01);
            response.Add(0x00); response.Add(0x01);

            return response.ToArray();
        }
        
        // CreateAuthoritativeResponse removed - BIND9 handles NS/SOA/Authority records

        /// <summary>
        /// Creates a TXT record response for sending command chunks to client
        /// </summary>
        public static byte[] CreateTXTResponse(byte[] originalQuery, DNSQueryInfo query, string txtData)
        {
            var response = new List<byte>();

            // DNS Header
            response.Add((byte)(query.TransactionId >> 8));
            response.Add((byte)(query.TransactionId & 0xFF));
            response.Add(0x81); response.Add(0x80); // Flags: response, authoritative
            response.Add(0x00); response.Add(0x01); // QDCOUNT = 1
            response.Add(0x00); response.Add(0x01); // ANCOUNT = 1
            response.Add(0x00); response.Add(0x00); // NSCOUNT = 0
            response.Add(0x00); response.Add(0x00); // ARCOUNT = 0

            // Question section (copy from original query)
            response.AddRange(originalQuery.Skip(12).TakeWhile(b => b != 0));
            response.Add(0x00);
            response.Add(0x00); response.Add(0x10); // QTYPE = TXT (16)
            response.Add(0x00); response.Add(0x01); // QCLASS = IN

            // Answer section
            response.Add(0xC0); response.Add(0x0C); // Name pointer to question
            response.Add(0x00); response.Add(0x10); // TYPE = TXT
            response.Add(0x00); response.Add(0x01); // CLASS = IN
            response.Add(0x00); response.Add(0x00); response.Add(0x00); response.Add(0x00); // TTL = 0 (no cache)

            // RDATA
            byte[] txtBytes = Encoding.UTF8.GetBytes(txtData);
            int rdLength = txtBytes.Length + 1; // +1 for length byte
            response.Add((byte)(rdLength >> 8));
            response.Add((byte)(rdLength & 0xFF));
            response.Add((byte)txtBytes.Length); // TXT length byte
            response.AddRange(txtBytes);

            return response.ToArray();
        }
    }
}

