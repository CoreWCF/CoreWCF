// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public interface IServiceDispatcher
    {
        Uri BaseAddress { get; }
        Binding Binding { get; }
        ServiceHostBase Host { get; }
        IList<Type> SupportedChannelTypes { get; }
        Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel);
    }
}
