// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public interface IServiceChannelDispatcher
    {
        Task DispatchAsync(RequestContext context);
        Task DispatchAsync(Message message);
    }
}
