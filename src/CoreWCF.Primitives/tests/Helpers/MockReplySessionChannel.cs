using CoreWCF.Channels;
using System;
using System.Collections.Generic;
using System.Text;

namespace Helpers
{
    internal class MockReplySessionChannel : MockReplyChannel, IReplySessionChannel
    {
        public MockReplySessionChannel(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            Session = new MockReplySessionChannelSession(Guid.NewGuid().ToString());
        }

        public IInputSession Session { get; }
 
        private class MockReplySessionChannelSession : IInputSession
        {
            public MockReplySessionChannelSession(string id)
            {
                Id = id;
            }

            public string Id { get; }
        }
    }
}
