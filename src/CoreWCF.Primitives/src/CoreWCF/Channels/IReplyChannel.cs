using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    public interface IReplyChannel : IChannel
    {
        EndpointAddress LocalAddress { get; }
    }
}