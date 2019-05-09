using Microsoft.ServiceModel.Configuration;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Channels.Framing
{
    internal class SingletonFramingFramingMiddleware
    {
        private HandshakeDelegate _next;

        public SingletonFramingFramingMiddleware(HandshakeDelegate next)
        {
            _next = next;
        }

        public Task OnConnectedAsync(FramingConnection connection)
        {
            return Task.CompletedTask;
        }
    }
}