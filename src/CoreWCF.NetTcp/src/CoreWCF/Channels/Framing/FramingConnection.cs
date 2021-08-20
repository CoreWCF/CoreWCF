// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Runtime;
using CoreWCF.Security;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoreWCF.Channels.Framing
{
    public class FramingConnection
    {
        private readonly ConnectionContext _context;
        private IDuplexPipe _transport;

        public FramingConnection(ConnectionContext context) : this(context, NullLogger.Instance) { }

        // TODO: Make public in later version
        internal FramingConnection(ConnectionContext context, ILogger _logger)
        {
            _context = context;
            Logger = new ConnectionIdWrappingLogger(_logger, context.ConnectionId);
            //TODO: Add a public api mechanism to enable connection logging in RELEASE build
#if DEBUG
            Transport = RawTransport = new ExceptionConvertingDuplexPipe(new LoggingDuplexPipe(_context.Transport, Logger) { LoggingEnabled = true });
#else
            Transport = RawTransport = new ExceptionConvertingDuplexPipe(_context.Transport);
#endif
            RemoteEndpoint = GetRemoteEndPoint(context);
        }

        public MessageEncoderFactory MessageEncoderFactory { get; internal set; }
        public StreamUpgradeAcceptor StreamUpgradeAcceptor { get; internal set; }
        public ISecurityCapabilities SecurityCapabilities { get; internal set; }
        public IServiceDispatcher ServiceDispatcher { get; internal set; }
        public PipeReader Input => Transport.Input;
        public PipeWriter Output => Transport.Output;
        public IDuplexPipe Transport { get; set; }
        public IDuplexPipe RawTransport { get; private set; }
        internal FramingDecoder FramingDecoder { get; set; }
        public Uri Via => FramingDecoder?.Via;
        internal FramingMode FramingMode { get; set; }
        public MessageEncoder MessageEncoder { get; internal set; }
        public SecurityMessageProperty SecurityMessageProperty
        {
            get;
            internal set;
        }
        public bool EOF { get; internal set; }
        public Memory<byte> EnvelopeBuffer { get; internal set; }
        public int EnvelopeOffset { get; internal set; }
        public BufferManager BufferManager { get; internal set; }
        public int EnvelopeSize { get; internal set; }
        public int MaxReceivedMessageSize { get; internal set; }
        public int MaxBufferSize { get; internal set; }
        public int ConnectionBufferSize { get; internal set; }
        public TransferMode TransferMode { get; internal set; }
        internal Stream RawStream { get; set; }
        public ILogger Logger { get; }
        public IPEndPoint RemoteEndpoint { get; }

        internal void Reset()
        {
            MessageEncoderFactory = null;
            StreamUpgradeAcceptor = null;
            SecurityCapabilities = null;
            ServiceDispatcher = null;
            Transport = RawTransport;
            FramingDecoder = null;
            FramingMode = default;
            MessageEncoder = null;
            SecurityMessageProperty = null;
            EOF = false;
            EnvelopeBuffer = null;
            EnvelopeOffset = 0;
            BufferManager = null;
            EnvelopeSize = 0;
            MaxReceivedMessageSize = 0;
            MaxBufferSize = 0;
            ConnectionBufferSize = 0;
            TransferMode = default;
            RawStream = null;
        }

        public void Abort() { _context.Abort(); }
        public void Abort(Exception e) { _context.Abort(new ConnectionAbortedException(e.Message, e)); }
        public void Abort(string reason) { _context.Abort(new ConnectionAbortedException(reason)); }

        public Task CloseAsync(TimeSpan timeout)
        {
            // Closing should be async and should accept a timeout. There are improvements coming in future releases of .NET Core which support this.
            Input.Complete();
            Output.Complete();
            return Task.CompletedTask;
        }

        public async Task SendFaultAsync(string faultString, TimeSpan sendTimeout, int maxRead)
        {
            //if (TD.ConnectionReaderSendFaultIsEnabled())
            //{
            //    TD.ConnectionReaderSendFault(faultString);
            //}
            var encodedFault = new EncodedFault(faultString);
            var timeoutHelper = new TimeoutHelper(sendTimeout);
            System.Threading.CancellationToken ct = timeoutHelper.GetCancellationToken();
            try
            {
                await Output.WriteAsync(encodedFault.EncodedBytes, ct);
                await Output.FlushAsync();
                // Connection will be closed on completion of Task returned from NetMessageFramingConnectionHandler.OnConnectedAsync
            }
            catch (CommunicationException e) // TODO: Consider exception filters to remvoe duplicate code
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                Abort(e);
                return;
            }
            catch (OperationCanceledException e)
            {
                //if (TD.SendTimeoutIsEnabled())
                //{
                //    TD.SendTimeout(e.Message);
                //}
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                Abort(e);
                return;
            }
            catch (TimeoutException e)
            {
                //if (TD.SendTimeoutIsEnabled())
                //{
                //    TD.SendTimeout(e.Message);
                //}
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                Abort(e);
                return;
            }

            // make sure we read until EOF or a quota is hit
            ReadResult readResult;
            long readTotal = 0;
            for (; ; )
            {
                try
                {
                    readResult = await Input.ReadAsync(ct);
                }
                catch (CommunicationException e) // TODO: Exception filters?
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    Abort(e);
                    return;
                }
                // TODO: Standardize handling of OperationCanceledException/TimeoutException
                catch (OperationCanceledException e)
                {
                    //if (TD.SendTimeoutIsEnabled())
                    //{
                    //    TD.SendTimeout(e.Message);
                    //}
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    Abort(e);
                    return;
                }
                catch (TimeoutException e)
                {
                    //if (TD.SendTimeoutIsEnabled())
                    //{
                    //    TD.SendTimeout(e.Message);
                    //}
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    Abort(e);
                    return;
                }

                if (readResult.IsCompleted)
                {
                    break;
                }

                readTotal += readResult.Buffer.Length;
                Input.AdvanceTo(readResult.Buffer.End);
                if (readTotal > maxRead || timeoutHelper.RemainingTime() <= TimeSpan.Zero)
                {
                    Abort();
                    return;
                }
            }
        }

        /// <summary>
        /// Tries to extract the remote endpoint from the given <see cref="ConnectionContext"/> in a
        /// version agnostic way.
        /// </summary>
        /// <param name="context">The ASP.net core connection context to extract the remote endpoint from.</param>
        /// <returns>The endpoint of the remote party or null if it was not provided.</returns>
        private static IPEndPoint GetRemoteEndPoint(ConnectionContext context)
        {
            // 1st chance: Server might provide remote endpoint via HTTP feature
            // (mostly the case in ASP.net core v2.x)
            IHttpConnectionFeature connectionFeature = context.Features.Get<IHttpConnectionFeature>();
            if (connectionFeature != null)
            {
                return new IPEndPoint(connectionFeature.RemoteIpAddress, connectionFeature.RemotePort);
            }

            // 2nd chance: on ASP.net core 5.0 the ConnectionContext has a direct Property RemoteEndpoint
            // via baseclass.
            var net5RemoteEndPointPropertyAccessor = s_net5RemoteEndPointPropertyAccessor;
            if (net5RemoteEndPointPropertyAccessor != null)
            {
                return net5RemoteEndPointPropertyAccessor(context);
            }

            // last chance: server does likely not support access to remote endpoint. could be
            // a non-tcp server like the ASP.net core test server
            return null;
        }


        private static readonly Func<ConnectionContext, IPEndPoint> s_net5RemoteEndPointPropertyAccessor =
            BuildNet5RemoteEndPointPropertyAccessor();

        private static Func<ConnectionContext, IPEndPoint> BuildNet5RemoteEndPointPropertyAccessor()
        {
            // https://github.com/dotnet/aspnetcore/blob/v5.0.9/src/Servers/Connections.Abstractions/src/BaseConnectionContext.cs
            var property =
                typeof(ConnectionContext).GetProperty("RemoteEndPoint", BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
            {
                return null;
            }

            // context => context.RemoteEndPoint as IPEndpoint
            var contextParam = Expression.Parameter(typeof(ConnectionContext), "context");
            return Expression.Lambda<Func<ConnectionContext, IPEndPoint>>(
                 Expression.TypeAs(Expression.Property(contextParam, property),  typeof(IPEndPoint)),
                contextParam
            ).Compile();
        }

        private class ConnectionIdWrappingLogger : ILogger
        {
            private ILogger _innerLogger;
            private string _connectionId;

            public ConnectionIdWrappingLogger(ILogger innerLogger, string connectionId)
            {
                _innerLogger = innerLogger;
                _connectionId = connectionId;
            }
            public IDisposable BeginScope<TState>(TState state) => _innerLogger.BeginScope(state);

            public bool IsEnabled(LogLevel logLevel) => _innerLogger.IsEnabled(logLevel);

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                (TState state, string connectionId, Func<TState, Exception, string> origFormatter) newState = (state, _connectionId, formatter);
                _innerLogger.Log(logLevel, eventId, newState, exception, ConnectionIdFormatter<TState>);
            }

            private static string ConnectionIdFormatter<TState>((TState state, string connectionId, Func<TState, Exception, string> formatter) modifiedState, Exception exception)
            {
                var state = modifiedState.state;
                var connectionId = modifiedState.connectionId;
                var formatter = modifiedState.formatter;
                var formattedString = formatter(state, exception);
                return $"[{connectionId}] {formattedString}";
            }
        }
    }

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
