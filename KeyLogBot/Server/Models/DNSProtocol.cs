namespace Server.Models
{
    
    public enum ResponseCode
    {
        OK = 200,          
        MALFORMED = 201,  
        NX = 202,         
        OOO = 203,        
        MAX = 204         
    }
    
    public class ShortCircuitException : Exception { }
    // UnrelatedException removed - BIND9 handles normal DNS, C&C only processes protocol packets
    public class DNSSyntaxException : Exception { }
    public class ServerMaxConnectionsException : Exception { }
    public class NXConnectionException : Exception { }
    public class PacketsOutOfOrderException : Exception { }
}

