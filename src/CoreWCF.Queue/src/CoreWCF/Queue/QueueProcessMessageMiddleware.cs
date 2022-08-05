using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Queue
{
    public class QueueProcessMessageMiddleware
    {
        private readonly QueueMessageDispatch _next;
        private readonly ILogger<QueueProcessMessageMiddleware> _logger;

        public QueueProcessMessageMiddleware(QueueMessageDispatch next, ILogger<QueueProcessMessageMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(QueueMessageContext context)
        {
            _logger.LogInformation($"Invoke {nameof(QueueProcessMessageMiddleware)}");

            await context.QueueTransportContext.ServiceChannelDispatcher.DispatchAsync(context.RequestMessage);
            await _next(context);
        }
    }
}
