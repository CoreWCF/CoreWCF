using Microsoft.AspNetCore.Connections;
using CoreWCF.Configuration;
using System.Threading.Tasks;

namespace CoreWCF.Channels.Framing
{
    internal class FramingModeHandshakeMiddleware
    {
        private HandshakeDelegate _next;

        public FramingModeHandshakeMiddleware(HandshakeDelegate next)
        {
            _next = next;
        }

        public async Task OnConnectedAsync(FramingConnection connection)
        {
            var inputPipe = connection.Input;
            var modeDecoder = new ServerModeDecoder();
            await modeDecoder.ReadModeAsync(inputPipe);
            connection.FramingMode = modeDecoder.Mode;
            await _next(connection);
        }
    }
}