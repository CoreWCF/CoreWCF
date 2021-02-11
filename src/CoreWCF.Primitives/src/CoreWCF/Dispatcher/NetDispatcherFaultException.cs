// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Dispatcher
{
    internal class NetDispatcherFaultException : FaultException
    {
        public NetDispatcherFaultException(string reason, FaultCode code, Exception innerException)
            : base(reason, code, FaultCodeConstants.Actions.NetDispatcher, innerException)
        {
        }
        public NetDispatcherFaultException(FaultReason reason, FaultCode code, Exception innerException)
            : base(reason, code, FaultCodeConstants.Actions.NetDispatcher, innerException)
        {
        }
    }
}