// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    public interface IDispatchMessageInspector
    {
        object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext);
        void BeforeSendReply(ref Message reply, object correlationState);
    }
}