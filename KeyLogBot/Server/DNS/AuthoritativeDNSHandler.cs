namespace Server.DNS
{

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
            
            if (stripped == _domain)
            {
                return DNSResponseBuilder.CreateAuthoritativeResponse(
                    originalQuery, query, _serverIp, _domain);
            }
            
            if (stripped == $"ns1.{_domain}" || stripped == $"ns2.{_domain}")
            {
                return DNSResponseBuilder.CreateSimpleAResponse(
                    originalQuery, query, _serverIp);
            }
            
            if (stripped.EndsWith($".{_domain}"))
            {
                return DNSResponseBuilder.CreateSimpleAResponse(
                    originalQuery, query, _serverIp);
            }
            
            return DNSResponseBuilder.CreateEmptyResponse(originalQuery, query);
        }
    }
}

