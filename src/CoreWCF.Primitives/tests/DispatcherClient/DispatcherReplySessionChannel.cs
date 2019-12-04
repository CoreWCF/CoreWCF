using System;
using CoreWCF.Channels;

namespace DispatcherClient
{
    internal class DispatcherReplySessionChannel : DispatcherReplyChannel, IReplySessionChannel
    {
        private IServiceProvider _serviceProvider;

        public DispatcherReplySessionChannel(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public IInputSession Session { get; } = new InputSession();

        class InputSession : IInputSession
        {
            public string Id { get; } = "uuid://dispatcher-session/" + Guid.NewGuid().ToString();
        }
    }
}
