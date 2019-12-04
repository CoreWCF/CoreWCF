using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;

namespace DispatcherClient
{
    internal class DispatcherRequestSessionChannel : DispatcherRequestChannel, IRequestSessionChannel
    {
        public DispatcherRequestSessionChannel(IServiceProvider serviceProvider, EndpointAddress to, Uri via)
            : base(serviceProvider, to, via)
        {
        }

        IOutputSession ISessionChannel<IOutputSession>.Session { get; } = new OutputSession();

        class OutputSession : IOutputSession
        {
            public string Id { get; } = "uuid://dispatcher-session/" + Guid.NewGuid().ToString();
        }
    }
}
