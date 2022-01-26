// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Dispatcher
{
    public class NetDispatcherFaultException : FaultException
    {
        public NetDispatcherFaultException(string reason, FaultCode code, Exception innerException)
            : base(reason, code, FaultCodeConstants.Actions.NetDispatcher, innerException)
        {
        }
        public NetDispatcherFaultException(FaultReason reason, FaultCode code, Exception innerException)
            : base(reason, code, FaultCodeConstants.Actions.NetDispatcher, innerException)
        {
        }

        public static NetDispatcherFaultException CreateDeserializationFailedFault(string reason, Exception innerException)
        {
            reason = SR.Format(SR.SFxDeserializationFailed1, reason);
            FaultCode code = new FaultCode(FaultCodeConstants.Codes.DeserializationFailed, FaultCodeConstants.Namespaces.NetDispatch);
            code = FaultCode.CreateSenderFaultCode(code);
            return new NetDispatcherFaultException(reason, code, innerException);
        }
    }
}
