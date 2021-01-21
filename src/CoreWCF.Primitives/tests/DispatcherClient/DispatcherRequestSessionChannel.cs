// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

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
