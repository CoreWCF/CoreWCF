// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    public interface IDispatchFaultFormatter
    {
        MessageFault Serialize(FaultException faultException, out string action);
    }

    // Only used on full framework by WebHttpBehavior
    public interface IDispatchFaultFormatterWrapper
    {
        IDispatchFaultFormatter InnerFaultFormatter
        {
            get;
            set;
        }
    }
}