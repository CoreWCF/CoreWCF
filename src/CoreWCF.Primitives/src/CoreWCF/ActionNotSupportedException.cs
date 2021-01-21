// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF
{
    //[Serializable]
    public class ActionNotSupportedException : CommunicationException
    {
        public ActionNotSupportedException() { }
        public ActionNotSupportedException(string message) : base(message) { }
        public ActionNotSupportedException(string message, Exception innerException) : base(message, innerException) { }
        // protected ActionNotSupportedException(SerializationInfo info, StreamingContext context) : base(info, context) { }

        internal Message ProvideFault(MessageVersion messageVersion)
        {
            Fx.Assert(messageVersion.Addressing != AddressingVersion.None, "");
            FaultCode code = FaultCode.CreateSenderFaultCode(AddressingStrings.ActionNotSupported, messageVersion.Addressing.Namespace);
            string reason = Message;
            return CoreWCF.Channels.Message.CreateMessage(
               messageVersion, code, reason, messageVersion.Addressing.FaultAction);
        }
    }
}