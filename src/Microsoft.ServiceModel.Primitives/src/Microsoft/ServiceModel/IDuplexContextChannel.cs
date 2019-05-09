using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel
{
    internal interface IDuplexContextChannel : IContextChannel
    {
        bool AutomaticInputSessionShutdown { get; set; }
        InstanceContext CallbackInstance { get; set; }
        Task CloseOutputSessionAsync(CancellationToken token);
    }
}