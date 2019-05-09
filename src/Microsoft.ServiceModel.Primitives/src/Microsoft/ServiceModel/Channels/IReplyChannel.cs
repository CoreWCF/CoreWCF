using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Channels
{
    public interface IReplyChannel : IChannel
    {
        EndpointAddress LocalAddress { get; }
        Task<RequestContext> ReceiveRequestAsync();
        Task<RequestContext> ReceiveRequestAsync(CancellationToken token);
        Task<TryAsyncResult<RequestContext>> TryReceiveRequestAsync(CancellationToken token);
        Task<bool> WaitForRequestAsync(CancellationToken token);
    }
}