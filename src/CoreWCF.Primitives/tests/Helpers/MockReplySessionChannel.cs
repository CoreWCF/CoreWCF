// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using CoreWCF.Channels;

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
