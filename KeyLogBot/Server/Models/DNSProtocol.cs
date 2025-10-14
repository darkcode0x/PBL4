namespace Server.Models
{
    /// <summary>
    /// DNS packet types received from client
    /// </summary>
    public enum ReceivedPacketType
    {
        START = 'a',  // Connection request
        DATA = 'b'    // Data packet
    }

    /// <summary>
    /// Response codes sent to client (in IP first octet)
    /// </summary>
    public enum ResponseCode
    {
        OK = 200,          // Processed normally
        MALFORMED = 201,   // Malformed packet
        NX = 202,          // Connection non-existent
        OOO = 203,         // Out of order packets
        MAX = 204          // Max connections reached
    }

    // Custom Exceptions - giống Python
    public class ShortCircuitException : Exception { }
    public class UnrelatedException : Exception { }
    public class DNSSyntaxException : Exception { }
    public class ServerMaxConnectionsException : Exception { }
    public class NXConnectionException : Exception { }
    public class PacketsOutOfOrderException : Exception { }
}

