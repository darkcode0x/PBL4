namespace Server.Models
{
    
    public enum ReceivedPacketType
    {
        START = 'a', 
        DATA = 'b'    
    }
    
    
    public enum ResponseCode
    {
        OK = 200,          
        MALFORMED = 201,  
        NX = 202,         
        OOO = 203,        
        MAX = 204         
    }
    
    public class ShortCircuitException : Exception { }
    public class UnrelatedException : Exception { }
    public class DNSSyntaxException : Exception { }
    public class ServerMaxConnectionsException : Exception { }
    public class NXConnectionException : Exception { }
    public class PacketsOutOfOrderException : Exception { }
}

