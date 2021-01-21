// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    public abstract class SessionOpenNotification
    {
        public abstract bool IsEnabled { get; }
        public abstract void UpdateMessageProperties(MessageProperties inboundMessageProperties);
    }
}