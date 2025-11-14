using Server.Models;

namespace Server.Utilities
{
    public static class IPGenerator
    {

        public static string CreateStartIp(int connections)
        {
            if (connections >= 254)
            {
                throw new ServerMaxConnectionsException();
            }
            
            var reserved = new HashSet<int> 
            { 
                0, 10, 100, 127, 169, 172, 192, 198, 203, 224, 233, 250, 255 
            };
            reserved.Add((int)ResponseCode.OK);
            reserved.Add((int)ResponseCode.MALFORMED);
            reserved.Add((int)ResponseCode.NX);
            reserved.Add((int)ResponseCode.OOO);
            reserved.Add((int)ResponseCode.MAX);
            
            var availableFirst = Enumerable.Range(1, 253)
                .Where(i => !reserved.Contains(i))
                .ToList();
            int first = availableFirst[Random.Shared.Next(availableFirst.Count)];
            
            int second = Random.Shared.Next(256);
            int third = Random.Shared.Next(256);
            
            int fourth = connections + 1;

            return $"{first}.{second}.{third}.{fourth}";
        }
        
        public static string CreateResponseIp(ResponseCode code)
        {
            int first = (int)code;
            int second = Random.Shared.Next(1, 255);
            int third = Random.Shared.Next(1, 255);
            int fourth = Random.Shared.Next(1, 255);

            return $"{first}.{second}.{third}.{fourth}";
        }
    }
}

