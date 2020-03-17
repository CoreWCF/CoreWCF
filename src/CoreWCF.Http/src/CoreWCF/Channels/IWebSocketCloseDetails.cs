using System.Net.WebSockets;

namespace CoreWCF.Channels
{
    public interface IWebSocketCloseDetails
    {
        WebSocketCloseStatus? InputCloseStatus { get; }
        string InputCloseStatusDescription { get; }
        void SetOutputCloseStatus(WebSocketCloseStatus closeStatus, string closeStatusDescription);
    }
}
