//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using CoreWCF.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Security.Authentication.ExtendedProtection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Primitives;

namespace CoreWCF.Channels
{
    // abstract out the common functionality of an "HttpInput"
    abstract class HttpInput
    {
        const string multipartRelatedMediaType = "multipart/related";
        const string startInfoHeaderParam = "start-info";
        const string defaultContentType = "application/octet-stream";

        BufferManager bufferManager;
        bool isRequest;
        MessageEncoder messageEncoder;
        IHttpTransportFactorySettings settings;
        bool streamed;
        WebException webException;
        Stream inputStream;
        bool enableChannelBinding;
        bool errorGettingInputStream;

        protected HttpInput(IHttpTransportFactorySettings settings, bool isRequest, bool enableChannelBinding)
        {
            this.settings = settings;
            this.bufferManager = settings.BufferManager;
            this.messageEncoder = settings.MessageEncoderFactory.Encoder;
            this.webException = null;
            this.isRequest = isRequest;
            this.inputStream = null;
            this.enableChannelBinding = enableChannelBinding;

            if (isRequest)
            {
                this.streamed = TransferModeHelper.IsRequestStreamed(settings.TransferMode);
            }
            else
            {
                this.streamed = TransferModeHelper.IsResponseStreamed(settings.TransferMode);
            }
        }

        internal WebException WebException
        {
            get { return webException; }
            set { webException = value; }
        }

        // Note: This method will return null in the case where throwOnError is false, and a non-fatal error occurs.
        // Please exercise caution when passing in throwOnError = false.  This should basically only be done in error
        // code paths, or code paths where there is very good reason that you would not want this method to throw.
        // When passing in throwOnError = false, please handle the case where this method returns null.
        public Stream GetInputStream(bool throwOnError)
        {
            if (inputStream == null && (throwOnError || !this.errorGettingInputStream))
            {
                try
                {
                    inputStream = GetInputStream();
                    this.errorGettingInputStream = false;
                }
                catch (Exception e)
                {
                    this.errorGettingInputStream = true;
                    if (throwOnError || Fx.IsFatal(e))
                    {
                        throw;
                    }

                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Warning);
                }
            }

            return inputStream;
        }

        // -1 if chunked
        public abstract long ContentLength { get; }
        protected abstract string ContentTypeCore { get; }
        protected abstract bool HasContent { get; }
        protected abstract string SoapActionHeader { get; }
        protected abstract Stream GetInputStream();
        protected virtual ChannelBinding ChannelBinding { get { return null; } }

        protected string ContentType
        {
            get
            {
                string contentType = ContentTypeCore;

                if (string.IsNullOrEmpty(contentType))
                {
                    return defaultContentType;
                }

                return contentType;
            }
        }

        void ThrowMaxReceivedMessageSizeExceeded()
        {
            if (isRequest)
            {
                ThrowHttpProtocolException(SR.Format(SR.MaxReceivedMessageSizeExceeded, settings.MaxReceivedMessageSize), HttpStatusCode.RequestEntityTooLarge);
            }
            else
            {
                string message = SR.Format(SR.MaxReceivedMessageSizeExceeded, settings.MaxReceivedMessageSize);
                Exception inner = new QuotaExceededException(message);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(message, inner));
            }
        }

        Message DecodeBufferedMessage(ArraySegment<byte> buffer, Stream inputStream)
        {
            try
            {
                // if we're chunked, make sure we've consumed the whole body
                if (ContentLength == -1 && buffer.Count == settings.MaxReceivedMessageSize)
                {
                    byte[] extraBuffer = new byte[1];
                    int extraReceived = inputStream.Read(extraBuffer, 0, 1);
                    if (extraReceived > 0)
                    {
                        ThrowMaxReceivedMessageSizeExceeded();
                    }
                }

                try
                {
                    return messageEncoder.ReadMessage(buffer, bufferManager, ContentType);
                }
                catch (XmlException xmlException)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new ProtocolException(SR.MessageXmlProtocolError, xmlException));
                }
            }
            finally
            {
                inputStream.Close();
            }
        }

        Message ReadBufferedMessage(Stream inputStream)
        {
            ArraySegment<byte> messageBuffer = GetMessageBuffer();
            byte[] buffer = messageBuffer.Array;
            int offset = 0;
            int count = messageBuffer.Count;

            while (count > 0)
            {
                int bytesRead = inputStream.Read(buffer, offset, count);
                if (bytesRead == 0) // EOF 
                {
                    if (ContentLength != -1)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            new ProtocolException(SR.HttpContentLengthIncorrect));
                    }

                    break;
                }
                count -= bytesRead;
                offset += bytesRead;
            }

            return DecodeBufferedMessage(new ArraySegment<byte>(buffer, 0, offset), inputStream);
        }

        Message ReadChunkedBufferedMessage(Stream inputStream)
        {
            try
            {
                return messageEncoder.ReadMessage(BufferMessageStream(inputStream, bufferManager, settings.MaxBufferSize), bufferManager, ContentType);
            }
            catch (XmlException xmlException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ProtocolException(SR.MessageXmlProtocolError, xmlException));
            }
        }

        Message ReadStreamedMessage(Stream inputStream)
        {
            MaxMessageSizeStream maxMessageSizeStream = new MaxMessageSizeStream(inputStream, settings.MaxReceivedMessageSize);

            try
            {
                return messageEncoder.ReadMessage(maxMessageSizeStream, settings.MaxBufferSize, ContentType);
            }
            catch (XmlException xmlException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ProtocolException(SR.MessageXmlProtocolError, xmlException));
            }
        }

        // used for buffered streaming
        internal ArraySegment<byte> BufferMessageStream(Stream stream, BufferManager bufferManager, int maxBufferSize)
        {
            byte[] buffer = bufferManager.TakeBuffer(ConnectionOrientedTransportDefaults.ConnectionBufferSize);
            int offset = 0;
            int currentBufferSize = Math.Min(buffer.Length, maxBufferSize);

            while (offset < currentBufferSize)
            {
                int count = stream.Read(buffer, offset, currentBufferSize - offset);
                if (count == 0)
                {
                    stream.Dispose();
                    break;
                }

                offset += count;
                if (offset == currentBufferSize)
                {
                    if (currentBufferSize >= maxBufferSize)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(MaxMessageSizeStream.CreateMaxReceivedMessageSizeExceededException(maxBufferSize));
                    }

                    currentBufferSize = Math.Min(currentBufferSize * 2, maxBufferSize);
                    byte[] temp = bufferManager.TakeBuffer(currentBufferSize);
                    Buffer.BlockCopy(buffer, 0, temp, 0, offset);
                    bufferManager.ReturnBuffer(buffer);
                    buffer = temp;
                }
            }

            return new ArraySegment<byte>(buffer, 0, offset);
        }

        protected abstract void AddProperties(Message message);

        void ApplyChannelBinding(Message message)
        {
            if (this.enableChannelBinding)
            {
                ChannelBindingUtility.TryAddToMessage(this.ChannelBinding, message, true);
            }
        }

        // makes sure that appropriate HTTP level headers are included in the received Message
        Exception ProcessHttpAddressing(Message message)
        {
            Exception result = null;
            AddProperties(message);

            // check if user is receiving WS-1 messages
            if (message.Version.Addressing == AddressingVersion.None)
            {
                bool actionAbsent = false;
                try
                {
                    actionAbsent = (message.Headers.Action == null);
                }
                catch (XmlException e)
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }
                catch (CommunicationException e)
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }

                if (!actionAbsent)
                {
                    result = new ProtocolException(SR.Format(SR.HttpAddressingNoneHeaderOnWire, "Action"));
                }

                bool toAbsent = false;
                try
                {
                    toAbsent = (message.Headers.To == null);
                }
                catch (XmlException e)
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }
                catch (CommunicationException e)
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }

                if (!toAbsent)
                {
                    result = new ProtocolException(SR.Format(SR.HttpAddressingNoneHeaderOnWire, "To"));
                }
                message.Headers.To = message.Properties.Via;
            }

            if (isRequest)
            {
                string action = null;

                if (message.Version.Envelope == EnvelopeVersion.Soap11)
                {
                    action = SoapActionHeader;
                }
                else if (message.Version.Envelope == EnvelopeVersion.Soap12 && !String.IsNullOrEmpty(ContentType))
                {
                    ContentType parsedContentType = new ContentType(ContentType);

                    if (parsedContentType.MediaType == multipartRelatedMediaType && parsedContentType.Parameters.ContainsKey(startInfoHeaderParam))
                    {
                        // fix to grab action from start-info as stated in RFC2387
                        action = new ContentType(parsedContentType.Parameters[startInfoHeaderParam]).Parameters["action"];
                    }
                    if (action == null)
                    {
                        // only if we can't find an action inside start-info
                        action = parsedContentType.Parameters["action"];
                    }
                }

                if (action != null)
                {
                    action = UrlUtility.UrlDecode(action, Encoding.UTF8);

                    if (action.Length >= 2 && action[0] == '"' && action[action.Length - 1] == '"')
                    {
                        action = action.Substring(1, action.Length - 2);
                    }

                    if (message.Version.Addressing == AddressingVersion.None)
                    {
                        message.Headers.Action = action;
                    }

                    try
                    {

                        if (action.Length > 0 && string.Compare(message.Headers.Action, action, StringComparison.Ordinal) != 0)
                        {
                            result = new ActionMismatchAddressingException(SR.Format(SR.HttpSoapActionMismatchFault,
                                message.Headers.Action, action), message.Headers.Action, action);
                        }

                    }
                    catch (XmlException e)
                    {
                        DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    }
                    catch (CommunicationException e)
                    {
                        DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    }
                }
            }

            ApplyChannelBinding(message);

            return result;
        }

        void ValidateContentType()
        {
            if (!HasContent)
                return;

            if (string.IsNullOrEmpty(ContentType))
            {
                ThrowHttpProtocolException(SR.HttpContentTypeHeaderRequired, HttpStatusCode.UnsupportedMediaType, HttpChannelUtilities.StatusDescriptionStrings.HttpContentTypeMissing);
            }
            if (!messageEncoder.IsContentTypeSupported(ContentType))
            {
                string statusDescription = string.Format(CultureInfo.InvariantCulture, HttpChannelUtilities.StatusDescriptionStrings.HttpContentTypeMismatch, ContentType, messageEncoder.ContentType);
                ThrowHttpProtocolException(SR.Format(SR.ContentTypeMismatch, ContentType, messageEncoder.ContentType), HttpStatusCode.UnsupportedMediaType, statusDescription);
            }
        }

        public Message ParseIncomingMessage(out Exception requestException)
        {
            Message message = null;
            requestException = null;
            bool throwing = true;
            try
            {
                ValidateContentType();

                if (!this.HasContent)
                {
                    if (this.messageEncoder.MessageVersion == MessageVersion.None)
                    {
                        message = new NullMessage();
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    Stream stream = this.GetInputStream(true);
                    if (streamed)
                    {
                        message = ReadStreamedMessage(stream);
                    }
                    else if (this.ContentLength == -1)
                    {
                        message = ReadChunkedBufferedMessage(stream);
                    }
                    else
                    {
                        message = ReadBufferedMessage(stream);
                    }
                }

                requestException = ProcessHttpAddressing(message);

                throwing = false;
                return message;
            }
            finally
            {
                if (throwing)
                {
                    Close();
                }
            }
        }

        void ThrowHttpProtocolException(string message, HttpStatusCode statusCode)
        {
            ThrowHttpProtocolException(message, statusCode, null);
        }

        void ThrowHttpProtocolException(string message, HttpStatusCode statusCode, string statusDescription)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateHttpProtocolException(message, statusCode, statusDescription, webException));
        }

        internal static ProtocolException CreateHttpProtocolException(string message, HttpStatusCode statusCode, string statusDescription, Exception innerException)
        {
            ProtocolException exception = new ProtocolException(message, innerException);
            exception.Data.Add(HttpChannelUtilities.HttpStatusCodeExceptionKey, statusCode);
            if (statusDescription != null && statusDescription.Length > 0)
            {
                exception.Data.Add(HttpChannelUtilities.HttpStatusDescriptionExceptionKey, statusDescription);
            }

            return exception;
        }

        protected virtual void Close()
        {
        }

        ArraySegment<byte> GetMessageBuffer()
        {
            long count = ContentLength;
            int bufferSize;

            if (count > settings.MaxReceivedMessageSize)
            {
                ThrowMaxReceivedMessageSizeExceeded();
            }

            bufferSize = (int)count;

            return new ArraySegment<byte>(bufferManager.TakeBuffer(bufferSize), 0, bufferSize);
        }
    }

    // abstract out the common functionality of an "HttpOutput"
    abstract class HttpOutput
    {
        const string DefaultMimeVersion = "1.0";

        HttpAbortReason abortReason;
        bool isDisposed;
        bool isRequest;
        Message message;
        IHttpTransportFactorySettings settings;
        byte[] bufferToRecycle;
        BufferManager bufferManager;
        MessageEncoder messageEncoder;
        bool streamed;
        static Action<object> onStreamSendTimeout;
        string mtomBoundary;
        Stream outputStream;
        bool supportsConcurrentIO;
        bool canSendCompressedResponses;

        protected HttpOutput(IHttpTransportFactorySettings settings, Message message, bool isRequest, bool supportsConcurrentIO)
        {
            this.settings = settings;
            this.message = message;
            this.isRequest = isRequest;
            this.bufferManager = settings.BufferManager;
            this.messageEncoder = settings.MessageEncoderFactory.Encoder;
            ICompressedMessageEncoder compressedMessageEncoder = this.messageEncoder as ICompressedMessageEncoder;
            this.canSendCompressedResponses = compressedMessageEncoder != null && compressedMessageEncoder.CompressionEnabled;
            if (isRequest)
            {
                this.streamed = TransferModeHelper.IsRequestStreamed(settings.TransferMode);
            }
            else
            {
                this.streamed = TransferModeHelper.IsResponseStreamed(settings.TransferMode);
            }
            this.supportsConcurrentIO = supportsConcurrentIO;
        }

        protected virtual bool IsChannelBindingSupportEnabled { get { return false; } }
        protected virtual ChannelBinding ChannelBinding { get { return null; } }

        protected void Abort()
        {
            Abort(HttpAbortReason.Aborted);
        }

        public virtual void Abort(HttpAbortReason reason)
        {
            if (isDisposed)
            {
                return;
            }

            this.abortReason = reason;

            CleanupBuffer();
        }

        public Task CloseAsync()
        {
            if (isDisposed)
            {
                return Task.CompletedTask;
            }

            try
            {
                if (outputStream != null)
                {
                    outputStream.Close();
                }
            }
            finally
            {
                CleanupBuffer();
            }

            return Task.CompletedTask;
        }

        void CleanupBuffer()
        {
            byte[] bufferToRecycleSnapshot = Interlocked.Exchange<byte[]>(ref this.bufferToRecycle, null);
            if (bufferToRecycleSnapshot != null)
            {
                bufferManager.ReturnBuffer(bufferToRecycleSnapshot);
            }

            isDisposed = true;
        }

        protected abstract void AddMimeVersion(string version);
        protected abstract void AddHeader(string name, string value);
        protected abstract void SetContentType(string contentType);
        protected abstract void SetContentEncoding(string contentEncoding);
        protected abstract void SetStatusCode(HttpStatusCode statusCode);
        protected abstract void SetStatusDescription(string statusDescription);
        protected virtual bool CleanupChannelBinding { get { return true; } }
        protected virtual void SetContentLength(int contentLength)
        {
        }

        protected virtual string HttpMethod { get { return null; } }

        public virtual ChannelBinding TakeChannelBinding()
        {
            return null;
        }

        private void ApplyChannelBinding()
        {
            if (this.IsChannelBindingSupportEnabled)
            {
                ChannelBindingUtility.TryAddToMessage(this.ChannelBinding, this.message, this.CleanupChannelBinding);
            }
        }

        protected abstract Stream GetOutputStream();

        protected virtual bool WillGetOutputStreamCompleteSynchronously
        {
            get { return true; }
        }

        protected bool CanSendCompressedResponses
        {
            get { return this.canSendCompressedResponses; }
        }

        protected virtual bool PrepareHttpSend(Message message)
        {
            string action = message.Headers.Action;

            if (message.Version.Addressing == AddressingVersion.None)
            {
                message.Headers.Action = null;
                message.Headers.To = null;
            }

            string contentType = null;

            if (message.Version == MessageVersion.None)
            {
                object property = null;
                if (message.Properties.TryGetValue(HttpResponseMessageProperty.Name, out property))
                {
                    HttpResponseMessageProperty responseProperty = (HttpResponseMessageProperty)property;
                    if (!string.IsNullOrEmpty(responseProperty.Headers[HttpResponseHeader.ContentType]))
                    {
                        contentType = responseProperty.Headers[HttpResponseHeader.ContentType];
                        if (!messageEncoder.IsContentTypeSupported(contentType))
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                new ProtocolException(SR.Format(SR.ResponseContentTypeNotSupported,
                                contentType)));
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(contentType))
            {
                //MtomMessageEncoder mtomMessageEncoder = messageEncoder as MtomMessageEncoder;
                //if (mtomMessageEncoder == null)
                //{
                contentType = messageEncoder.ContentType;
                //}
                //else
                //{
                //    contentType = mtomMessageEncoder.GetContentType(out this.mtomBoundary);
                //    // For MTOM messages, add a MIME version header
                //    AddMimeVersion("1.0");
                //}
            }

            SetContentType(contentType);
            return message is NullMessage;
        }

        ArraySegment<byte> SerializeBufferedMessage(Message message)
        {
            // by default, the HttpOutput should own the buffer and clean it up
            return SerializeBufferedMessage(message, true);
        }

        ArraySegment<byte> SerializeBufferedMessage(Message message, bool shouldRecycleBuffer)
        {
            ArraySegment<byte> result;

            //MtomMessageEncoder mtomMessageEncoder = messageEncoder as MtomMessageEncoder;
            //if (mtomMessageEncoder == null)
            //{
            result = messageEncoder.WriteMessage(message, int.MaxValue, bufferManager);
            //}
            //else
            //{
            //    result = mtomMessageEncoder.WriteMessage(message, int.MaxValue, bufferManager, 0, this.mtomBoundary);
            //}

            if (shouldRecycleBuffer)
            {
                // Only set this.bufferToRecycle if the HttpOutput owns the buffer, we will clean it up upon httpOutput.Close()
                // Otherwise, caller of SerializeBufferedMessage assumes responsibility for returning the buffer to the buffer pool
                this.bufferToRecycle = result.Array;
            }
            return result;
        }

        Stream GetWrappedOutputStream()
        {
            const int ChunkSize = 32768;    // buffer size used for synchronous writes
            // const int BufferSize = 16384;   // buffer size used for asynchronous writes
            // const int BufferCount = 4;      // buffer count used for asynchronous writes

            // Writing an HTTP request chunk has a high fixed cost, so use BufferedStream to avoid writing 
            // small ones. 
            // TODO: Evaluate whether we need to buffer the output stream and if concurrent io is supported
            //return supportsConcurrentIO ? (Stream)new BufferedOutputAsyncStream(outputStream, BufferSize, BufferCount) : new BufferedStream(outputStream, ChunkSize);
            //return this.supportsConcurrentIO ? (Stream)new BufferedOutputAsyncStream(this.outputStream, BufferSize, BufferCount) : new BufferedStream(this.outputStream, ChunkSize);
            return new BufferedStream(outputStream, ChunkSize);
        }

        private async Task WriteStreamedMessageAsync(CancellationToken token)
        {
            this.outputStream = GetWrappedOutputStream();

            // Since HTTP streams don't support timeouts, we can't just use TimeoutStream here. 
            // Rather, we need to run a timer to bound the overall operation
            if (onStreamSendTimeout == null)
            {
                onStreamSendTimeout = OnStreamSendTimeout;
            }

            // TODO: Verify that the cancellation token is honored for timeout
            //IOThreadTimer sendTimer = new IOThreadTimer(onStreamSendTimeout, this, true);
            //sendTimer.Set(timeout);

            try
            {
                //MtomMessageEncoder mtomMessageEncoder = messageEncoder as MtomMessageEncoder;
                //if (mtomMessageEncoder == null)
                //{
                await messageEncoder.WriteMessageAsync(this.message, this.outputStream);
                //}
                //else
                //{
                //    mtomMessageEncoder.WriteMessage(this.message, this.outputStream, this.mtomBoundary);
                //}

                //if (this.supportsConcurrentIO)
                //{
                //    this.outputStream.Close();
                //}
            }
            finally
            {
                //sendTimer.Cancel();
            }
        }

        static void OnStreamSendTimeout(object state)
        {
            HttpOutput thisPtr = (HttpOutput)state;
            thisPtr.Abort(HttpAbortReason.TimedOut);
        }

        public async Task SendAsync(CancellationToken token)
        {
            bool suppressEntityBody = PrepareHttpSend(message);

            if (suppressEntityBody)
            {
                // requests can't always support an output stream (for GET, etc)
                if (!isRequest)
                {
                    outputStream = GetOutputStream();
                }
                else
                {
                    this.SetContentLength(0);
                }
            }
            else if (streamed)
            {
                outputStream = GetOutputStream();
                ApplyChannelBinding();
                await WriteStreamedMessageAsync(token);
            }
            else
            {
                if (this.IsChannelBindingSupportEnabled)
                {
                    //need to get the Channel binding token (CBT), apply channel binding info to the message and then write the message                    
                    //CBT is only enabled when message security is in the stack, which also requires an HTTP entity body, so we 
                    //should be safe to always get the stream.
                    outputStream = GetOutputStream();

                    ApplyChannelBinding();

                    ArraySegment<byte> buffer = SerializeBufferedMessage(message);

                    Fx.Assert(buffer.Count != 0, "We should always have an entity body in this case...");
                    await outputStream.WriteAsync(buffer.Array, buffer.Offset, buffer.Count);
                }
                else
                {
                    ArraySegment<byte> buffer = SerializeBufferedMessage(message);
                    SetContentLength(buffer.Count);

                    // requests can't always support an output stream (for GET, etc)
                    if (!isRequest || buffer.Count > 0)
                    {
                        outputStream = GetOutputStream();
                        await outputStream.WriteAsync(buffer.Array, buffer.Offset, buffer.Count);
                    }
                }
            }
        }

        internal static HttpOutput CreateHttpOutput(HttpContext httpContext, IHttpTransportFactorySettings settings, Message message, string httpMethod)
        {
            return new AspNetCoreHttpOutput(httpContext, settings, message, httpMethod);
        }

        class AspNetCoreHttpOutput : HttpOutput
        {
            private HttpResponse httpResponse;
            private HttpContext httpContext;
            private string httpMethod;

            public AspNetCoreHttpOutput(HttpContext httpContext, IHttpTransportFactorySettings settings, Message message, string httpMethod)
                : base(settings, message, false, true)
            {
                this.httpResponse = httpContext.Response;
                this.httpContext = httpContext;
                this.httpMethod = httpMethod;

                if (message.IsFault)
                {
                    this.SetStatusCode(HttpStatusCode.InternalServerError);
                }
                else
                {
                    this.SetStatusCode(HttpStatusCode.OK);
                }
            }

            protected override string HttpMethod => httpMethod;

            public override void Abort(HttpAbortReason abortReason)
            {
                httpContext.Abort();
                base.Abort(abortReason);
            }

            protected override void AddMimeVersion(string version)
            {
                httpResponse.Headers[HttpChannelUtilities.MIMEVersionHeader] = version;
            }

            protected override bool PrepareHttpSend(Message message)
            {
                bool result = base.PrepareHttpSend(message);

                if (this.CanSendCompressedResponses)
                {
                    string contentType = httpResponse.ContentType;
                    string contentEncoding;
                    if (HttpChannelUtilities.GetHttpResponseTypeAndEncodingForCompression(ref contentType, out contentEncoding))
                    {
                        if (contentType != this.httpResponse.ContentType)
                        {
                            this.SetContentType(contentType);
                        }
                        this.SetContentEncoding(contentEncoding);
                    }
                }

                message.Properties.TryGetValue(HttpResponseMessageProperty.Name, out object responsePropertyObj);
                HttpResponseMessageProperty responseProperty = (HttpResponseMessageProperty)responsePropertyObj;
                bool httpResponseMessagePropertyFound = responseProperty != null;
                bool httpMethodIsHead = string.Compare(httpMethod, "HEAD", StringComparison.OrdinalIgnoreCase) == 0;

                if (httpMethodIsHead ||
                    httpResponseMessagePropertyFound && responseProperty.SuppressEntityBody)
                {
                    result = true;
                    SetContentLength(0);
                    SetContentType(null);
                    // TODO: Validate what to do about httpResponse.SendChunked property
                    //httpResponse.SendChunked = false;
                }

                if (httpResponseMessagePropertyFound)
                {
                    this.SetStatusCode(responseProperty.StatusCode);
                    if (responseProperty.StatusDescription != null)
                    {
                        this.SetStatusDescription(responseProperty.StatusDescription);
                    }

                    WebHeaderCollection responseHeaders = responseProperty.Headers;
                    for (int i = 0; i < responseHeaders.Count; i++)
                    {
                        string name = responseHeaders.Keys[i];
                        string value = responseHeaders[i];
                        if (string.Compare(name, "content-length", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            int contentLength = -1;
                            if (httpMethodIsHead &&
                                int.TryParse(value, out contentLength))
                            {
                                this.SetContentLength(contentLength);
                            }
                        }
                        else if (string.Compare(name, "content-type", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            if (httpMethodIsHead ||
                                !responseProperty.SuppressEntityBody)
                            {
                                this.SetContentType(value);
                            }
                        }
                        else if (string.Compare(name, "connection", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            SetConnection(value);
                        }
                        else
                        {
                            this.AddHeader(name, value);
                        }
                    }
                }

                return result;
            }

            protected override void AddHeader(string name, string value)
            {
                if (string.Compare(name, "WWW-Authenticate", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    httpResponse.Headers[name] = value;
                }
                else
                {
                    if(httpResponse.Headers.ContainsKey(name))
                    {
                        var previousValues = httpResponse.Headers[name];
                        httpResponse.Headers[name] = StringValues.Concat(previousValues, value);
                    }
                    else
                    {
                        httpResponse.Headers[name] = value;
                    }
                }
            }

            private void SetConnection(string connectionValue)
            {
                if ((connectionValue.Equals("keep-alive", StringComparison.OrdinalIgnoreCase) ||
                    connectionValue.Equals("close", StringComparison.OrdinalIgnoreCase)) &&
                    httpResponse.Headers.ContainsKey("Connection"))
                {
                    // Need to remove existing keep-alive and/or close values
                    StringValues connectionHeaderValue;
                    var previousValues = httpResponse.Headers["Connection"];
                    for (int i = 0; i < previousValues.Count; i++)
                    {
                        if (previousValues[i].Equals("keep-alive", StringComparison.OrdinalIgnoreCase) ||
                            previousValues[i].Equals("close", StringComparison.OrdinalIgnoreCase))
                        {
                            continue; // Don't add to new connectionHeaderValue as it will conflict with new value
                        }

                        connectionHeaderValue = StringValues.Concat(connectionHeaderValue, previousValues[i]);
                    }

                    connectionHeaderValue = StringValues.Concat(connectionHeaderValue, connectionValue);
                    httpResponse.Headers["Connection"] = connectionHeaderValue;
                }
                else
                {
                    AddHeader("Connection", connectionValue);
                }
            }

            protected override void SetContentType(string contentType)
            {
                httpResponse.ContentType = contentType;
            }

            protected override void SetContentEncoding(string contentEncoding)
            {
                httpResponse.Headers[HttpChannelUtilities.ContentEncodingHeader] = contentEncoding;
            }

            protected override void SetContentLength(int contentLength)
            {
                httpResponse.ContentLength = contentLength;
            }

            protected override void SetStatusCode(HttpStatusCode statusCode)
            {
                httpResponse.StatusCode = (int)statusCode;
            }

            protected override void SetStatusDescription(string statusDescription)
            {
                httpResponse.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = statusDescription;
            }

            protected override Stream GetOutputStream()
            {
                return httpResponse.Body;
            }
        }
    }

    enum HttpAbortReason
    {
        None,
        Aborted,
        TimedOut
    }


    static class HttpChannelUtilities
    {
        internal static class StatusDescriptionStrings
        {
                    internal const string HttpContentTypeMissing = "Missing Content Type";
                    internal const string HttpContentTypeMismatch = "Cannot process the message because the content type '{0}' was not the expected type '{1}'.";
        }

        internal const string HttpStatusCodeKey = "HttpStatusCode";
        internal const string HttpStatusCodeExceptionKey = "CoreWCF.Channels.HttpInput.HttpStatusCode";
        internal const string HttpStatusDescriptionExceptionKey = "CoreWCF.Channels.HttpInput.HttpStatusDescription";

        internal const string MIMEVersionHeader = "MIME-Version";

        internal const string ContentEncodingHeader = "Content-Encoding";
        internal const string AcceptEncodingHeader = "Accept-Encoding";

        public static Exception CreateCommunicationException(Exception exception)
        {
            return new CommunicationException(exception.Message, exception);
        }

        //    public static void EnsureHttpRequestMessageContentNotNull(HttpRequestMessage httpRequestMessage)
        //    {
        //        if (httpRequestMessage.Content == null)
        //        {
        //            httpRequestMessage.Content = new ByteArrayContent(EmptyArray<byte>.Instance);
        //        }
        //    }

        //    public static void EnsureHttpResponseMessageContentNotNull(HttpResponseMessage httpResponseMessage)
        //    {
        //        if (httpResponseMessage.Content == null)
        //        {
        //            httpResponseMessage.Content = new ByteArrayContent(EmptyArray<byte>.Instance);
        //        }
        //    }

        //    public static bool IsEmpty(HttpResponseMessage httpResponseMessage)
        //    {
        //        return httpResponseMessage.Content == null
        //           || (httpResponseMessage.Content.Headers.ContentLength.HasValue && httpResponseMessage.Content.Headers.ContentLength.Value == 0);
        //    }

        //    internal static void HandleContinueWithTask(Task task)
        //    {
        //        HandleContinueWithTask(task, null);
        //    }

        //    internal static void HandleContinueWithTask(Task task, Action<Exception> exceptionHandler)
        //    {
        //        if (task.IsFaulted)
        //        {
        //            if (exceptionHandler == null)
        //            {
        //                throw FxTrace.Exception.AsError<FaultException>(task.Exception);
        //            }
        //            else
        //            {
        //                exceptionHandler.Invoke(task.Exception);
        //            }
        //        }
        //        else if (task.IsCanceled)
        //        {
        //            throw FxTrace.Exception.AsError(new TimeoutException(SR.GetString(SR.TaskCancelledError)));
        //        }
        //    }

        //    public static void AbortRequest(HttpWebRequest request)
        //    {
        //        request.Abort();
        //    }

        //    public static void SetRequestTimeout(HttpWebRequest request, TimeSpan timeout)
        //    {
        //        int millisecondsTimeout = TimeoutHelper.ToMilliseconds(timeout);
        //        if (millisecondsTimeout == 0)
        //        {
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException(SR.GetString(
        //                SR.HttpRequestTimedOut, request.RequestUri, timeout)));
        //        }
        //        request.Timeout = millisecondsTimeout;
        //        request.ReadWriteTimeout = millisecondsTimeout;
        //    }

        //    public static void AddReplySecurityProperty(HttpChannelFactory<IRequestChannel> factory, HttpWebRequest webRequest,
        //        HttpWebResponse webResponse, Message replyMessage)
        //    {
        //        SecurityMessageProperty securityProperty = factory.CreateReplySecurityProperty(webRequest, webResponse);
        //        if (securityProperty != null)
        //        {
        //            replyMessage.Properties.Security = securityProperty;
        //        }
        //    }

        //    public static void CopyHeaders(HttpRequestMessage request, AddHeaderDelegate addHeader)
        //    {
        //        HttpChannelUtilities.CopyHeaders(request.Headers, addHeader);
        //        if (request.Content != null)
        //        {
        //            HttpChannelUtilities.CopyHeaders(request.Content.Headers, addHeader);
        //        }
        //    }

        //    public static void CopyHeaders(HttpResponseMessage response, AddHeaderDelegate addHeader)
        //    {
        //        HttpChannelUtilities.CopyHeaders(response.Headers, addHeader);
        //        if (response.Content != null)
        //        {
        //            HttpChannelUtilities.CopyHeaders(response.Content.Headers, addHeader);
        //        }
        //    }

        //    static void CopyHeaders(HttpHeaders headers, AddHeaderDelegate addHeader)
        //    {
        //        foreach (KeyValuePair<string, IEnumerable<string>> header in headers)
        //        {
        //            foreach (string value in header.Value)
        //            {
        //                TryAddToCollection(addHeader, header.Key, value);
        //            }
        //        }
        //    }

        //    public static void CopyHeaders(NameValueCollection headers, AddHeaderDelegate addHeader)
        //    {
        //        //this nested loop logic was copied from NameValueCollection.Add(NameValueCollection)
        //        int count = headers.Count;
        //        for (int i = 0; i < count; i++)
        //        {
        //            string key = headers.GetKey(i);

        //            string[] values = headers.GetValues(i);
        //            if (values != null)
        //            {
        //                for (int j = 0; j < values.Length; j++)
        //                {
        //                    TryAddToCollection(addHeader, key, values[j]);
        //                }
        //            }
        //            else
        //            {
        //                addHeader(key, null);
        //            }
        //        }
        //    }

        //    public static void CopyHeadersToNameValueCollection(NameValueCollection headers, NameValueCollection destination)
        //    {
        //        CopyHeaders(headers, destination.Add);
        //    }

        //    [System.Diagnostics.CodeAnalysis.SuppressMessage(FxCop.Category.ReliabilityBasic, "Reliability104",
        //                        Justification = "The exceptions are traced already.")]
        //    static void TryAddToCollection(AddHeaderDelegate addHeader, string headerName, string value)
        //    {
        //        try
        //        {
        //            addHeader(headerName, value);
        //        }
        //        catch (ArgumentException ex)
        //        {
        //            string encodedValue = null;
        //            if (TryEncodeHeaderValueAsUri(headerName, value, out encodedValue))
        //            {
        //                //note: if the hosthame of a referer header contains illegal chars, we will still throw from here
        //                //because Uri will not fix this up for us, which is ok. The request will get rejected in the error code path.
        //                addHeader(headerName, encodedValue);
        //            }
        //            else
        //            {
        //                // In self-hosted scenarios, some of the headers like Content-Length cannot be added directly.
        //                // It will throw ArgumentException instead.
        //                FxTrace.Exception.AsInformation(ex);
        //            }
        //        }
        //    }

        //    static bool TryEncodeHeaderValueAsUri(string headerName, string value, out string result)
        //    {
        //        result = null;
        //        //Internet Explorer will send the referrer header on the wire in unicode without encoding it
        //        //this will cause errors when added to a WebHeaderCollection.  This is a workaround for sharepoint, 
        //        //but will only work for WebHosted Scenarios.
        //        if (String.Compare(headerName, "Referer", StringComparison.OrdinalIgnoreCase) == 0)
        //        {
        //            Uri uri;
        //            if (Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out uri))
        //            {
        //                if (uri.IsAbsoluteUri)
        //                {
        //                    result = uri.AbsoluteUri;
        //                }
        //                else
        //                {
        //                    result = uri.GetComponents(UriComponents.SerializationInfoString, UriFormat.UriEscaped);
        //                }
        //                return true;
        //            }
        //        }
        //        return false;
        //    }

        //    //TODO, weixi, CSDMain 231775: Refactor the code for GetType logic in System.ServiceModel.dll and System.ServiceModel.Activation.dll
        //    internal static Type GetTypeFromAssembliesInCurrentDomain(string typeString)
        //    {
        //        Type type = Type.GetType(typeString, false);
        //        if (null == type)
        //        {
        //            if (!allReferencedAssembliesLoaded)
        //            {
        //                allReferencedAssembliesLoaded = true;
        //                AspNetEnvironment.Current.EnsureAllReferencedAssemblyLoaded();
        //            }

        //            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        //            for (int i = 0; i < assemblies.Length; i++)
        //            {
        //                type = assemblies[i].GetType(typeString, false);
        //                if (null != type)
        //                {
        //                    break;
        //                }
        //            }
        //        }

        //        return type;
        //    }

        //    public static NetworkCredential GetCredential(AuthenticationSchemes authenticationScheme,
        //        SecurityTokenProviderContainer credentialProvider, TimeSpan timeout,
        //        out TokenImpersonationLevel impersonationLevel, out AuthenticationLevel authenticationLevel)
        //    {
        //        impersonationLevel = TokenImpersonationLevel.None;
        //        authenticationLevel = AuthenticationLevel.None;

        //        NetworkCredential result = null;

        //        if (authenticationScheme != AuthenticationSchemes.Anonymous)
        //        {
        //            result = GetCredentialCore(authenticationScheme, credentialProvider, timeout, out impersonationLevel, out authenticationLevel);
        //        }

        //        return result;
        //    }

        //    [MethodImpl(MethodImplOptions.NoInlining)]
        //    static NetworkCredential GetCredentialCore(AuthenticationSchemes authenticationScheme,
        //        SecurityTokenProviderContainer credentialProvider, TimeSpan timeout,
        //        out TokenImpersonationLevel impersonationLevel, out AuthenticationLevel authenticationLevel)
        //    {
        //        impersonationLevel = TokenImpersonationLevel.None;
        //        authenticationLevel = AuthenticationLevel.None;

        //        NetworkCredential result = null;

        //        switch (authenticationScheme)
        //        {
        //            case AuthenticationSchemes.Basic:
        //                result = TransportSecurityHelpers.GetUserNameCredential(credentialProvider, timeout);
        //                impersonationLevel = TokenImpersonationLevel.Delegation;
        //                break;

        //            case AuthenticationSchemes.Digest:
        //                result = TransportSecurityHelpers.GetSspiCredential(credentialProvider, timeout,
        //                    out impersonationLevel, out authenticationLevel);

        //                HttpChannelUtilities.ValidateDigestCredential(ref result, impersonationLevel);
        //                break;

        //            case AuthenticationSchemes.Negotiate:
        //                result = TransportSecurityHelpers.GetSspiCredential(credentialProvider, timeout,
        //                    out impersonationLevel, out authenticationLevel);
        //                break;

        //            case AuthenticationSchemes.Ntlm:
        //                result = TransportSecurityHelpers.GetSspiCredential(credentialProvider, timeout,
        //                    out impersonationLevel, out authenticationLevel);
        //                if (authenticationLevel == AuthenticationLevel.MutualAuthRequired)
        //                {
        //                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
        //                        new InvalidOperationException(SR.GetString(SR.CredentialDisallowsNtlm)));
        //                }
        //                break;

        //            default:
        //                // The setter for this property should prevent this.
        //                throw Fx.AssertAndThrow("GetCredential: Invalid authentication scheme");
        //        }

        //        return result;
        //    }


        //    public static HttpWebResponse ProcessGetResponseWebException(WebException webException, HttpWebRequest request, HttpAbortReason abortReason)
        //    {
        //        HttpWebResponse response = null;

        //        if (webException.Status == WebExceptionStatus.Success ||
        //            webException.Status == WebExceptionStatus.ProtocolError)
        //        {
        //            response = (HttpWebResponse)webException.Response;
        //        }

        //        if (response == null)
        //        {
        //            Exception convertedException = ConvertWebException(webException, request, abortReason);

        //            if (convertedException != null)
        //            {
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(convertedException);
        //            }

        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(webException.Message,
        //                webException));
        //        }

        //        if (response.StatusCode == HttpStatusCode.NotFound)
        //        {
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new EndpointNotFoundException(SR.GetString(SR.EndpointNotFound, request.RequestUri.AbsoluteUri), webException));
        //        }

        //        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        //        {
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ServerTooBusyException(SR.GetString(SR.HttpServerTooBusy, request.RequestUri.AbsoluteUri), webException));
        //        }

        //        if (response.StatusCode == HttpStatusCode.UnsupportedMediaType)
        //        {
        //            string statusDescription = response.StatusDescription;
        //            if (!string.IsNullOrEmpty(statusDescription))
        //            {
        //                if (string.Compare(statusDescription, HttpChannelUtilities.StatusDescriptionStrings.HttpContentTypeMissing, StringComparison.OrdinalIgnoreCase) == 0)
        //                {
        //                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ProtocolException(SR.GetString(SR.MissingContentType, request.RequestUri), webException));
        //                }
        //            }
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ProtocolException(SR.GetString(SR.FramingContentTypeMismatch, request.ContentType, request.RequestUri), webException));
        //        }

        //        if (response.StatusCode == HttpStatusCode.GatewayTimeout)
        //        {
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException(webException.Message, webException));
        //        }

        //        // if http.sys has a request queue on the TCP port, then if the path fails to match it will send
        //        // back "<h1>Bad Request (Invalid Hostname)</h1>" in the body of a 400 response.
        //        // See code at \\index1\sddnsrv\net\http\sys\httprcv.c for details
        //        if (response.StatusCode == HttpStatusCode.BadRequest)
        //        {
        //            const string httpSysRequestQueueNotFound = "<h1>Bad Request (Invalid Hostname)</h1>";
        //            const string httpSysRequestQueueNotFoundVista = "<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.01//EN\"\"http://www.w3.org/TR/html4/strict.dtd\">\r\n<HTML><HEAD><TITLE>Bad Request</TITLE>\r\n<META HTTP-EQUIV=\"Content-Type\" Content=\"text/html; charset=us-ascii\"></HEAD>\r\n<BODY><h2>Bad Request - Invalid Hostname</h2>\r\n<hr><p>HTTP Error 400. The request hostname is invalid.</p>\r\n</BODY></HTML>\r\n";
        //            string notFoundTestString = null;

        //            if (response.ContentLength == httpSysRequestQueueNotFound.Length)
        //            {
        //                notFoundTestString = httpSysRequestQueueNotFound;
        //            }
        //            else if (response.ContentLength == httpSysRequestQueueNotFoundVista.Length)
        //            {
        //                notFoundTestString = httpSysRequestQueueNotFoundVista;
        //            }

        //            if (notFoundTestString != null)
        //            {
        //                Stream responseStream = response.GetResponseStream();
        //                byte[] responseBytes = new byte[notFoundTestString.Length];
        //                int bytesRead = responseStream.Read(responseBytes, 0, responseBytes.Length);

        //                // since the response is buffered by System.Net (it's an error response), we should have read
        //                // the amount we were expecting
        //                if (bytesRead == notFoundTestString.Length
        //                    && notFoundTestString == UTF8Encoding.ASCII.GetString(responseBytes))
        //                {
        //                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new EndpointNotFoundException(SR.GetString(SR.EndpointNotFound, request.RequestUri.AbsoluteUri), webException));
        //                }
        //            }
        //        }

        //        return response;
        //    }

        //    public static Exception ConvertWebException(WebException webException, HttpWebRequest request, HttpAbortReason abortReason)
        //    {
        //        switch (webException.Status)
        //        {
        //            case WebExceptionStatus.ConnectFailure:
        //            case WebExceptionStatus.NameResolutionFailure:
        //            case WebExceptionStatus.ProxyNameResolutionFailure:
        //                return new EndpointNotFoundException(SR.GetString(SR.EndpointNotFound, request.RequestUri.AbsoluteUri), webException);
        //            case WebExceptionStatus.SecureChannelFailure:
        //                return new SecurityNegotiationException(SR.GetString(SR.SecureChannelFailure, request.RequestUri.Authority), webException);
        //            case WebExceptionStatus.TrustFailure:
        //                return new SecurityNegotiationException(SR.GetString(SR.TrustFailure, request.RequestUri.Authority), webException);
        //            case WebExceptionStatus.Timeout:
        //                return new TimeoutException(CreateRequestTimedOutMessage(request), webException);
        //            case WebExceptionStatus.ReceiveFailure:
        //                return new CommunicationException(SR.GetString(SR.HttpReceiveFailure, request.RequestUri), webException);
        //            case WebExceptionStatus.SendFailure:
        //                return new CommunicationException(SR.GetString(SR.HttpSendFailure, request.RequestUri), webException);
        //            case WebExceptionStatus.RequestCanceled:
        //                return CreateRequestCanceledException(webException, request, abortReason);
        //            case WebExceptionStatus.ProtocolError:
        //                HttpWebResponse response = (HttpWebResponse)webException.Response;
        //                Fx.Assert(response != null, "'response' MUST NOT be NULL for WebExceptionStatus=='ProtocolError'.");
        //                if (response.StatusCode == HttpStatusCode.InternalServerError &&
        //                    string.Compare(response.StatusDescription, HttpChannelUtilities.StatusDescriptionStrings.HttpStatusServiceActivationException, StringComparison.OrdinalIgnoreCase) == 0)
        //                {
        //                    return new ServiceActivationException(SR.GetString(SR.Hosting_ServiceActivationFailed, request.RequestUri));
        //                }
        //                else
        //                {
        //                    return null;
        //                }
        //            default:
        //                return null;
        //        }
        //    }

        //    public static Exception CreateResponseIOException(IOException ioException, TimeSpan receiveTimeout)
        //    {
        //        if (ioException.InnerException is SocketException)
        //        {
        //            return SocketConnection.ConvertTransferException((SocketException)ioException.InnerException, receiveTimeout, ioException);
        //        }

        //        return new CommunicationException(SR.GetString(SR.HttpTransferError, ioException.Message), ioException);
        //    }

        //    public static Exception CreateResponseWebException(WebException webException, HttpWebResponse response)
        //    {
        //        switch (webException.Status)
        //        {
        //            case WebExceptionStatus.RequestCanceled:
        //                return TraceResponseException(new CommunicationObjectAbortedException(SR.GetString(SR.HttpRequestAborted, response.ResponseUri), webException));
        //            case WebExceptionStatus.ConnectionClosed:
        //                return TraceResponseException(new CommunicationException(webException.Message, webException));
        //            case WebExceptionStatus.Timeout:
        //                return TraceResponseException(new TimeoutException(SR.GetString(SR.HttpResponseTimedOut, response.ResponseUri,
        //                    TimeSpan.FromMilliseconds(response.GetResponseStream().ReadTimeout)), webException));
        //            default:
        //                return CreateUnexpectedResponseException(webException, response);
        //        }
        //    }

        //    public static Exception CreateRequestCanceledException(Exception webException, HttpWebRequest request, HttpAbortReason abortReason)
        //    {
        //        switch (abortReason)
        //        {
        //            case HttpAbortReason.Aborted:
        //                return new CommunicationObjectAbortedException(SR.GetString(SR.HttpRequestAborted, request.RequestUri), webException);
        //            case HttpAbortReason.TimedOut:
        //                return new TimeoutException(CreateRequestTimedOutMessage(request), webException);
        //            default:
        //                return new CommunicationException(SR.GetString(SR.HttpTransferError, webException.Message), webException);
        //        }
        //    }

        //    public static Exception CreateRequestIOException(IOException ioException, HttpWebRequest request)
        //    {
        //        return CreateRequestIOException(ioException, request, null);
        //    }

        //    public static Exception CreateRequestIOException(IOException ioException, HttpWebRequest request, Exception originalException)
        //    {
        //        Exception exception = originalException == null ? ioException : originalException;

        //        if (ioException.InnerException is SocketException)
        //        {
        //            return SocketConnection.ConvertTransferException((SocketException)ioException.InnerException, TimeSpan.FromMilliseconds(request.Timeout), exception);
        //        }

        //        return new CommunicationException(SR.GetString(SR.HttpTransferError, exception.Message), exception);
        //    }

        //    static string CreateRequestTimedOutMessage(HttpWebRequest request)
        //    {
        //        return SR.GetString(SR.HttpRequestTimedOut, request.RequestUri, TimeSpan.FromMilliseconds(request.Timeout));
        //    }

        //    public static Exception CreateRequestWebException(WebException webException, HttpWebRequest request, HttpAbortReason abortReason)
        //    {
        //        Exception convertedException = ConvertWebException(webException, request, abortReason);

        //        if (webException.Response != null)
        //        {
        //            //free the connection for use by another request
        //            webException.Response.Close();
        //        }

        //        if (convertedException != null)
        //        {
        //            return convertedException;
        //        }

        //        if (webException.InnerException is IOException)
        //        {
        //            return CreateRequestIOException((IOException)webException.InnerException, request, webException);
        //        }

        //        if (webException.InnerException is SocketException)
        //        {
        //            return SocketConnectionInitiator.ConvertConnectException((SocketException)webException.InnerException, request.RequestUri, TimeSpan.MaxValue, webException);
        //        }

        //        return new EndpointNotFoundException(SR.GetString(SR.EndpointNotFound, request.RequestUri.AbsoluteUri), webException);
        //    }

        //    static Exception CreateUnexpectedResponseException(WebException responseException, HttpWebResponse response)
        //    {
        //        string statusDescription = response.StatusDescription;
        //        if (string.IsNullOrEmpty(statusDescription))
        //            statusDescription = response.StatusCode.ToString();

        //        return TraceResponseException(
        //            new ProtocolException(SR.GetString(SR.UnexpectedHttpResponseCode,
        //            (int)response.StatusCode, statusDescription), responseException));
        //    }

        //    public static Exception CreateNullReferenceResponseException(NullReferenceException nullReferenceException)
        //    {
        //        return TraceResponseException(
        //            new ProtocolException(SR.GetString(SR.NullReferenceOnHttpResponse), nullReferenceException));
        //    }

        //    static string GetResponseStreamString(HttpWebResponse webResponse, out int bytesRead)
        //    {
        //        Stream responseStream = webResponse.GetResponseStream();

        //        long bufferSize = webResponse.ContentLength;

        //        if (bufferSize < 0 || bufferSize > ResponseStreamExcerptSize)
        //        {
        //            bufferSize = ResponseStreamExcerptSize;
        //        }

        //        byte[] responseBuffer = DiagnosticUtility.Utility.AllocateByteArray(checked((int)bufferSize));
        //        bytesRead = responseStream.Read(responseBuffer, 0, (int)bufferSize);
        //        responseStream.Close();

        //        return System.Text.Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);
        //    }

        //    static Exception TraceResponseException(Exception exception)
        //    {
        //        if (DiagnosticUtility.ShouldTraceError)
        //        {
        //            TraceUtility.TraceEvent(TraceEventType.Error, TraceCode.HttpChannelUnexpectedResponse, SR.GetString(SR.TraceCodeHttpChannelUnexpectedResponse), (object)null, exception);
        //        }

        //        return exception;
        //    }

        //    static bool ValidateEmptyContent(HttpWebResponse response)
        //    {
        //        bool responseIsEmpty = true;

        //        if (response.ContentLength > 0)
        //        {
        //            responseIsEmpty = false;
        //        }
        //        else if (response.ContentLength == -1) // chunked 
        //        {
        //            Stream responseStream = response.GetResponseStream();
        //            byte[] testBuffer = new byte[1];
        //            responseIsEmpty = (responseStream.Read(testBuffer, 0, 1) != 1);
        //        }

        //        return responseIsEmpty;
        //    }

        //    static void ValidateAuthentication(HttpWebRequest request, HttpWebResponse response,
        //        WebException responseException, HttpChannelFactory<IRequestChannel> factory)
        //    {
        //        if (response.StatusCode == HttpStatusCode.Unauthorized)
        //        {
        //            string message = SR.GetString(SR.HttpAuthorizationFailed, factory.AuthenticationScheme,
        //                response.Headers[HttpResponseHeader.WwwAuthenticate]);
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
        //                TraceResponseException(new MessageSecurityException(message, responseException)));
        //        }

        //        if (response.StatusCode == HttpStatusCode.Forbidden)
        //        {
        //            string message = SR.GetString(SR.HttpAuthorizationForbidden, factory.AuthenticationScheme);
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
        //                TraceResponseException(new MessageSecurityException(message, responseException)));
        //        }

        //        if ((request.AuthenticationLevel == AuthenticationLevel.MutualAuthRequired) &&
        //            !response.IsMutuallyAuthenticated)
        //        {
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
        //                TraceResponseException(new SecurityNegotiationException(SR.GetString(SR.HttpMutualAuthNotSatisfied),
        //                responseException)));
        //        }
        //    }

        //    public static void ValidateDigestCredential(ref NetworkCredential credential, TokenImpersonationLevel impersonationLevel)
        //    {
        //        // this is a work-around to VSWhidbey#470545 (Since the service always uses Impersonation,
        //        // we mitigate EOP by preemptively not allowing Identification)
        //        if (!SecurityUtils.IsDefaultNetworkCredential(credential))
        //        {
        //            // With a non-default credential, Digest will not honor a client impersonation constraint of 
        //            // TokenImpersonationLevel.Identification.
        //            if (!TokenImpersonationLevelHelper.IsGreaterOrEqual(impersonationLevel,
        //                TokenImpersonationLevel.Impersonation))
        //            {
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.GetString(
        //                    SR.DigestExplicitCredsImpersonationLevel, impersonationLevel)));
        //            }
        //        }
        //    }

        //    // only valid response codes are 500 (if it's a fault) or 200 (iff it's a response message)
        //    public static HttpInput ValidateRequestReplyResponse(HttpWebRequest request, HttpWebResponse response,
        //        HttpChannelFactory<IRequestChannel> factory, WebException responseException, ChannelBinding channelBinding)
        //    {
        //        ValidateAuthentication(request, response, responseException, factory);

        //        HttpInput httpInput = null;

        //        // We will close the HttpWebResponse if we got an error code betwen 200 and 300 and 
        //        // 1) an exception was thrown out or 
        //        // 2) it's an empty message and we are using SOAP.
        //        // For responses with status code above 300, System.Net will close the underlying connection so we don't need to worry about that.
        //        if ((200 <= (int)response.StatusCode && (int)response.StatusCode < 300) || response.StatusCode == HttpStatusCode.InternalServerError)
        //        {
        //            if (response.StatusCode == HttpStatusCode.InternalServerError
        //                && string.Compare(response.StatusDescription, HttpChannelUtilities.StatusDescriptionStrings.HttpStatusServiceActivationException, StringComparison.OrdinalIgnoreCase) == 0)
        //            {
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ServiceActivationException(SR.GetString(SR.Hosting_ServiceActivationFailed, request.RequestUri)));
        //            }
        //            else
        //            {
        //                bool throwing = true;
        //                try
        //                {
        //                    if (string.IsNullOrEmpty(response.ContentType))
        //                    {
        //                        if (!ValidateEmptyContent(response))
        //                        {
        //                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(TraceResponseException(
        //                                new ProtocolException(
        //                                    SR.GetString(SR.HttpContentTypeHeaderRequired),
        //                                    responseException)));
        //                        }
        //                    }
        //                    else if (response.ContentLength != 0)
        //                    {
        //                        MessageEncoder encoder = factory.MessageEncoderFactory.Encoder;
        //                        if (!encoder.IsContentTypeSupported(response.ContentType))
        //                        {
        //                            int bytesRead;
        //                            String responseExcerpt = GetResponseStreamString(response, out bytesRead);

        //                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(TraceResponseException(
        //                                new ProtocolException(
        //                                    SR.GetString(
        //                                        SR.ResponseContentTypeMismatch,
        //                                        response.ContentType,
        //                                        encoder.ContentType,
        //                                        bytesRead,
        //                                        responseExcerpt), responseException)));

        //                        }

        //                        httpInput = HttpInput.CreateHttpInput(response, factory, channelBinding);
        //                        httpInput.WebException = responseException;
        //                    }

        //                    throwing = false;
        //                }
        //                finally
        //                {
        //                    if (throwing)
        //                    {
        //                        response.Close();
        //                    }
        //                }
        //            }

        //            if (httpInput == null)
        //            {
        //                if (factory.MessageEncoderFactory.MessageVersion == MessageVersion.None)
        //                {
        //                    httpInput = HttpInput.CreateHttpInput(response, factory, channelBinding);
        //                    httpInput.WebException = responseException;
        //                }
        //                else
        //                {
        //                    // In this case, we got a response with
        //                    // 1) status code between 200 and 300
        //                    // 2) Non-empty Content Type string
        //                    // 3) Zero content length
        //                    // Since we are trying to use SOAP here, the message seems to be malicious and we should
        //                    // just close the response directly.
        //                    response.Close();
        //                }
        //            }
        //        }
        //        else
        //        {
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateUnexpectedResponseException(responseException, response));
        //        }

        //        return httpInput;
        //    }

        public static bool GetHttpResponseTypeAndEncodingForCompression(ref string contentType, out string contentEncoding)
        {
            contentEncoding = null;
            bool isSession = false;
            bool isDeflate = false;

            if (string.Equals(BinaryVersion.GZipVersion1.ContentType, contentType, StringComparison.OrdinalIgnoreCase) ||
                (isSession = string.Equals(BinaryVersion.GZipVersion1.SessionContentType, contentType, StringComparison.OrdinalIgnoreCase)) ||
                (isDeflate = (string.Equals(BinaryVersion.DeflateVersion1.ContentType, contentType, StringComparison.OrdinalIgnoreCase) ||
                (isSession = string.Equals(BinaryVersion.DeflateVersion1.SessionContentType, contentType, StringComparison.OrdinalIgnoreCase)))))
            {
                contentType = isSession ? BinaryVersion.Version1.SessionContentType : BinaryVersion.Version1.ContentType;
                contentEncoding = isDeflate ? MessageEncoderCompressionHandler.DeflateContentEncoding : MessageEncoderCompressionHandler.GZipContentEncoding;
                return true;
            }
            return false;
        }
    }

    //abstract class HttpDelayedAcceptStream : DetectEofStream
    //{
    //    HttpOutput httpOutput;
    //    bool isHttpOutputClosed;

    //    /// <summary>
    //    /// Indicates whether the HttpOutput should be closed when this stream is closed. In the streamed case, 
    //    /// we�ll leave the HttpOutput opened (and it will be closed by the HttpRequestContext, so we won't leak it).
    //    /// </summary>
    //    bool closeHttpOutput;

    //    // sometimes we can't flush the HTTP output until we're done reading the end of the 
    //    // incoming stream of the HTTP input
    //    protected HttpDelayedAcceptStream(Stream stream)
    //        : base(stream)
    //    {
    //    }

    //    public bool EnableDelayedAccept(HttpOutput output, bool closeHttpOutput)
    //    {
    //        if (IsAtEof)
    //        {
    //            return false;
    //        }

    //        this.closeHttpOutput = closeHttpOutput;
    //        this.httpOutput = output;
    //        return true;
    //    }

    //    protected override void OnReceivedEof()
    //    {
    //        if (this.closeHttpOutput)
    //        {
    //            CloseHttpOutput();
    //        }
    //    }

    //    public override void Close()
    //    {
    //        if (this.closeHttpOutput)
    //        {
    //            CloseHttpOutput();
    //        }

    //        base.Close();
    //    }

    //    void CloseHttpOutput()
    //    {
    //        if (this.httpOutput != null && !this.isHttpOutputClosed)
    //        {
    //            this.httpOutput.Close();
    //            this.isHttpOutputClosed = true;
    //        }
    //    }
    //}

    //abstract class BytesReadPositionStream : DelegatingStream
    //{
    //    int bytesSent = 0;

    //    protected BytesReadPositionStream(Stream stream)
    //        : base(stream)
    //    {
    //    }

    //    public override long Position
    //    {
    //        get
    //        {
    //            return bytesSent;
    //        }
    //        set
    //        {
    //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.GetString(SR.SeekNotSupported)));
    //        }
    //    }

    //    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
    //    {
    //        this.bytesSent += count;
    //        return BaseStream.BeginWrite(buffer, offset, count, callback, state);
    //    }

    //    public override void Write(byte[] buffer, int offset, int count)
    //    {
    //        BaseStream.Write(buffer, offset, count);
    //        this.bytesSent += count;
    //    }

    //    public override void WriteByte(byte value)
    //    {
    //        BaseStream.WriteByte(value);
    //        this.bytesSent++;
    //    }
    //}

    class PreReadStream : DelegatingStream
    {
        byte[] preReadBuffer;

        public PreReadStream(Stream stream, byte[] preReadBuffer)
            : base(stream)
        {
            this.preReadBuffer = preReadBuffer;
        }

        bool ReadFromBuffer(byte[] buffer, int offset, int count, out int bytesRead)
        {
            if (this.preReadBuffer != null)
            {
                if (buffer == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(buffer));
                }

                if (offset >= buffer.Length)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), offset,
                        SR.Format(SR.OffsetExceedsBufferBound, buffer.Length - 1)));
                }

                if (count < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), count,
                        SR.Format(SR.ValueMustBeNonNegative)));
                }

                if (count == 0)
                {
                    bytesRead = 0;
                }
                else
                {
                    buffer[offset] = this.preReadBuffer[0];
                    this.preReadBuffer = null;
                    bytesRead = 1;
                }

                return true;
            }

            bytesRead = -1;
            return false;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead;
            if (ReadFromBuffer(buffer, offset, count, out bytesRead))
            {
                return bytesRead;
            }

            return base.Read(buffer, offset, count);
        }

        public override int ReadByte()
        {
            if (this.preReadBuffer != null)
            {
                byte[] tempBuffer = new byte[1];
                int bytesRead;
                if (ReadFromBuffer(tempBuffer, 0, 1, out bytesRead))
                {
                    return tempBuffer[0];
                }
            }
            return base.ReadByte();
        }
    }

    //class HttpRequestMessageHttpInput : HttpInput, HttpRequestMessageProperty.IHttpHeaderProvider
    //{
    //    const string SoapAction = "SOAPAction";
    //    HttpRequestMessage httpRequestMessage;
    //    ChannelBinding channelBinding;

    //    public HttpRequestMessageHttpInput(HttpRequestMessage httpRequestMessage, IHttpTransportFactorySettings settings, bool enableChannelBinding, ChannelBinding channelBinding)
    //        : base(settings, true, enableChannelBinding)
    //    {
    //        this.httpRequestMessage = httpRequestMessage;
    //        this.channelBinding = channelBinding;
    //    }

    //    public override long ContentLength
    //    {
    //        get
    //        {
    //            if (this.httpRequestMessage.Content.Headers.ContentLength == null)
    //            {
    //                // Chunked transfer mode
    //                return -1;
    //            }

    //            return this.httpRequestMessage.Content.Headers.ContentLength.Value;
    //        }
    //    }

    //    protected override ChannelBinding ChannelBinding
    //    {
    //        get
    //        {
    //            return this.channelBinding;
    //        }
    //    }

    //    public HttpRequestMessage HttpRequestMessage
    //    {
    //        get { return this.httpRequestMessage; }
    //    }

    //    protected override bool HasContent
    //    {
    //        get
    //        {
    //            // In Chunked transfer mode, the ContentLength header is null
    //            // Otherwise we just rely on the ContentLength header
    //            return this.httpRequestMessage.Content.Headers.ContentLength == null || this.httpRequestMessage.Content.Headers.ContentLength.Value > 0;
    //        }
    //    }

    //    protected override string ContentTypeCore
    //    {
    //        get
    //        {
    //            if (!this.HasContent)
    //            {
    //                return null;
    //            }

    //            return this.httpRequestMessage.Content.Headers.ContentType == null ? null : this.httpRequestMessage.Content.Headers.ContentType.MediaType;
    //        }
    //    }

    //    public override void ConfigureHttpRequestMessage(HttpRequestMessage message)
    //    {
    //        throw FxTrace.Exception.AsError(new InvalidOperationException());
    //    }

    //    protected override Stream GetInputStream()
    //    {
    //        if (this.httpRequestMessage.Content == null)
    //        {
    //            return Stream.Null;
    //        }

    //        return this.httpRequestMessage.Content.ReadAsStreamAsync().Result;
    //    }

    //    protected override void AddProperties(Message message)
    //    {
    //        HttpRequestMessageProperty requestProperty = new HttpRequestMessageProperty(this.httpRequestMessage);
    //        message.Properties.Add(HttpRequestMessageProperty.Name, requestProperty);
    //        message.Properties.Via = this.httpRequestMessage.RequestUri;

    //        foreach (KeyValuePair<string, object> property in this.httpRequestMessage.Properties)
    //        {
    //            message.Properties.Add(property.Key, property.Value);
    //        }

    //        this.httpRequestMessage.Properties.Clear();
    //    }

    //    protected override string SoapActionHeader
    //    {
    //        get
    //        {
    //            IEnumerable<string> values;
    //            if (this.httpRequestMessage.Headers.TryGetValues(SoapAction, out values))
    //            {
    //                foreach (string headerValue in values)
    //                {
    //                    return headerValue;
    //                }
    //            }

    //            return null;
    //        }
    //    }

    //    public void CopyHeaders(WebHeaderCollection headers)
    //    {
    //        // No special-casing for the "WWW-Authenticate" header required here,
    //        // because this method is only called for the incoming request
    //        // and the WWW-Authenticate header is a header only applied to responses.
    //        HttpChannelUtilities.CopyHeaders(this.httpRequestMessage, headers.Add);
    //    }

    //    internal void SetHttpRequestMessage(HttpRequestMessage httpRequestMessage)
    //    {
    //        Fx.Assert(httpRequestMessage != null, "httpRequestMessage should not be null.");
    //        this.httpRequestMessage = httpRequestMessage;
    //    }
    //}
}

