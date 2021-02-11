// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Configuration;

namespace CoreWCF.Channels
{
    public interface IChannel : ICommunicationObject
    {
        T GetProperty<T>() where T : class;
        IServiceChannelDispatcher ChannelDispatcher { get; set; }
    }
}