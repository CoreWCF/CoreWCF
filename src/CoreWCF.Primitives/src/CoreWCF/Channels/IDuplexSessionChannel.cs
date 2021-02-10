// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    public interface IDuplexSessionChannel : IChannel, IDuplexChannel, IInputChannel, IOutputChannel, ISessionChannel<IDuplexSession>, ICommunicationObject
    {
    }
}