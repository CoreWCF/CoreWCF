// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;

namespace CoreWCF.Queue.Tests.Fakes
{
    internal class FakeServiceDispatcher : IServiceDispatcher
    {
        public Uri BaseAddress { get; }
        public Binding Binding => new FakeBinding();
        public ServiceHostBase Host { get; }
        public IList<Type> SupportedChannelTypes { get; }

        public Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel) =>
            throw new NotImplementedException();
    }
}
