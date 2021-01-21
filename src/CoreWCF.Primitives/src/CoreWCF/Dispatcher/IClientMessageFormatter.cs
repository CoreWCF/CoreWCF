// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    public interface IClientMessageFormatter
    {
        Message SerializeRequest(MessageVersion messageVersion, object[] parameters);
        object DeserializeReply(Message message, object[] parameters);
    }
}