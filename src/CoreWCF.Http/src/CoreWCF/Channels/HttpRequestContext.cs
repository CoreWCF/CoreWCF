﻿using Microsoft.AspNetCore.Http;
using CoreWCF.Runtime;
using CoreWCF.Security;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Authentication.ExtendedProtection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CoreWCF.Channels
{
    abstract class HttpRequestContext : RequestContextBase
    {
        HttpOutput httpOutput;
        bool errorGettingHttpInput;
        SecurityMessageProperty securityProperty;
        //EventTraceActivity eventTraceActivity;
        //ServerWebSocketTransportDuplexSessionChannel webSocketChannel;

        protected HttpRequestContext(IHttpTransportFactorySettings settings, Message requestMessage)
            : base(requestMessage, settings.CloseTimeout, settings.SendTimeout)
        {
            HttpTransportSettings = settings;
        }

        public bool KeepAliveEnabled
        {
            get
            {
                return HttpTransportSettings.KeepAliveEnabled;
            }
        }

        public abstract string HttpMethod { get; }
        public abstract bool IsWebSocketRequest { get; }

        //internal ServerWebSocketTransportDuplexSessionChannel WebSocketChannel
        //{
        //    get
        //    {
        //        return this.webSocketChannel;
        //    }

        //    set
        //    {
        //        Fx.Assert(this.webSocketChannel == null, "webSocketChannel should not be set twice.");
        //        this.webSocketChannel = value;
        //    }
        //}

        internal IHttpTransportFactorySettings HttpTransportSettings { get; }

        //internal EventTraceActivity EventTraceActivity
        //{
        //    get
        //    {
        //        return this.eventTraceActivity;
        //    }
        //}

        // Note: This method will return null in the case where throwOnError is false, and a non-fatal error occurs.
        // Please exercise caution when passing in throwOnError = false.  This should basically only be done in error
        // code paths, or code paths where there is very good reason that you would not want this method to throw.
        // When passing in throwOnError = false, please handle the case where this method returns null.
        public HttpInput GetHttpInput(bool throwOnError)
        {
            HttpInput httpInput = null;
            if (throwOnError || !this.errorGettingHttpInput)
            {
                try
                {
                    httpInput = GetHttpInput();
                    this.errorGettingHttpInput = false;
                }
                catch (Exception e)
                {
                    this.errorGettingHttpInput = true;
                    if (throwOnError || Fx.IsFatal(e))
                    {
                        throw;
                    }

                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Warning);
                }
            }

            return httpInput;
        }

        internal static HttpRequestContext CreateContext(IHttpTransportFactorySettings settings, HttpContext httpContext)
        {
            return new AspNetCoreHttpContext(settings, httpContext);
        }

        protected abstract SecurityMessageProperty OnProcessAuthentication();

        public abstract HttpOutput GetHttpOutput(Message message);

        protected abstract HttpInput GetHttpInput();

        public HttpOutput GetHttpOutputCore(Message message)
        {
            if (this.httpOutput != null)
            {
                return this.httpOutput;
            }

            return this.GetHttpOutput(message);
        }

        protected override void OnAbort()
        {
            if (this.httpOutput != null)
            {
                this.httpOutput.Abort(HttpAbortReason.Aborted);
            }

            this.Cleanup();
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            try
            {
                if (this.httpOutput != null)
                {
                    await httpOutput.CloseAsync(); ;
                }
            }
            finally
            {
                this.Cleanup();
            }
        }

        protected virtual void Cleanup()
        {

        }

        internal void SetMessage(Message message, Exception requestException)
        {
            if ((message == null) && (requestException == null))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ProtocolException(SR.MessageXmlProtocolError,
                    new XmlException(SR.MessageIsEmpty)));
            }

            this.TraceHttpMessageReceived(message);

            if (requestException != null)
            {
                base.SetRequestMessage(requestException);
                message.Close();
            }
            else
            {
                message.Properties.Security = (this.securityProperty != null) ? (SecurityMessageProperty)this.securityProperty.CreateCopy() : null;
                base.SetRequestMessage(message);
            }
        }

        void TraceHttpMessageReceived(Message message)
        {
        }

        protected abstract HttpStatusCode ValidateAuthentication();

        bool PrepareReply(ref Message message)
        {
            bool closeOnReceivedEof = false;

            // null means we're done
            if (message == null)
            {
                // A null message means either a one-way request or that the service operation returned null and
                // hence we can close the HttpOutput. By default we keep the HttpOutput open to allow the writing to the output 
                // even after the HttpInput EOF is received and the HttpOutput will be closed only on close of the HttpRequestContext.
                closeOnReceivedEof = true;
                message = CreateAckMessage(HttpStatusCode.Accepted, string.Empty);
            }

            if (!HttpTransportSettings.ManualAddressing)
            {
                if (message.Version.Addressing == AddressingVersion.WSAddressingAugust2004)
                {
                    if (message.Headers.To == null ||
                        (HttpTransportSettings.AnonymousUriPrefixMatcher as HttpAnonymousUriPrefixMatcher) == null ||
                        !(HttpTransportSettings.AnonymousUriPrefixMatcher as HttpAnonymousUriPrefixMatcher).IsAnonymousUri(message.Headers.To))
                    {
                        message.Headers.To = message.Version.Addressing.AnonymousUri;
                    }
                }
                else if (message.Version.Addressing == AddressingVersion.WSAddressing10
                    || message.Version.Addressing == AddressingVersion.None)
                {
                    if (message.Headers.To != null &&
                        (HttpTransportSettings.AnonymousUriPrefixMatcher as HttpAnonymousUriPrefixMatcher == null ||
                        !(HttpTransportSettings.AnonymousUriPrefixMatcher as HttpAnonymousUriPrefixMatcher).IsAnonymousUri(message.Headers.To)))
                    {
                        message.Headers.To = null;
                    }
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new ProtocolException(SR.Format(SR.AddressingVersionNotSupported, message.Version.Addressing)));
                }
            }

            message.Properties.AllowOutputBatching = false;
            this.httpOutput = GetHttpOutputCore(message);

            return closeOnReceivedEof;
        }

        protected override async Task OnReplyAsync(Message message, CancellationToken token)
        {
            Message responseMessage = message;

            try
            {
                bool closeOutputAfterReply = PrepareReply(ref responseMessage);
                httpOutput = this.GetHttpOutput(message);
                await httpOutput.SendAsync(token);

                if (closeOutputAfterReply)
                {
                    await httpOutput.CloseAsync();
                }
            }
            finally
            {
                if (message != null &&
                    !object.ReferenceEquals(message, responseMessage))
                {
                    responseMessage.Close();
                }
            }
        }

        public async Task<bool> ProcessAuthenticationAsync()
        {
            HttpStatusCode statusCode = ValidateAuthentication();

            if (statusCode == HttpStatusCode.OK)
            {
                bool authenticationSucceeded = false;
                statusCode = HttpStatusCode.Forbidden;
                try
                {
                    this.securityProperty = OnProcessAuthentication();
                    authenticationSucceeded = true;
                    return true;
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }

                    if (e.Data.Contains(HttpChannelUtilities.HttpStatusCodeKey))
                    {
                        if (e.Data[HttpChannelUtilities.HttpStatusCodeKey] is HttpStatusCode)
                        {
                            statusCode = (HttpStatusCode)e.Data[HttpChannelUtilities.HttpStatusCodeKey];
                        }
                    }

                    throw;
                }
                finally
                {
                    if (!authenticationSucceeded)
                    {
                        await SendResponseAndCloseAsync(statusCode);
                    }
                }
            }
            else
            {
                await SendResponseAndCloseAsync(statusCode);
                return false;
            }
        }

        internal Task SendResponseAndCloseAsync(HttpStatusCode statusCode)
        {
            return SendResponseAndCloseAsync(statusCode, string.Empty);
        }

        internal async Task SendResponseAndCloseAsync(HttpStatusCode statusCode, string statusDescription)
        {
            if (ReplyInitiated)
            {
                await CloseAsync();
                return;
            }

            using (Message ackMessage = CreateAckMessage(statusCode, statusDescription))
            {
                await ReplyAsync(ackMessage);
            }

            await CloseAsync();
        }

        Message CreateAckMessage(HttpStatusCode statusCode, string statusDescription)
        {
            Message ackMessage = new NullMessage();
            HttpResponseMessageProperty httpResponseProperty = new HttpResponseMessageProperty();
            httpResponseProperty.StatusCode = statusCode;
            httpResponseProperty.SuppressEntityBody = true;
            if (statusDescription.Length > 0)
            {
                httpResponseProperty.StatusDescription = statusDescription;
            }

            ackMessage.Properties.Add(HttpResponseMessageProperty.Name, httpResponseProperty);

            return ackMessage;
        }

        class AspNetCoreHttpContext : HttpRequestContext
        {
            HttpContext _aspNetContext;
            // byte[] webSocketInternalBuffer;

            public AspNetCoreHttpContext(IHttpTransportFactorySettings settings, HttpContext aspNetContext)
                : base(settings, null)
            {
                _aspNetContext = aspNetContext;
            }

            public override string HttpMethod => _aspNetContext.Request.Method;

            public override bool IsWebSocketRequest => false;

            protected override HttpInput GetHttpInput()
            {
                return new AspNetCoreHttpInput(this);
            }
            public override HttpOutput GetHttpOutput(Message message)
            {
                // TODO: Enable KeepAlive setting
                //if (!_httpBindingElement.KeepAlive)
                //{
                //    aspNetContext.Response.Headers["Connection"] = "close";
                //}

                ICompressedMessageEncoder compressedMessageEncoder = HttpTransportSettings.MessageEncoderFactory.Encoder as ICompressedMessageEncoder;
                if (compressedMessageEncoder != null && compressedMessageEncoder.CompressionEnabled)
                {
                    string acceptEncoding = _aspNetContext.Request.Headers[HttpChannelUtilities.AcceptEncodingHeader];
                    compressedMessageEncoder.AddCompressedMessageProperties(message, acceptEncoding);
                }

                return HttpOutput.CreateHttpOutput(_aspNetContext, HttpTransportSettings, message, HttpMethod);
            }

            protected override SecurityMessageProperty OnProcessAuthentication()
            {
                // TODO: Wire up authentication for ASP.Net Core
                //return Listener.ProcessAuthentication(listenerContext);
                return null;
            }

            protected override HttpStatusCode ValidateAuthentication()
            {
                // TODO: Wire up authentication for ASP.Net Core
                return HttpStatusCode.OK;
                //return Listener.ValidateAuthentication(listenerContext);
            }

            protected override void OnAbort()
            {
                _aspNetContext.Abort();
                this.Cleanup();
            }

            protected override Task OnCloseAsync(CancellationToken token)
            {
                return base.OnCloseAsync(token);
                //try
                //{
                //    // TODO: Work out how to close the HttpContext
                //    // Most likely will be some mechanism to complete the Task returned by the RequestDelegate
                //    aspNetContext.Response.Close();
                //}
                //catch (HttpListenerException listenerException)
                //{
                //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                //        HttpChannelUtilities.CreateCommunicationException(listenerException));
                //}
            }

            class AspNetCoreHttpInput : HttpInput
            {
                AspNetCoreHttpContext _aspNetCoreHttpContext;
                string cachedContentType; // accessing the header in System.Net involves a native transition
                byte[] preReadBuffer;

                // TODO: ChannelBindingSupport
                public AspNetCoreHttpInput(AspNetCoreHttpContext aspNetCoreHttpContext)
                    : base(aspNetCoreHttpContext.HttpTransportSettings, true, false /* ChannelBindingSupportEnabled */) 
                {
                    _aspNetCoreHttpContext = aspNetCoreHttpContext;
                    if (!this._aspNetCoreHttpContext._aspNetContext.Request.ContentLength.HasValue)
                    {
                        // TODO: Look into useing PipeReader with look-ahead
                        this.preReadBuffer = new byte[1];
                        if (_aspNetCoreHttpContext._aspNetContext.Request.Body.Read(preReadBuffer, 0, 1) == 0)
                        {
                            this.preReadBuffer = null;
                        }
                    }
                }

                // TODO: Switch to nullable
                public override long ContentLength => _aspNetCoreHttpContext._aspNetContext.Request.ContentLength ?? -1;

                protected override string ContentTypeCore
                {
                    get
                    {
                        if (cachedContentType == null)
                        {
                            cachedContentType = _aspNetCoreHttpContext._aspNetContext.Request.ContentType;
                        }

                        return cachedContentType;
                    }
                }

                protected override bool HasContent => preReadBuffer != null || ContentLength > 0;

                protected override string SoapActionHeader => _aspNetCoreHttpContext._aspNetContext.Request.Headers["SOAPAction"];


                protected override ChannelBinding ChannelBinding
                {
                    get
                    {
                        throw new PlatformNotSupportedException("Shouldn't be able to request CBT"); // TODO: ChannelBindingToken
                        // return ChannelBindingUtility.GetToken(this.listenerHttpContext.listenerContext.Request.TransportContext);
                    }
                }

                protected override void AddProperties(Message message)
                {
                    var request = _aspNetCoreHttpContext._aspNetContext.Request;
                    HttpRequestMessageProperty requestProperty = new HttpRequestMessageProperty();
                    requestProperty.Method = request.Method;
                    foreach (var header in request.Headers)
                    {
                        requestProperty.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    }

                    // TODO: Uri.Query always includes the '?', check if the same is true for ASP.NET Core
                    if (request.QueryString.HasValue)
                    {
                        requestProperty.QueryString = request.QueryString.Value.Substring(1);
                    }

                    message.Properties.Add(HttpRequestMessageProperty.Name, requestProperty);
                    // TODO: Test the Via code
                    message.Properties.Via = new Uri(string.Concat(
                        request.Scheme,
                        "://",
                        request.Host.ToUriComponent(),
                        request.PathBase.ToUriComponent(),
                        request.Path.ToUriComponent(),
                        request.QueryString.ToUriComponent()));

                    var remoteIPAddress = request.HttpContext.Connection.RemoteIpAddress;
                    var remotePort = request.HttpContext.Connection.RemotePort;
                    RemoteEndpointMessageProperty remoteEndpointProperty = new RemoteEndpointMessageProperty(new IPEndPoint(remoteIPAddress, remotePort));
                    message.Properties.Add(RemoteEndpointMessageProperty.Name, remoteEndpointProperty);
                }

                protected override Stream GetInputStream()
                {
                    if (preReadBuffer != null)
                    {
                        return new AspNetCoreInputStream(_aspNetCoreHttpContext, preReadBuffer);
                    }
                    else
                    {
                        return new AspNetCoreInputStream(_aspNetCoreHttpContext);
                    }
                }

                class AspNetCoreInputStream : DetectEofStream
                {
                    public AspNetCoreInputStream(AspNetCoreHttpContext aspNetCoreHttpContext)
                        : base(aspNetCoreHttpContext._aspNetContext.Request.Body)
                    {
                    }

                    public AspNetCoreInputStream(AspNetCoreHttpContext aspNetCoreHttpContext, byte[] preReadBuffer)
                        : base(new PreReadStream(aspNetCoreHttpContext._aspNetContext.Request.Body, preReadBuffer))
                    {
                    }

                    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
                    {
                        try
                        {
                            return base.BeginRead(buffer, offset, count, callback, state);
                        }
                        catch (Exception exception)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                HttpChannelUtilities.CreateCommunicationException(exception));
                        }
                    }

                    public override int EndRead(IAsyncResult result)
                    {
                        try
                        {
                            return base.EndRead(result);
                        }
                        catch (Exception exception)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                HttpChannelUtilities.CreateCommunicationException(exception));
                        }
                    }

                    public override int Read(byte[] buffer, int offset, int count)
                    {
                        try
                        {
                            return base.Read(buffer, offset, count);
                        }
                        catch (Exception exception)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                HttpChannelUtilities.CreateCommunicationException(exception));
                        }
                    }

                    public override int ReadByte()
                    {
                        try
                        {
                            return base.ReadByte();
                        }
                        catch (Exception exception)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                HttpChannelUtilities.CreateCommunicationException(exception));
                        }
                    }
                }
            }
        }
    }
}
