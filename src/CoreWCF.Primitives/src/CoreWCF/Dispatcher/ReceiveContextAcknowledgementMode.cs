// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Dispatcher
{
    internal enum ReceiveContextAcknowledgementMode
    {
        AutoAcknowledgeOnReceive = 0,
        AutoAcknowledgeOnRPCComplete = 1,
        ManualAcknowledgement = 2
    }
}