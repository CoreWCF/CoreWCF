using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;

namespace CoreWCF
{
    internal interface IDuplexContextChannel : IContextChannel
    {
        bool AutomaticInputSessionShutdown { get; set; }
        InstanceContext CallbackInstance { get; set; }
        Task CloseOutputSessionAsync(CancellationToken token);
    }
}