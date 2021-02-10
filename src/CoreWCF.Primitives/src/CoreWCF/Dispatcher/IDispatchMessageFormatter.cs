// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal interface IDispatchMessageFormatter
    {
        void DeserializeRequest(Message message, object[] parameters);
        Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result);
    }
}