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
            response.Add(0x00); response.Add(0x00); response.Add(0x00); response.Add(0x3C); 
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
        
        
        public static byte[] CreateAuthoritativeResponse(byte[] originalQuery, DNSQueryInfo query,  string ipAddress, string domain)
        {
            var response = new List<byte>();

            response.Add((byte)(query.TransactionId >> 8));
            response.Add((byte)(query.TransactionId & 0xFF));
            response.Add(0x81); 
            response.Add(0x80); 
            response.Add(0x00); response.Add(0x01); 
            response.Add(0x00); response.Add(0x01); 
            response.Add(0x00); response.Add(0x02); 
            response.Add(0x00); response.Add(0x01); 


            int questionEnd = 12;
            while (questionEnd < originalQuery.Length && originalQuery[questionEnd] != 0)
            {
                questionEnd++;
            }
            questionEnd += 5; 
            response.AddRange(originalQuery.Skip(12).Take(questionEnd - 12));
            
            response.Add(0xC0); response.Add(0x0C); 
            response.Add(0x00); response.Add(0x01); 
            response.Add(0x00); response.Add(0x01); 
            response.Add(0x00); response.Add(0x00); response.Add(0x0E); response.Add(0x10); 
            response.Add(0x00); response.Add(0x04); 
            foreach (var octet in ipAddress.Split('.'))
                response.Add(byte.Parse(octet));
            
            for (int i = 1; i <= 2; i++)
            {
                response.Add(0xC0); response.Add(0x0C); 
                response.Add(0x00); response.Add(0x02);
                response.Add(0x00); response.Add(0x01);
                response.Add(0x00); response.Add(0x00); response.Add(0x0E); response.Add(0x10);

                string nsName = $"ns{i}.{domain}";
                byte[] nsNameBytes = DNSParser.EncodeDomainName(nsName);
                response.Add(0x00); response.Add((byte)nsNameBytes.Length);
                response.AddRange(nsNameBytes);
            }
            
            response.Add(0xC0); response.Add(0x0C);
            response.Add(0x00); response.Add(0x06); 
            response.Add(0x00); response.Add(0x01); 
            response.Add(0x00); response.Add(0x00); response.Add(0x0E); response.Add(0x10); 
            response.Add(0x00); response.Add(0x14); 

            response.AddRange(new byte[20]);

            return response.ToArray();
        }

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
            response.Add(0x00); response.Add(0x00); response.Add(0x00); response.Add(0x3C); // TTL = 60 seconds

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

