// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal interface ICallContextInitializer
    {
        object BeforeInvoke(InstanceContext instanceContext, IClientChannel channel, Message message);
        void AfterInvoke(object correlationState);
    }
}