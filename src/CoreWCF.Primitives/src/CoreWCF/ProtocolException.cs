// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Runtime.Serialization;
using CoreWCF.Channels;

namespace CoreWCF
{
    [Serializable]
    public class ProtocolException : CommunicationException
    {
        public ProtocolException() { }
        public ProtocolException(string message) : base(message) { }
        public ProtocolException(string message, Exception innerException) : base(message, innerException) { }
        protected ProtocolException(SerializationInfo info, StreamingContext context) : base(info, context) { }

        internal static ProtocolException ReceiveShutdownReturnedNonNull(Message message)
        {
            if (message.IsFault)
            {
                try
                {
                    MessageFault fault = MessageFault.CreateFault(message, 64 * 1024);
                    FaultReasonText reason = fault.Reason.GetMatchingTranslation(CultureInfo.CurrentCulture);
                    string text = SR.Format(SRCommon.ReceiveShutdownReturnedFault, reason.Text);
                    return new ProtocolException(text);
                }
                catch (QuotaExceededException)
                {
                    string text = SR.Format(SRCommon.ReceiveShutdownReturnedLargeFault, message.Headers.Action);
                    return new ProtocolException(text);
                }
            }
            else
            {
                string text = SR.Format(SRCommon.ReceiveShutdownReturnedMessage, message.Headers.Action);
                return new ProtocolException(text);
            }
        }
    }
}
