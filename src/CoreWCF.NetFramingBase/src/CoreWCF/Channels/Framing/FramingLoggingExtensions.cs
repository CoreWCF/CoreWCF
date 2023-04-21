// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO.Pipelines;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels.Framing
{
    internal static class FramingLoggingExtensions
    {
        // Convention is for paired events (eg Start/Stop) to have sequential numbers with the start even being even and the end event being odd.
        // Paired events use event id's in the 1000-1999 range. Solo events start at 2000
#region PairedEvents
        private static Action<ILogger, string, Exception> s_serverModeDecoderStartState = LoggerMessage.Define<string>(
            LogLevel.Trace,
            new EventId(1000, nameof(ServerModeDecoder) + "StartState"),
            nameof(ServerModeDecoder) + ":Start state is {decoderState}");

        private static Action<ILogger, string, int, Exception> s_serverModeDecoderEndState = LoggerMessage.Define<string, int>(
            LogLevel.Trace,
            new EventId(1001, nameof(ServerModeDecoder) + "EndState"),
            nameof(ServerModeDecoder) + ":End state is {decoderState} after reading {bytesRead} bytes");

        private static Action<ILogger, string, Exception> s_serverSessionDecoderStartState = LoggerMessage.Define<string>(
            LogLevel.Trace,
            new EventId(1002, nameof(ServerSessionDecoder) + "StartState"),
            nameof(ServerSessionDecoder) + ":Start state is {decoderState}");

        private static Action<ILogger, string, int, Exception> s_serverSessionDecoderEndState = LoggerMessage.Define<string, int>(
            LogLevel.Trace,
            new EventId(1003, nameof(ServerSessionDecoder) + "EndState"),
            nameof(ServerSessionDecoder) + ":End state is {decoderState} after reading {bytesRead} bytes");

        private static Action<ILogger, string, Exception> s_singletonMessageDecoderStartState = LoggerMessage.Define<string>(
            LogLevel.Trace,
            new EventId(1004, nameof(SingletonMessageDecoder) + "StartState"),
            nameof(SingletonMessageDecoder) + ":Start state is {decoderState}");

        private static Action<ILogger, string, int, Exception> s_singletonMessageDecoderEndState = LoggerMessage.Define<string, int>(
            LogLevel.Trace,
            new EventId(1005, nameof(SingletonMessageDecoder) + "EndState"),
            nameof(SingletonMessageDecoder) + ":End state is {decoderState} after reading {bytesRead} bytes");

        private static Action<ILogger, string, Exception> s_serverSingletonDecoderStartState = LoggerMessage.Define<string>(
            LogLevel.Trace,
            new EventId(1006, nameof(ServerSingletonDecoder) + "StartState"),
            nameof(ServerSingletonDecoder) + ":Start state is {decoderState}");

        private static Action<ILogger, string, int, Exception> s_serverSingletonDecoderEndState = LoggerMessage.Define<string, int>(
            LogLevel.Trace,
            new EventId(1007, nameof(ServerSingletonDecoder) + "EndState"),
            nameof(ServerSingletonDecoder) + ":End state is {decoderState} after reading {bytesRead} bytes");

        private static Action<ILogger, string, Exception> s_serverSingletonSizedDecoderStartState = LoggerMessage.Define<string>(
            LogLevel.Trace,
            new EventId(1008, nameof(ServerSingletonSizedDecoder) + "StartState"),
            nameof(ServerSingletonSizedDecoder) + ":Start state is {decoderState}");

        private static Action<ILogger, string, int, Exception> s_serverSingletonSizedDecoderEndState = LoggerMessage.Define<string, int>(
            LogLevel.Trace,
            new EventId(1009, nameof(ServerSingletonSizedDecoder) + "EndState"),
            nameof(ServerSingletonSizedDecoder) + ":End state is {decoderState} after reading {bytesRead} bytes");

        private static Action<ILogger, string, Exception> s_stringDecoderStartState = LoggerMessage.Define<string>(
            LogLevel.Trace,
            new EventId(1010, nameof(StringDecoder) + "StartState"),
            nameof(StringDecoder) + ":Start state is {decoderState}");

        private static Action<ILogger, string, int, Exception> s_stringDecoderEndState = LoggerMessage.Define<string, int>(
            LogLevel.Trace,
            new EventId(1011, nameof(StringDecoder) + "EndState"),
            nameof(StringDecoder) + ":End state is {decoderState} after reading {bytesRead} bytes");

        private static Action<ILogger, string, Exception> s_startStreamUpgradeAccept = LoggerMessage.Define<string>(
            LogLevel.Trace,
            new EventId(1012, nameof(StreamUpgradeAcceptor) + "StartUpgrade"),
            "{upgradeAcceptorType} start stream upgrade");

        private static Action<ILogger, string, Exception> s_completeStreamUpgradeAccept = LoggerMessage.Define<string>(
            LogLevel.Trace,
            new EventId(1013, nameof(StreamUpgradeAcceptor) + "CompleteUpgrade"),
            "{upgradeAcceptorType} stream upgrade completed");

        private static Action<ILogger, Exception> s_startPendingReadOnIdleSocket = LoggerMessage.Define(
            LogLevel.Trace,
            new EventId(1014, "StartPendingReadIdleSocket"),
            "Starting pending read on idle socket");

        private static Action<ILogger, bool, bool, long, Exception> s_endPendingReadOnIdleSocket = LoggerMessage.Define<bool, bool, long>(
            LogLevel.Trace,
            new EventId(1014, "EndPendingReadIdleSocket"),
            "Pending read on idle socket completed, IsCompleted: {isCompleted}, IsCanceled: {isCanceled}, bytes received: {bytesReceived}");

#endregion // PairedEvents

        private static Action<ILogger, int, int, int, Exception> s_decodingInt = LoggerMessage.Define<int, int, int>(
            LogLevel.Trace,
            new EventId(1050, nameof(IntDecoder) + "DecodingValue"),
            nameof(IntDecoder) + ":Decoding Int with  next {next} at index {index}, with total so far {value}");

        private static Action<ILogger, string, Exception> s_stringDecoded = LoggerMessage.Define<string>(
            LogLevel.Trace,
            new EventId(1051, nameof(StringDecoder) + "DecodedString"),
            nameof(StringDecoder) + ":String decoded: \"{decodedString}\"");

        private static Action<ILogger, Exception> s_unwrappingRawStream = LoggerMessage.Define(
            LogLevel.Trace,
            new EventId(1052, "UnwrappingRawStream"),
            "Unwrapping raw stream");

        private static Action<ILogger, Exception> s_connectionPoolFull = LoggerMessage.Define(
            LogLevel.Trace,
            new EventId(1053, "RawSocketClose"),
            "Connection pool full, closing raw socket");

        private static Action<ILogger, Exception> s_failureInConnectionReuse = LoggerMessage.Define(
            LogLevel.Trace,
            new EventId(1054, "ConnectionReuseFailure"),
            "Failed to reuse connection");

        private static Action<ILogger, Exception> s_idleConnectionClosed = LoggerMessage.Define(
            LogLevel.Trace,
            new EventId(1055, "IdleConnectionClosed"),
            "Idle connection closed when waiting for reuse");

        private static Action<ILogger, Exception> s_receivedNullMessage = LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(1056, "ReceivedNullMessage"),
            "Null message received by transport");

        private static Action<ILogger, string, string, string, Exception> s_receivedMessage = LoggerMessage.Define<string, string, string>(
            LogLevel.Trace,
            new EventId(1057, "ReceivedMessage"),
            "Received message with Id \"{id}\" with action \"{action}\" to \"{toAddress}\"");

        private static Action<ILogger, string, string, string, string, Exception> s_sendMessage = LoggerMessage.Define<string, string, string, string>(
            LogLevel.Trace,
            new EventId(1058, "SendMessage"),
            "Sending message with RelatesTo Id \"{relatesToId}\" Id \"{id}\" with action \"{action}\" to \"{toAddress}\"");

        private static Action<ILogger, string, int, string, Exception> s_connectionLogging = LoggerMessage.Define<string, int, string>(
            LogLevel.Trace,
            new EventId(1059, "ConnectionLogging"),
            "{method}[{byteCount}]{data}");

        public static void LogStartState(this ILogger logger, ServerModeDecoder serverModeDecoder)
        {
            s_serverModeDecoderStartState(logger, serverModeDecoder.CurrentState.ToString(), null);
        }

        public static void LogEndState(this ILogger logger, ServerModeDecoder serverModeDecoder, int bytesRead)
        {
            s_serverModeDecoderEndState(logger, serverModeDecoder.CurrentState.ToString(), bytesRead, null);
        }

        public static void LogStartState(this ILogger logger, ServerSessionDecoder serverSessionDecoder)
        {
            s_serverSessionDecoderStartState(logger, serverSessionDecoder.CurrentState.ToString(), null);
        }

        public static void LogEndState(this ILogger logger, ServerSessionDecoder serverSessionDecoder, int bytesRead)
        {
            s_serverSessionDecoderEndState(logger, serverSessionDecoder.CurrentState.ToString(), bytesRead, null);
        }

        public static void LogStartState(this ILogger logger, SingletonMessageDecoder singletonMessageDecoder)
        {
            s_singletonMessageDecoderStartState(logger, singletonMessageDecoder.CurrentState.ToString(), null);
        }

        public static void LogEndState(this ILogger logger, SingletonMessageDecoder singletonMessageDecoder, int bytesRead)
        {
            s_singletonMessageDecoderEndState(logger, singletonMessageDecoder.CurrentState.ToString(), bytesRead, null);
        }

        public static void LogStartState(this ILogger logger, ServerSingletonDecoder serverSingletonDecoder)
        {
            s_serverSingletonDecoderStartState(logger, serverSingletonDecoder.CurrentState.ToString(), null);
        }

        public static void LogEndState(this ILogger logger, ServerSingletonDecoder serverSingletonDecoder, int bytesRead)
        {
            s_serverSingletonDecoderEndState(logger, serverSingletonDecoder.CurrentState.ToString(), bytesRead, null);
        }

        public static void LogStartState(this ILogger logger, ServerSingletonSizedDecoder serverSingletonSizedDecoder)
        {
            s_serverSingletonSizedDecoderStartState(logger, serverSingletonSizedDecoder.CurrentState.ToString(), null);
        }

        public static void LogEndState(this ILogger logger, ServerSingletonSizedDecoder serverSingletonSizedDecoder, int bytesRead)
        {
            s_serverSingletonSizedDecoderEndState(logger, serverSingletonSizedDecoder.CurrentState.ToString(), bytesRead, null);
        }

        public static void DecodingInt(this ILogger logger, int next, int index, int value)
        {
            s_decodingInt(logger, next, index, value, null);
        }

        public static void LogStartState(this ILogger logger, StringDecoder stringDecoder)
        {
            s_stringDecoderStartState(logger, stringDecoder.CurrentState.ToString(), null);
        }

        public static void LogEndState(this ILogger logger, StringDecoder stringDecoder, int bytesRead)
        {
            s_stringDecoderEndState(logger, stringDecoder.CurrentState.ToString(), bytesRead, null);
        }

        public static void StringDecoded(this ILogger logger, string decodedString)
        {
            s_stringDecoded(logger, decodedString, null);
        }

        public static void StartStreamUpgradeAccept(this ILogger logger, StreamUpgradeAcceptor upgradeAcceptor)
        {
            s_startStreamUpgradeAccept(logger, upgradeAcceptor.GetType().Name, null);
        }

        public static void CompleteStreamUpgradeAccept(this ILogger logger, StreamUpgradeAcceptor upgradeAcceptor)
        {
            s_completeStreamUpgradeAccept(logger, upgradeAcceptor.GetType().Name, null);
        }

        public static void UnwrappingRawStream(this ILogger logger)
        {
            s_unwrappingRawStream(logger, null);
        }

        public static void ConnectionPoolFull(this ILogger logger)
        {
            s_connectionPoolFull(logger, null);
        }

        public static void StartPendingReadOnIdleSocket(this ILogger logger)
        {
            s_startPendingReadOnIdleSocket(logger, null);
        }

        public static void EndPendingReadOnIdleSocket(this ILogger logger, ReadResult readResult)
        {
            s_endPendingReadOnIdleSocket(logger, readResult.IsCompleted, readResult.IsCanceled, readResult.Buffer.Length, null);
        }

        public static void FailureInConnectionReuse(this ILogger logger, Exception e)
        {
            s_failureInConnectionReuse(logger, e);
        }

        public static void IdleConnectionClosed(this ILogger logger)
        {
            s_idleConnectionClosed(logger, null);
        }

        public static void ReceivedMessage(this ILogger logger, Message message)
        {
            if (message == null)
                s_receivedNullMessage(logger, null);
            else
                s_receivedMessage(logger, message.Headers.MessageId?.ToString(), message.Headers.Action?.ToString(), message.Headers.To?.ToString(), null);
        }

        public static void SendMessage(this ILogger logger, Message message)
        {
            s_sendMessage(logger, message.Headers.RelatesTo?.ToString(), message.Headers.MessageId?.ToString(), message.Headers.Action?.ToString(), message.Headers.To?.ToString(), null);
        }

        public static void LogBytes(this ILogger logger, string method, int count, string data)
        {
            s_connectionLogging(logger, method, count, data, null);
        }
    }
}
