﻿using CoreWCF.Channels;

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