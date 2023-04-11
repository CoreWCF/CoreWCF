// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using CoreWCF.Channels;

namespace CoreWCF
{
    internal static class ProtocolExceptionHelper
    {
        internal static ProtocolException ReceiveShutdownReturnedNonNull(Message message)
        {
            if (message.IsFault)
            {
                try
                {
                    MessageFault fault = MessageFault.CreateFault(message, 64 * 1024);
                    FaultReasonText reason = fault.Reason.GetMatchingTranslation(CultureInfo.CurrentCulture);
                    string text = SRCommon.Format(SRCommon.ReceiveShutdownReturnedFault, reason.Text);
                    return new ProtocolException(text);
                }
                catch (QuotaExceededException)
                {
                    string text = SRCommon.Format(SRCommon.ReceiveShutdownReturnedLargeFault, message.Headers.Action);
                    return new ProtocolException(text);
                }
            }
            else
            {
                string text = SRCommon.Format(SRCommon.ReceiveShutdownReturnedMessage, message.Headers.Action);
                return new ProtocolException(text);
            }
        }
    }
}
