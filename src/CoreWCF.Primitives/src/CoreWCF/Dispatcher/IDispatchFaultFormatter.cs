// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal interface IDispatchFaultFormatter
    {
        MessageFault Serialize(FaultException faultException, out string action);
    }

    // Only used on full framework by WebHttpBehavior
    internal interface IDispatchFaultFormatterWrapper
    {
        IDispatchFaultFormatter InnerFaultFormatter
        {
            get;
            set;
        }
    }
}