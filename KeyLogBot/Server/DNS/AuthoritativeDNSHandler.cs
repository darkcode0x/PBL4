namespace Server.DNS
{
    /// Handle normal DNS queries for production mode

    public class AuthoritativeDNSHandler
    {
        private readonly string _domain;
        private readonly string _serverIp;

        public AuthoritativeDNSHandler(string domain, string serverIp)
        {
            _domain = domain;
            _serverIp = serverIp;
        }

        public byte[] HandleQuery(byte[] originalQuery, DNSQueryInfo query, string queryName)
        {
            string stripped = queryName.TrimEnd('.');

            // Query for main domain: example.com
            if (stripped == _domain)
            {
                return DNSResponseBuilder.CreateAuthoritativeResponse(
                    originalQuery, query, _serverIp, _domain);
            }

            // Query for nameservers: ns1.example.com, ns2.example.com
            if (stripped == $"ns1.{_domain}" || stripped == $"ns2.{_domain}")
            {
                return DNSResponseBuilder.CreateSimpleAResponse(
                    originalQuery, query, _serverIp);
            }
            
            if (stripped.EndsWith($".{_domain}"))
            {
                // Return A record pointing to server IP
                return DNSResponseBuilder.CreateSimpleAResponse(
                    originalQuery, query, _serverIp);
            }
            
            return DNSResponseBuilder.CreateEmptyResponse(originalQuery, query);
        }
    }
}

