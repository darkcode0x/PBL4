namespace Server.Models
{
    /// <summary>
    /// Client connection information
    /// </summary>
    public class ClientInfo
    {
        public int ConnectionId { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public DateTime ConnectedAt { get; set; }
        public int PacketsReceived { get; set; }
        public int DataLength { get; set; }
        public DateTime LastActivity { get; set; }
    }
}
