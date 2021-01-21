// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using CoreWCF.Security;
using Microsoft.AspNetCore.Http;

namespace CoreWCF.Channels
{
    internal abstract class WebSocketTransportDuplexSessionChannel : TransportDuplexSessionChannel
    {
        private WebSocket _webSocket = null;
        private int _cleanupStatus = WebSocketHelper.OperationNotStarted;
        private readonly WebSocketCloseDetails _webSocketCloseDetails = new WebSocketCloseDetails();
        private bool _shouldDisposeWebSocketAfterClosed = true;
        private readonly Exception _pendingWritingMessageException;

        public WebSocketTransportDuplexSessionChannel(IHttpTransportFactorySettings settings, EndpointAddress localAddress, Uri localVia)
            : base(settings, localAddress, localVia, EndpointAddress.AnonymousAddress, settings.MessageVersion.Addressing.AnonymousUri)
        {
            Fx.Assert(settings.WebSocketSettings != null, "IHttpTransportFactorySettings.WebSocketTransportSettings should not be null.");
            WebSocketSettings = settings.WebSocketSettings;
            TransferMode = settings.TransferMode;
            MaxBufferSize = settings.MaxBufferSize;
            TransportFactorySettings = settings;
        }

        protected WebSocket WebSocket
        {
            get
            {
                return _webSocket;
            }

            set
            {
                Fx.Assert(value != null, "value should not be null.");
                Fx.Assert(_webSocket == null, "webSocket should not be set before this set call.");
                _webSocket = value;
            }
        }

        protected WebSocketTransportSettings WebSocketSettings { get; }

        protected TransferMode TransferMode { get; }

        protected int MaxBufferSize { get; }

        protected ITransportFactorySettings TransportFactorySettings { get; }

        protected bool ShouldDisposeWebSocketAfterClosed
        {
            set
            {
                _shouldDisposeWebSocketAfterClosed = value;
            }
        }

        protected override void OnAbort()
        {
            //if (TD.WebSocketConnectionAbortedIsEnabled())
            //{
            //    TD.WebSocketConnectionAborted(
            //        this.EventTraceActivity,
            //        this.WebSocket != null ? this.WebSocket.GetHashCode() : -1);
            //}

            Cleanup();
        }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IWebSocketCloseDetails))
            {
                return _webSocketCloseDetails as T;
            }

            return base.GetProperty<T>();
        }

        protected override async Task CompleteCloseAsync(CancellationToken token)
        {
            //if (TD.WebSocketCloseSentIsEnabled())
            //{
            //    TD.WebSocketCloseSent(
            //        this.WebSocket.GetHashCode(),
            //        this.webSocketCloseDetails.OutputCloseStatus.ToString(),
            //        this.RemoteAddress != null ? this.RemoteAddress.ToString() : string.Empty);
            //}

            try
            {
                await CloseInternalAsync(token);
            }
            catch (Exception ex)
            {
                if (Fx.IsFatal(ex))
                {
                    throw;
                }

                WebSocketHelper.ThrowCorrectException(ex, TimeoutHelper.GetOriginalTimeout(token), WebSocketHelper.CloseOperation);
            }

            //if (TD.WebSocketConnectionClosedIsEnabled())
            //{
            //    TD.WebSocketConnectionClosed(this.WebSocket.GetHashCode());
            //}
        }

        protected override async Task CloseOutputSessionCoreAsync(CancellationToken token)
        {
            //if (TD.WebSocketCloseOutputSentIsEnabled())
            //{
            //    TD.WebSocketCloseOutputSent(
            //        this.WebSocket.GetHashCode(),
            //        this.webSocketCloseDetails.OutputCloseStatus.ToString(),
            //        this.RemoteAddress != null ? this.RemoteAddress.ToString() : string.Empty);
            //}
            try
            {
                await CloseOutputAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                if (Fx.IsFatal(ex))
                {
                    throw;
                }

                WebSocketHelper.ThrowCorrectException(ex, TimeoutHelper.GetOriginalTimeout(token), WebSocketHelper.CloseOperation);
            }
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            try
            {
                await base.OnCloseAsync(token);
            }
            finally
            {
                Cleanup();
            }
        }

        protected override void ReturnConnectionIfNecessary(bool abort, CancellationToken token)
        {
        }

        protected override Task CloseOutputAsync(CancellationToken token)
        {
            Task task = CloseOutputImplAsync(token);
            return task.ContinueWith(t =>
            {
                try
                {
                    WebSocketHelper.ThrowExceptionOnTaskFailure(t, TimeoutHelper.GetOriginalTimeout(token), WebSocketHelper.CloseOperation);
                }
                catch (Exception error)
                {
                    Fx.Exception.TraceHandledException(error, TraceEventType.Information);
                    throw;
                }
            });
        }

        protected override async Task OnSendCoreAsync(Message message, CancellationToken token)
        {
            Fx.Assert(message != null, "message should not be null.");

            WebSocketMessageType outgoingMessageType = GetWebSocketMessageType(message);
            if (IsStreamedOutput)
            {
                WebSocketStream webSocketStream = new WebSocketStream(WebSocket, outgoingMessageType, token);
                TimeoutStream timeoutStream = new TimeoutStream(webSocketStream, token);
                await MessageEncoder.WriteMessageAsync(message, timeoutStream);
                await webSocketStream.WriteEndOfMessageAsync(token);
            }
            else
            {
                ArraySegment<byte> messageData = EncodeMessage(message);
                bool success = false;
                try
                {
                    //if (TD.WebSocketAsyncWriteStartIsEnabled())
                    //{
                    //    TD.WebSocketAsyncWriteStart(
                    //        this.WebSocket.GetHashCode(),
                    //        messageData.Count,
                    //        this.RemoteAddress != null ? this.RemoteAddress.ToString() : string.Empty);
                    //}

                    try
                    {
                        await WebSocket.SendAsync(messageData, outgoingMessageType, true, token);
                    }
                    catch (Exception ex)
                    {
                        if (Fx.IsFatal(ex))
                        {
                            throw;
                        }

                        WebSocketHelper.ThrowCorrectException(ex, TimeoutHelper.GetOriginalTimeout(token), WebSocketHelper.SendOperation);
                    }

                    //if (TD.WebSocketAsyncWriteStopIsEnabled())
                    //{
                    //    TD.WebSocketAsyncWriteStop(this.webSocket.GetHashCode());
                    //}

                    success = true;
                }
                finally
                {
                    try
                    {
                        BufferManager.ReturnBuffer(messageData.Array);
                    }
                    catch (Exception ex)
                    {
                        if (Fx.IsFatal(ex) || success)
                        {
                            throw;
                        }

                        Fx.Exception.TraceUnhandledException(ex);
                    }
                }
            }
        }

        protected override ArraySegment<byte> EncodeMessage(Message message)
        {
            return MessageEncoder.WriteMessage(message, int.MaxValue, BufferManager, 0);
        }

        protected void Cleanup()
        {
            if (Interlocked.CompareExchange(ref _cleanupStatus, WebSocketHelper.OperationFinished, WebSocketHelper.OperationNotStarted) == WebSocketHelper.OperationNotStarted)
            {
                OnCleanup();
            }
        }

        protected virtual void OnCleanup()
        {
            Fx.Assert(_cleanupStatus == WebSocketHelper.OperationFinished,
                "This method should only be called by this.Cleanup(). Make sure that you never call overriden OnCleanup()-methods directly in subclasses");
            if (_shouldDisposeWebSocketAfterClosed && _webSocket != null)
            {
                _webSocket.Dispose();
            }
        }

        private static void ThrowOnPendingException(ref Exception pendingException)
        {
            Exception exceptionToThrow = pendingException;

            if (exceptionToThrow != null)
            {
                pendingException = null;
                throw Fx.Exception.AsError(exceptionToThrow);
            }
        }

        private Task CloseInternalAsync(CancellationToken token)
        {
            try
            {
                return WebSocket.CloseAsync(_webSocketCloseDetails.OutputCloseStatus, _webSocketCloseDetails.OutputCloseStatusDescription, token);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                throw WebSocketHelper.ConvertAndTraceException(e);
            }
        }

        private Task CloseOutputImplAsync(CancellationToken cancellationToken)
        {
            try
            {
                return WebSocket.CloseOutputAsync(_webSocketCloseDetails.OutputCloseStatus, _webSocketCloseDetails.OutputCloseStatusDescription, cancellationToken);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                throw WebSocketHelper.ConvertAndTraceException(e);
            }
        }

        private static WebSocketMessageType GetWebSocketMessageType(Message message)
        {
            WebSocketMessageType outgoingMessageType = WebSocketDefaults.DefaultWebSocketMessageType;
            if (WebSocketMessageProperty.TryGet(message.Properties, out WebSocketMessageProperty webSocketMessageProperty))
            {
                outgoingMessageType = webSocketMessageProperty.MessageType;
            }

            return outgoingMessageType;
        }

        protected class WebSocketMessageSource : IMessageSource
        {
            private static readonly Action<object> onAsyncReceiveCancelled = Fx.ThunkCallback<object>(OnAsyncReceiveCancelled);
            private MessageEncoder encoder;
            private BufferManager bufferManager;
            private EndpointAddress localAddress;
            private Message pendingMessage;
            private Exception pendingException;
            private readonly WebSocketContext context;
            private WebSocket webSocket;
            private bool closureReceived = false;
            private bool useStreaming;
            private int receiveBufferSize;
            private int maxBufferSize;
            private long maxReceivedMessageSize;
            private TaskCompletionSource<object> streamWaitTask;
            private IDefaultCommunicationTimeouts defaultTimeouts;
            private readonly RemoteEndpointMessageProperty remoteEndpointMessageProperty;
            private SecurityMessageProperty handshakeSecurityMessageProperty;
            private WebSocketCloseDetails closeDetails;
            private readonly ReadOnlyDictionary<string, object> properties;
            private TimeSpan asyncReceiveTimeout;
            private TaskCompletionSource<object> receiveTask;
            private IOThreadTimer receiveTimer;
            private int asyncReceiveState;

            public WebSocketMessageSource(WebSocketTransportDuplexSessionChannel webSocketTransportDuplexSessionChannel, WebSocket webSocket,
                    bool useStreaming, IDefaultCommunicationTimeouts defaultTimeouts)
            {
                Initialize(webSocketTransportDuplexSessionChannel, webSocket, useStreaming, defaultTimeouts);

                StartNextReceiveAsync();
            }

            public WebSocketMessageSource(WebSocketTransportDuplexSessionChannel webSocketTransportDuplexSessionChannel, WebSocketContext context,
                bool isStreamed, RemoteEndpointMessageProperty remoteEndpointMessageProperty, IDefaultCommunicationTimeouts defaultTimeouts, HttpContext requestContext)
            {
                Initialize(webSocketTransportDuplexSessionChannel, context.WebSocket, isStreamed, defaultTimeouts);

                IPrincipal user = requestContext == null ? null : requestContext.User;
                this.context = new ServiceWebSocketContext(context, user);
                this.remoteEndpointMessageProperty = remoteEndpointMessageProperty;
                // Copy any string keyed items from requestContext to properties. This is an attempt to mimic HttpRequestMessage.Properties
                var properties = new Dictionary<string, object>();
                foreach (var kv in requestContext.Items)
                {
                    if (kv.Key is string key)
                    {
                        properties[key] = kv.Value;
                    }
                }

                this.properties = requestContext == null ? null : new ReadOnlyDictionary<string, object>(properties);

                StartNextReceiveAsync();
            }

            private void Initialize(WebSocketTransportDuplexSessionChannel webSocketTransportDuplexSessionChannel, WebSocket webSocket, bool useStreaming, IDefaultCommunicationTimeouts defaultTimeouts)
            {
                this.webSocket = webSocket;
                encoder = webSocketTransportDuplexSessionChannel.MessageEncoder;
                bufferManager = webSocketTransportDuplexSessionChannel.BufferManager;
                localAddress = webSocketTransportDuplexSessionChannel.LocalAddress;
                maxBufferSize = webSocketTransportDuplexSessionChannel.MaxBufferSize;
                handshakeSecurityMessageProperty = webSocketTransportDuplexSessionChannel.RemoteSecurity;
                maxReceivedMessageSize = webSocketTransportDuplexSessionChannel.TransportFactorySettings.MaxReceivedMessageSize;
                receiveBufferSize = Math.Min(WebSocketHelper.GetReceiveBufferSize(maxReceivedMessageSize), maxBufferSize);
                this.useStreaming = useStreaming;
                this.defaultTimeouts = defaultTimeouts;
                closeDetails = webSocketTransportDuplexSessionChannel._webSocketCloseDetails;
                receiveTimer = new IOThreadTimer(onAsyncReceiveCancelled, this, true);
                asyncReceiveState = AsyncReceiveState.Finished;
            }

            internal RemoteEndpointMessageProperty RemoteEndpointMessageProperty
            {
                get { return remoteEndpointMessageProperty; }
            }

            private static void OnAsyncReceiveCancelled(object target)
            {
                WebSocketMessageSource messageSource = (WebSocketMessageSource)target;
                messageSource.AsyncReceiveCancelled();
            }

            private void AsyncReceiveCancelled()
            {
                if (Interlocked.CompareExchange(ref asyncReceiveState, AsyncReceiveState.Cancelled, AsyncReceiveState.Started) == AsyncReceiveState.Started)
                {
                    receiveTask.SetResult(null);
                }
            }

            public async Task<Message> ReceiveAsync(CancellationToken token)
            {
                if (!receiveTask.Task.IsCompleted)
                {
                    using (token.Register(() => AsyncReceiveCancelled()))
                    {
                        await receiveTask.Task;
                    }
                }

                if (asyncReceiveState == AsyncReceiveState.Cancelled)
                {
                    throw Fx.Exception.AsError(WebSocketHelper.GetTimeoutException(null, TimeoutHelper.GetOriginalTimeout(token), WebSocketHelper.ReceiveOperation));
                }
                else
                {
                    Fx.Assert(asyncReceiveState == AsyncReceiveState.Finished, "this.asyncReceiveState is not AsyncReceiveState.Finished: " + asyncReceiveState);
                    Message message = GetPendingMessage();

                    if (message != null)
                    {
                        // If we get any exception thrown out before that, the channel will be aborted thus no need to maintain the receive loop here.
                        StartNextReceiveAsync();
                    }

                    return message;
                }
            }

            public void UpdateOpenNotificationMessageProperties(MessageProperties messageProperties)
            {
                AddMessageProperties(messageProperties, WebSocketDefaults.DefaultWebSocketMessageType);
            }

            private async Task ReadBufferedMessageAsync()
            {
                byte[] internalBuffer = null;
                try
                {
                    internalBuffer = bufferManager.TakeBuffer(receiveBufferSize);

                    int receivedByteCount = 0;
                    bool endOfMessage = false;
                    WebSocketReceiveResult result = null;
                    do
                    {
                        try
                        {

                            //if (TD.WebSocketAsyncReadStartIsEnabled())
                            //{
                            //    TD.WebSocketAsyncReadStart(this.webSocket.GetHashCode());
                            //}

                            Task<WebSocketReceiveResult> receiveTask = webSocket.ReceiveAsync(
                                                            new ArraySegment<byte>(internalBuffer, receivedByteCount, internalBuffer.Length - receivedByteCount),
                                                            CancellationToken.None);

                            await receiveTask.ConfigureAwait(false);

                            result = receiveTask.Result;
                            CheckCloseStatus(result);
                            endOfMessage = result.EndOfMessage;

                            receivedByteCount += result.Count;
                            if (receivedByteCount >= internalBuffer.Length && !result.EndOfMessage)
                            {
                                if (internalBuffer.Length >= maxBufferSize)
                                {
                                    pendingException = Fx.Exception.AsError(new QuotaExceededException(SR.Format(SR.MaxReceivedMessageSizeExceeded, maxBufferSize)));
                                    return;
                                }

                                int newSize = (int)Math.Min(((double)internalBuffer.Length) * 2, maxBufferSize);
                                Fx.Assert(newSize > 0, "buffer size should be larger than zero.");
                                byte[] newBuffer = bufferManager.TakeBuffer(newSize);
                                Buffer.BlockCopy(internalBuffer, 0, newBuffer, 0, receivedByteCount);
                                bufferManager.ReturnBuffer(internalBuffer);
                                internalBuffer = newBuffer;
                            }

                            //if (TD.WebSocketAsyncReadStopIsEnabled())
                            //{
                            //    TD.WebSocketAsyncReadStop(
                            //        this.webSocket.GetHashCode(),
                            //        receivedByteCount,
                            //        TraceUtility.GetRemoteEndpointAddressPort(this.RemoteEndpointMessageProperty));
                            //}
                        }
                        catch (AggregateException ex)
                        {
                            WebSocketHelper.ThrowCorrectException(ex, TimeSpan.MaxValue, WebSocketHelper.ReceiveOperation);
                        }

                    }
                    while (!endOfMessage && !closureReceived);

                    byte[] buffer = null;
                    bool success = false;
                    try
                    {
                        buffer = bufferManager.TakeBuffer(receivedByteCount);
                        Buffer.BlockCopy(internalBuffer, 0, buffer, 0, receivedByteCount);
                        Fx.Assert(result != null, "Result should not be null");
                        pendingMessage = await PrepareMessageAsync(result, buffer, receivedByteCount);
                        success = true;
                    }
                    finally
                    {
                        if (buffer != null && (!success || pendingMessage == null))
                        {
                            bufferManager.ReturnBuffer(buffer);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (Fx.IsFatal(ex))
                    {
                        throw;
                    }

                    pendingException = WebSocketHelper.ConvertAndTraceException(ex, TimeSpan.MaxValue, WebSocketHelper.ReceiveOperation);
                }
                finally
                {
                    if (internalBuffer != null)
                    {
                        bufferManager.ReturnBuffer(internalBuffer);
                    }
                }
            }

            public async Task<bool> WaitForMessageAsync(CancellationToken token)
            {
                try
                {
                    pendingMessage = await ReceiveAsync(token);
                    return true;
                }
                catch (TimeoutException ex)
                {
                    //if (TD.ReceiveTimeoutIsEnabled())
                    //{
                    //    TD.ReceiveTimeout(ex.Message);
                    //}

                    pendingException = Fx.Exception.AsError(ex);
                    DiagnosticUtility.TraceHandledException(ex, TraceEventType.Information);
                    return false;
                }
            }

            internal void FinishUsingMessageStream(Exception ex)
            {
                //// The pattern of the task here is:
                //// 1) Only one thread can get the stream and consume the stream. A new task will be created at the moment it takes the stream
                //// 2) Only one another thread can enter the lock and wait on the task
                //// 3) The cleanup on the stream will return the stream to message source. And the cleanup call is limited to be called only once.
                if (ex != null && pendingException == null)
                {
                    pendingException = ex;
                }

                streamWaitTask.SetResult(null);
            }

            internal void CheckCloseStatus(WebSocketReceiveResult result)
            {
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    //if (TD.WebSocketCloseStatusReceivedIsEnabled())
                    //{
                    //    TD.WebSocketCloseStatusReceived(
                    //        this.webSocket.GetHashCode(),
                    //        result.CloseStatus.ToString());
                    //}

                    closureReceived = true;
                    closeDetails.InputCloseStatus = result.CloseStatus;
                    closeDetails.InputCloseStatusDescription = result.CloseStatusDescription;
                }
            }

            private async void StartNextReceiveAsync()
            {
                Fx.Assert(receiveTask == null || receiveTask.Task.IsCompleted, "this.receiveTask is not completed.");
                receiveTask = new TaskCompletionSource<object>();
                int currentState = Interlocked.CompareExchange(ref asyncReceiveState, AsyncReceiveState.Started, AsyncReceiveState.Finished);
                Fx.Assert(currentState == AsyncReceiveState.Finished, "currentState is not AsyncReceiveState.Finished: " + currentState);
                if (currentState != AsyncReceiveState.Finished)
                {
                    throw Fx.Exception.AsError(new InvalidOperationException());
                }

                try
                {
                    if (useStreaming)
                    {
                        if (streamWaitTask != null)
                        {
                            //// Wait until the previous stream message finished.

                            await streamWaitTask.Task.ConfigureAwait(false);
                        }

                        streamWaitTask = new TaskCompletionSource<object>();
                    }

                    if (pendingException == null)
                    {
                        if (!useStreaming)
                        {
                            await ReadBufferedMessageAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            byte[] buffer = bufferManager.TakeBuffer(receiveBufferSize);
                            bool success = false;
                            try
                            {
                                //if (TD.WebSocketAsyncReadStartIsEnabled())
                                //{
                                //    TD.WebSocketAsyncReadStart(this.webSocket.GetHashCode());
                                //}

                                try
                                {
                                    Task<WebSocketReceiveResult> receiveTask = webSocket.ReceiveAsync(
                                                        new ArraySegment<byte>(buffer, 0, receiveBufferSize),
                                                        CancellationToken.None);

                                    await receiveTask.ConfigureAwait(false);

                                    WebSocketReceiveResult result = receiveTask.Result;
                                    CheckCloseStatus(result);
                                    pendingMessage = await PrepareMessageAsync(result, buffer, result.Count);

                                    //if (TD.WebSocketAsyncReadStopIsEnabled())
                                    //{
                                    //    TD.WebSocketAsyncReadStop(
                                    //        this.webSocket.GetHashCode(),
                                    //        result.Count,
                                    //        TraceUtility.GetRemoteEndpointAddressPort(this.remoteEndpointMessageProperty));
                                    //}
                                }
                                catch (AggregateException ex)
                                {
                                    WebSocketHelper.ThrowCorrectException(ex, asyncReceiveTimeout, WebSocketHelper.ReceiveOperation);
                                }
                                success = true;
                            }
                            catch (Exception ex)
                            {
                                if (Fx.IsFatal(ex))
                                {
                                    throw;
                                }

                                pendingException = WebSocketHelper.ConvertAndTraceException(ex, asyncReceiveTimeout, WebSocketHelper.ReceiveOperation);
                            }
                            finally
                            {
                                if (!success)
                                {
                                    bufferManager.ReturnBuffer(buffer);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    if (Interlocked.CompareExchange(ref asyncReceiveState, AsyncReceiveState.Finished, AsyncReceiveState.Started) == AsyncReceiveState.Started)
                    {
                        receiveTask.SetResult(null);
                    }
                }
            }

            private void AddMessageProperties(MessageProperties messageProperties, WebSocketMessageType incomingMessageType)
            {
                Fx.Assert(messageProperties != null, "messageProperties should not be null.");
                WebSocketMessageProperty messageProperty = new WebSocketMessageProperty(
                                                                context,
                                                                webSocket.SubProtocol,
                                                                incomingMessageType,
                                                                properties);
                messageProperties.Add(WebSocketMessageProperty.Name, messageProperty);

                if (remoteEndpointMessageProperty != null)
                {
                    messageProperties.Add(RemoteEndpointMessageProperty.Name, remoteEndpointMessageProperty);
                }

                if (handshakeSecurityMessageProperty != null)
                {
                    messageProperties.Security = (SecurityMessageProperty)handshakeSecurityMessageProperty.CreateCopy();
                }
            }

            private Message GetPendingMessage()
            {
                ThrowOnPendingException(ref pendingException);

                if (pendingMessage != null)
                {
                    Message pendingMessage = this.pendingMessage;
                    this.pendingMessage = null;
                    return pendingMessage;
                }

                return null;
            }

            private async Task<Message> PrepareMessageAsync(WebSocketReceiveResult result, byte[] buffer, int count)
            {
                if (result.MessageType != WebSocketMessageType.Close)
                {
                    Message message;
                    if (useStreaming)
                    {
                        TimeoutHelper readTimeoutHelper = new TimeoutHelper(defaultTimeouts.ReceiveTimeout);
                        message = await encoder.ReadMessageAsync(
                            new MaxMessageSizeStream(
                                new TimeoutStream(
                                    new WebSocketStream(
                                        this,
                                        new ArraySegment<byte>(buffer, 0, count),
                                        webSocket,
                                        result.EndOfMessage,
                                        bufferManager,
                                        new TimeoutHelper(defaultTimeouts.CloseTimeout).GetCancellationToken()),
                                    readTimeoutHelper.GetCancellationToken()),
                                maxReceivedMessageSize),
                            maxBufferSize);
                    }
                    else
                    {
                        ArraySegment<byte> bytes = new ArraySegment<byte>(buffer, 0, count);
                        message = encoder.ReadMessage(bytes, bufferManager);
                    }

                    if (message.Version.Addressing != AddressingVersion.None || !localAddress.IsAnonymous)
                    {
                        localAddress.ApplyTo(message);
                    }

                    if (message.Version.Addressing == AddressingVersion.None && message.Headers.Action == null)
                    {
                        if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            message.Headers.Action = WebSocketTransportSettings.BinaryMessageReceivedAction;
                        }
                        else
                        {
                            // WebSocketMesssageType should always be binary or text at this moment. The layer below us will help protect this.
                            Fx.Assert(result.MessageType == WebSocketMessageType.Text, "result.MessageType must be WebSocketMessageType.Text.");
                            message.Headers.Action = WebSocketTransportSettings.TextMessageReceivedAction;
                        }
                    }

                    if (message != null)
                    {
                        AddMessageProperties(message.Properties, result.MessageType);
                    }

                    return message;
                }

                return null;
            }

            private static class AsyncReceiveState
            {
                internal const int Started = 0;
                internal const int Finished = 1;
                internal const int Cancelled = 2;
            }
        }

        private class WebSocketStream : Stream
        {
            private readonly WebSocket webSocket;
            private readonly WebSocketMessageSource messageSource;
            private CancellationToken closeToken;
            private ArraySegment<byte> initialReadBuffer;
            private bool endOfMessageReached = false;
            private readonly bool isForRead;
            private bool endofMessageReceived;
            private readonly WebSocketMessageType outgoingMessageType;
            private readonly BufferManager bufferManager;
            private int messageSourceCleanState;
            private int endOfMessageWritten;
            private int readTimeout;
            private int writeTimeout;

            public WebSocketStream(
                        WebSocketMessageSource messageSource,
                        ArraySegment<byte> initialBuffer,
                        WebSocket webSocket,
                        bool endofMessageReceived,
                        BufferManager bufferManager,
                        CancellationToken closeToken)
                : this(webSocket, WebSocketDefaults.DefaultWebSocketMessageType, closeToken)
            {
                Fx.Assert(messageSource != null, "messageSource should not be null.");
                this.messageSource = messageSource;
                initialReadBuffer = initialBuffer;
                isForRead = true;
                this.endofMessageReceived = endofMessageReceived;
                this.bufferManager = bufferManager;
                messageSourceCleanState = WebSocketHelper.OperationNotStarted;
                endOfMessageWritten = WebSocketHelper.OperationNotStarted;
            }

            public WebSocketStream(
                    WebSocket webSocket,
                    WebSocketMessageType outgoingMessageType,
                    CancellationToken closeToken)
            {
                Fx.Assert(webSocket != null, "webSocket should not be null.");
                this.webSocket = webSocket;
                isForRead = false;
                this.outgoingMessageType = outgoingMessageType;
                messageSourceCleanState = WebSocketHelper.OperationFinished;
                this.closeToken = closeToken;
            }

            public override bool CanRead
            {
                get { return isForRead; }
            }

            public override bool CanSeek
            {
                get { return false; }
            }

            public override bool CanTimeout
            {
                get
                {
                    return true;
                }
            }

            public override bool CanWrite
            {
                get { return !isForRead; }
            }

            public override long Length
            {
                get { throw Fx.Exception.AsError(new NotSupportedException(SR.SeekNotSupported)); }
            }

            public override long Position
            {
                get
                {
                    throw Fx.Exception.AsError(new NotSupportedException(SR.SeekNotSupported));
                }

                set
                {
                    throw Fx.Exception.AsError(new NotSupportedException(SR.SeekNotSupported));
                }
            }

            public override int ReadTimeout
            {
                get
                {
                    return readTimeout;
                }

                set
                {
                    Fx.Assert(value >= 0, "ReadTimeout should not be negative.");
                    readTimeout = value;
                }
            }

            public override int WriteTimeout
            {
                get
                {
                    return writeTimeout;
                }

                set
                {
                    Fx.Assert(value >= 0, "WriteTimeout should not be negative.");
                    writeTimeout = value;
                }
            }

            public override void Close()
            {
                base.Close();
                CleanupAsync(closeToken);
            }

            public override void Flush()
            {
            }

            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            {
                return ReadAsync(buffer, offset, count).ToApm(callback, state);
            }

            public override int EndRead(IAsyncResult asyncResult)
            {
                return asyncResult.ToApmEnd<int>();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Fx.Assert(messageSource != null, "messageSource should not be null in read case.");
                Fx.Assert(cancellationToken.CanBeCanceled, "WebSocketStream should be wrapped by TimeoutStream which should pass a cancellable token");
                cancellationToken.ThrowIfCancellationRequested();

                if (endOfMessageReached)
                {
                    return 0;
                }

                if (initialReadBuffer.Count != 0)
                {
                    return GetBytesFromInitialReadBuffer(buffer, offset, count);
                }

                int receivedBytes = 0;
                if (endofMessageReceived)
                {
                    endOfMessageReached = true;
                }
                else
                {
                    //if (TD.WebSocketAsyncReadStartIsEnabled())
                    //{
                    //    TD.WebSocketAsyncReadStart(this.webSocket.GetHashCode());
                    //}

                    WebSocketReceiveResult result = null;
                    try
                    {
                        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, count), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        if (Fx.IsFatal(ex))
                        {
                            throw;
                        }

                        WebSocketHelper.ThrowCorrectException(ex, TimeoutHelper.GetOriginalTimeout(cancellationToken), WebSocketHelper.ReceiveOperation);
                    }

                    if (result.EndOfMessage)
                    {
                        endofMessageReceived = true;
                        endOfMessageReached = true;
                    }

                    receivedBytes = result.Count;
                    CheckResultAndEnsureNotCloseMessage(messageSource, result);

                    //if (TD.WebSocketAsyncReadStopIsEnabled())
                    //{
                    //    TD.WebSocketAsyncReadStop(
                    //        this.webSocket.GetHashCode(),
                    //        receivedBytes,
                    //        this.messageSource != null ? TraceUtility.GetRemoteEndpointAddressPort(this.messageSource.RemoteEndpointMessageProperty) : string.Empty);
                    //}
                }

                if (endOfMessageReached)
                {
                    CleanupAsync(cancellationToken);
                }

                return receivedBytes;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw Fx.Exception.AsError(new NotSupportedException());
            }

            public override void SetLength(long value)
            {
                throw Fx.Exception.AsError(new NotSupportedException());
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                WriteAsync(buffer, offset, count).GetAwaiter().GetResult();
            }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (endOfMessageWritten == WebSocketHelper.OperationFinished)
                {
                    throw Fx.Exception.AsError(new InvalidOperationException(SR.WebSocketStreamWriteCalledAfterEOMSent));
                }

                Fx.Assert(cancellationToken.CanBeCanceled, "WebSocketStream should be wrapped by TimeoutStream which should pass a cancellable token");
                cancellationToken.ThrowIfCancellationRequested();

                cancellationToken.ThrowIfCancellationRequested();
                //if (TD.WebSocketAsyncWriteStartIsEnabled())
                //{
                //    TD.WebSocketAsyncWriteStart(
                //            this.webSocket.GetHashCode(),
                //            count,
                //            this.messageSource != null ? TraceUtility.GetRemoteEndpointAddressPort(this.messageSource.RemoteEndpointMessageProperty) : string.Empty);
                //}

                try
                {
                    await webSocket.SendAsync(new ArraySegment<byte>(buffer, offset, count), outgoingMessageType, false, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    if (Fx.IsFatal(ex))
                    {
                        throw;
                    }

                    WebSocketHelper.ThrowCorrectException(ex, TimeoutHelper.GetOriginalTimeout(cancellationToken), WebSocketHelper.SendOperation);
                }

                //if (TD.WebSocketAsyncWriteStopIsEnabled())
                //{
                //    TD.WebSocketAsyncWriteStop(this.webSocket.GetHashCode());
                //}
            }

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            {
                return WriteAsync(buffer, offset, count).ToApm(callback, state);
            }

            public override void EndWrite(IAsyncResult asyncResult)
            {
                asyncResult.ToApmEnd();
            }

            public async Task WriteEndOfMessageAsync(CancellationToken cancellationToken)
            {
                //if (TD.WebSocketAsyncWriteStartIsEnabled())
                //{
                //    TD.WebSocketAsyncWriteStart(
                //            this.webSocket.GetHashCode(),
                //            0,
                //            this.messageSource != null ? TraceUtility.GetRemoteEndpointAddressPort(this.messageSource.RemoteEndpointMessageProperty) : string.Empty);
                //}

                if (Interlocked.CompareExchange(ref endOfMessageWritten, WebSocketHelper.OperationFinished, WebSocketHelper.OperationNotStarted) == WebSocketHelper.OperationNotStarted)
                {
                    try
                    {
                        await webSocket.SendAsync(new ArraySegment<byte>(Array.Empty<byte>(), 0, 0), outgoingMessageType, true, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        if (Fx.IsFatal(ex))
                        {
                            throw;
                        }

                        WebSocketHelper.ThrowCorrectException(ex, TimeoutHelper.GetOriginalTimeout(cancellationToken), WebSocketHelper.SendOperation);
                    }
                }

                //if (TD.WebSocketAsyncWriteStopIsEnabled())
                //{
                //    TD.WebSocketAsyncWriteStop(this.webSocket.GetHashCode());
                //}
            }

            private static void CheckResultAndEnsureNotCloseMessage(WebSocketMessageSource messageSource, WebSocketReceiveResult result)
            {
                messageSource.CheckCloseStatus(result);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw Fx.Exception.AsError(new ProtocolException(SR.WebSocketUnexpectedCloseMessageError));
                }
            }

            private int GetBytesFromInitialReadBuffer(byte[] buffer, int offset, int count)
            {
                int bytesToCopy = initialReadBuffer.Count > count ? count : initialReadBuffer.Count;
                Buffer.BlockCopy(initialReadBuffer.Array, initialReadBuffer.Offset, buffer, offset, bytesToCopy);
                initialReadBuffer = new ArraySegment<byte>(initialReadBuffer.Array, initialReadBuffer.Offset + bytesToCopy, initialReadBuffer.Count - bytesToCopy);
                return bytesToCopy;
            }

            private async Task CleanupAsync(CancellationToken cancellationToken)
            {
                if (isForRead)
                {
                    if (Interlocked.CompareExchange(ref messageSourceCleanState, WebSocketHelper.OperationFinished, WebSocketHelper.OperationNotStarted) == WebSocketHelper.OperationNotStarted)
                    {
                        Exception pendingException = null;
                        try
                        {
                            if (!endofMessageReceived && (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseSent))
                            {
                                // Drain the reading stream
                                do
                                {
                                    try
                                    {
                                        WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(initialReadBuffer.Array), cancellationToken);
                                        endofMessageReceived = receiveResult.EndOfMessage;
                                    }
                                    catch (Exception ex)
                                    {
                                        if (Fx.IsFatal(ex))
                                        {
                                            throw;
                                        }

                                        WebSocketHelper.ThrowCorrectException(ex, TimeoutHelper.GetOriginalTimeout(cancellationToken), WebSocketHelper.ReceiveOperation);
                                    }
                                }
                                while (!endofMessageReceived && (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseSent));
                            }
                        }
                        catch (Exception ex)
                        {
                            if (Fx.IsFatal(ex))
                            {
                                throw;
                            }

                            // Not throwing out this exception during stream cleanup. The exception
                            // will be thrown out when we are trying to receive the next message using the same
                            // WebSocket object.
                            pendingException = WebSocketHelper.ConvertAndTraceException(ex, TimeoutHelper.GetOriginalTimeout(cancellationToken), WebSocketHelper.CloseOperation);
                        }

                        bufferManager.ReturnBuffer(initialReadBuffer.Array);
                        Fx.Assert(messageSource != null, "messageSource should not be null.");
                        messageSource.FinishUsingMessageStream(pendingException);
                    }
                }
                else
                {
                    if (Interlocked.CompareExchange(ref endOfMessageWritten, WebSocketHelper.OperationFinished, WebSocketHelper.OperationNotStarted) == WebSocketHelper.OperationNotStarted)
                    {
                        await WriteEndOfMessageAsync(cancellationToken);
                    }
                }
            }
        }

        private class WebSocketCloseDetails : IWebSocketCloseDetails
        {
            private WebSocketCloseStatus outputCloseStatus = WebSocketCloseStatus.NormalClosure;
            private string outputCloseStatusDescription;
            private WebSocketCloseStatus? inputCloseStatus;
            private string inputCloseStatusDescription;

            public WebSocketCloseStatus? InputCloseStatus
            {
                get
                {
                    return inputCloseStatus;
                }

                internal set
                {
                    inputCloseStatus = value;
                }
            }

            public string InputCloseStatusDescription
            {
                get
                {
                    return inputCloseStatusDescription;
                }

                internal set
                {
                    inputCloseStatusDescription = value;
                }
            }

            internal WebSocketCloseStatus OutputCloseStatus
            {
                get
                {
                    return outputCloseStatus;
                }
            }

            internal string OutputCloseStatusDescription
            {
                get
                {
                    return outputCloseStatusDescription;
                }
            }

            public void SetOutputCloseStatus(WebSocketCloseStatus closeStatus, string closeStatusDescription)
            {
                outputCloseStatus = closeStatus;
                outputCloseStatusDescription = closeStatusDescription;
            }
        }
    }
}
