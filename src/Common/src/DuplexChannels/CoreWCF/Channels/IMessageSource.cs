using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    interface IMessageSource
    {
        Task<Message> ReceiveAsync(CancellationToken token);
        Task<bool> WaitForMessageAsync(CancellationToken token);
    }
}
