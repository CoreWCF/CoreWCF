// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Dispatcher
{
    internal interface IInputSessionShutdown
    {
        void ChannelFaulted(IDuplexContextChannel channel);
        void DoneReceiving(IDuplexContextChannel channel);
    }
}