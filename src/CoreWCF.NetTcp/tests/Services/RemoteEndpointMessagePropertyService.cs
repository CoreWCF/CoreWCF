// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using ServiceContract;

namespace Services
{
    public class RemoteEndpointMessagePropertyService : IRemoteEndpointMessageProperty
    {
        public Message Echo(Message input)
        {
            RemoteEndpointMessageProperty remp = (RemoteEndpointMessageProperty)input.Properties[RemoteEndpointMessageProperty.Name];
            return Message.CreateMessage(input.Version, "echo", input.GetBody<string>() + ";" + remp.Address + ";" + remp.Port.ToString());
        }
    }
}
