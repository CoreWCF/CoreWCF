// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;

namespace DispatcherClient
{
    internal class DispatcherReplySessionChannel : DispatcherReplyChannel, IReplySessionChannel
    {
        public DispatcherReplySessionChannel(IServiceProvider serviceProvider) : base(serviceProvider) { }

        public IInputSession Session { get; } = new InputSession();

        private class InputSession : IInputSession
        {
            public string Id { get; } = "uuid://dispatcher-session/" + Guid.NewGuid().ToString();
        }
    }
}
