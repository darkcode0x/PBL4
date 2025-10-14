namespace Server.DNS
{
    /// <summary>
    /// Handle normal DNS queries for production mode
    /// </summary>
    public class AuthoritativeDNSHandler
    {
        private readonly string _domain;
        private readonly string _serverIp;

        public AuthoritativeDNSHandler(string domain, string serverIp)
        {
            _domain = domain;
            _serverIp = serverIp;
        }

        /// <summary>
        /// Handle normal DNS queries
        /// LOCAL TEST: Rarely used (only for manual testing)
        /// PRODUCTION: Essential for acting as Authoritative DNS
        /// </summary>
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

            // Query for subdomains (e.g., email.example.com)
            if (stripped.EndsWith($".{_domain}"))
            {
                // Return A record pointing to server IP
                return DNSResponseBuilder.CreateSimpleAResponse(
                    originalQuery, query, _serverIp);
            }

            // Unknown query - return empty response
            return DNSResponseBuilder.CreateEmptyResponse(originalQuery, query);
        }
    }
}

