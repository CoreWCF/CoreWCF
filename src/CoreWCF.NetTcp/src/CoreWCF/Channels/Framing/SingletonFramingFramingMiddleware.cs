using CoreWCF.Configuration;
using System.Threading.Tasks;

namespace CoreWCF.Channels.Framing
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