// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;
using CoreWCF.Channels;

namespace ServiceContract
{
    [ServiceContract]
    public interface IRemoteEndpointMessageProperty
    {
        [OperationContract(Action = "*", ReplyAction = "*")]
        Message Echo(Message input);
    }
}
