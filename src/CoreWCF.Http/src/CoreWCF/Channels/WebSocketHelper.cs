// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.WebSockets;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal static class WebSocketHelper
    {
        internal const int OperationNotStarted = 0;
        internal const int OperationFinished = 1;
        internal const string CloseOperation = "CloseOperation";
        internal const string SendOperation = "SendOperation";
        internal const string ReceiveOperation = "ReceiveOperation";
        internal static readonly char[] ProtocolSeparators = new char[] { ',' };
        internal static readonly HashSet<char> InvalidSeparatorSet = new HashSet<char>(new char[] { '(', ')', '<', '>', '@', ',', ';', ':', '\\', '"', '/', '[', ']', '?', '=', '{', '}', ' ' });
        private const string SchemeWs = "ws";
        private const string SchemeWss = "wss";

        internal static int GetReceiveBufferSize(long maxReceivedMessageSize)
        {
            int effectiveMaxReceiveBufferSize = maxReceivedMessageSize <= WebSocketDefaults.BufferSize ? (int)maxReceivedMessageSize : WebSocketDefaults.BufferSize;
            return Math.Max(WebSocketDefaults.MinReceiveBufferSize, effectiveMaxReceiveBufferSize);
        }

        internal static bool IsSubProtocolInvalid(string protocol, out string invalidChar)
        {
            Fx.Assert(protocol != null, "protocol should not be null");
            char[] chars = protocol.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char ch = chars[i];
                if (ch < 0x21 || ch > 0x7e)
                {
                    invalidChar = string.Format(CultureInfo.InvariantCulture, "[{0}]", (int)ch);
                    return true;
                }

                if (InvalidSeparatorSet.Contains(ch))
                {
                    invalidChar = ch.ToString();
                    return true;
                }
            }

            invalidChar = null;
            return false;
        }

        internal static void ThrowCorrectException(Exception ex)
        {
            throw ConvertAndTraceException(ex);
        }

        internal static void ThrowCorrectException(Exception ex, TimeSpan timeout, string operation)
        {
            throw ConvertAndTraceException(ex, timeout, operation);
        }

        internal static Exception ConvertAndTraceException(Exception ex)
        {
            return ConvertAndTraceException(
                    ex,
                    TimeSpan.MinValue, // this is a dummy since operation type is null, so the timespan value won't be used
                    null);
        }

        internal static Exception ConvertAndTraceException(Exception ex, TimeSpan timeout, string operation)
        {
            if (ex is ObjectDisposedException objectDisposedException)
            {
                var communicationObjectAbortedException = new CommunicationObjectAbortedException(ex.Message, ex);
                Fx.Exception.AsWarning(communicationObjectAbortedException);
                return communicationObjectAbortedException;
            }

            if (ex is AggregateException aggregationException)
            {
                Exception exception = Fx.Exception.AsError<OperationCanceledException>(aggregationException);
                if (exception is OperationCanceledException operationCanceledException)
                {
                    TimeoutException timeoutException = GetTimeoutException(exception, timeout, operation);
                    Fx.Exception.AsWarning(timeoutException);
                    return timeoutException;
                }
                else
                {
                    Exception communicationException = ConvertAggregateExceptionToCommunicationException(aggregationException);
                    if (communicationException is CommunicationObjectAbortedException)
                    {
                        Fx.Exception.AsWarning(communicationException);
                        return communicationException;
                    }
                    else
                    {
                        return Fx.Exception.AsError(communicationException);
                    }
                }
            }

            if (ex is WebSocketException webSocketException)
            {
                switch (webSocketException.WebSocketErrorCode)
                {
                    case WebSocketError.InvalidMessageType:
                    case WebSocketError.UnsupportedProtocol:
                    case WebSocketError.UnsupportedVersion:
                        ex = new ProtocolException(ex.Message, ex);
                        break;
                    default:
                        ex = new CommunicationException(ex.Message, ex);
                        break;
                }
            }

            return Fx.Exception.AsError(ex);
        }

        internal static Exception ConvertAggregateExceptionToCommunicationException(AggregateException ex)
        {
            Exception exception = Fx.Exception.AsError<WebSocketException>(ex);
            if (exception is WebSocketException webSocketException && webSocketException.InnerException != null)
            {
                // TODO: Find out is AspNetCore has some specific exception type they throw
                //HttpListenerException httpListenerException = webSocketException.InnerException as HttpListenerException;
                //if (httpListenerException != null)
                //{
                //    return HttpChannelUtilities.CreateCommunicationException(httpListenerException);
                //}
            }

            if (exception is ObjectDisposedException objectDisposedException)
            {
                return new CommunicationObjectAbortedException(exception.Message, exception);
            }

            return new CommunicationException(exception.Message, exception);
        }

        internal static void ThrowExceptionOnTaskFailure(Task task, TimeSpan timeout, string operation)
        {
            if (task.IsFaulted)
            {
                throw Fx.Exception.AsError<CommunicationException>(task.Exception);
            }
            else if (task.IsCanceled)
            {
                throw Fx.Exception.AsError(GetTimeoutException(null, timeout, operation));
            }
        }

        internal static TimeoutException GetTimeoutException(Exception innerException, TimeSpan timeout, string operation)
        {
            string errorMsg = string.Empty;
            if (operation != null)
            {
                switch (operation)
                {
                    case CloseOperation:
                        errorMsg = SR.Format(SR.CloseTimedOut, timeout);
                        break;
                    case SendOperation:
                        errorMsg = SR.Format(SR.WebSocketSendTimedOut, timeout);
                        break;
                    case ReceiveOperation:
                        errorMsg = SR.Format(SR.WebSocketReceiveTimedOut, timeout);
                        break;
                    default:
                        errorMsg = SR.Format(SR.WebSocketOperationTimedOut, operation, timeout);
                        break;
                }
            }

            return innerException == null ? new TimeoutException(errorMsg) : new TimeoutException(errorMsg, innerException);
        }

        internal static bool UseWebSocketTransport(WebSocketTransportUsage transportUsage, bool isContractDuplex)
        {
            return transportUsage == WebSocketTransportUsage.Always
                || (transportUsage == WebSocketTransportUsage.WhenDuplex && isContractDuplex);
        }

        internal static Uri GetWebSocketUri(Uri httpUri)
        {
            Fx.Assert(httpUri != null, "RemoteAddress.Uri should not be null.");
            UriBuilder builder = new UriBuilder(httpUri);

            if (Uri.UriSchemeHttp.Equals(httpUri.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                builder.Scheme = SchemeWs;
            }
            else
            {
                Fx.Assert(
                    Uri.UriSchemeHttps.Equals(httpUri.Scheme, StringComparison.OrdinalIgnoreCase),
                    "httpUri.Scheme should be http or https.");
                builder.Scheme = SchemeWss;
            }

            return builder.Uri;
        }
    }
}
