// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security;
using Microsoft.AspNetCore.Http;

namespace CoreWCF.Channels
{
    internal abstract class HttpRequestContext : RequestContextBase
    {
        private HttpOutput _httpOutput;
        private bool _errorGettingHttpInput;
        private SecurityMessageProperty _securityProperty;
        private readonly TaskCompletionSource<object> _replySentTcs;
        //EventTraceActivity eventTraceActivity;
        //ServerWebSocketTransportDuplexSessionChannel webSocketChannel;

        protected HttpRequestContext(IHttpTransportFactorySettings settings, Message requestMessage)
            : base(requestMessage, settings.CloseTimeout, settings.SendTimeout)
        {
            HttpTransportSettings = settings;
            _replySentTcs = new TaskCompletionSource<object>(TaskContinuationOptions.RunContinuationsAsynchronously);
        }

        public bool KeepAliveEnabled
        {
            get
            {
                return HttpTransportSettings.KeepAliveEnabled;
            }
        }

        public abstract string HttpMethod { get; }

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
            if (throwOnError || !_errorGettingHttpInput)
            {
                try
                {
                    httpInput = GetHttpInput();
                    _errorGettingHttpInput = false;
                }
                catch (Exception e)
                {
                    _errorGettingHttpInput = true;
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

        protected abstract Task<SecurityMessageProperty> OnProcessAuthenticationAsync();

        public abstract HttpOutput GetHttpOutput(Message message);

        protected abstract HttpInput GetHttpInput();

        public HttpOutput GetHttpOutputCore(Message message)
        {
            if (_httpOutput != null)
            {
                return _httpOutput;
            }

            return GetHttpOutput(message);
        }

        protected override void OnAbort()
        {
            if (_httpOutput != null)
            {
                _httpOutput.Abort(HttpAbortReason.Aborted);
            }

            Cleanup();
            _replySentTcs.TrySetResult(null);
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            try
            {
                if (_httpOutput != null)
                {
                    await _httpOutput.CloseAsync(); ;
                }
            }
            finally
            {
                Cleanup();
                _replySentTcs.TrySetResult(null);
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

            TraceHttpMessageReceived(message);

            if (requestException != null)
            {
                SetRequestMessage(requestException);
                message.Close();
            }
            else
            {
                message.Properties.Security = (_securityProperty != null) ? (SecurityMessageProperty)_securityProperty.CreateCopy() : null;
                SetRequestMessage(message);
            }
        }

        private void TraceHttpMessageReceived(Message message)
        {
        }

        protected abstract HttpStatusCode ValidateAuthentication();

        private bool PrepareReply(ref Message message)
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
            _httpOutput = GetHttpOutputCore(message);

            return closeOnReceivedEof;
        }

        public override void OnOperationInvoke()
        {
            base.OnOperationInvoke();
            _replySentTcs.TrySetResult(null);
        }

        protected override async Task OnReplyAsync(Message message, CancellationToken token)
        {
            Message responseMessage = message;

            try
            {
                bool closeOutputAfterReply = PrepareReply(ref responseMessage);
                await _httpOutput.SendAsync(token);

                if (closeOutputAfterReply)
                {
                    await _httpOutput.CloseAsync();
                }
            }
            finally
            {
                if (message != null && !ReferenceEquals(message, responseMessage))
                {
                    responseMessage.Close();
                }
            }
        }

        public Task ReplySent => _replySentTcs.Task;

        public async Task<bool> ProcessAuthenticationAsync()
        {
            HttpStatusCode statusCode = ValidateAuthentication();

            if (statusCode == HttpStatusCode.OK)
            {
                bool authenticationSucceeded = false;
                statusCode = HttpStatusCode.Forbidden;
                try
                {
                    _securityProperty = await OnProcessAuthenticationAsync();
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

        private Message CreateAckMessage(HttpStatusCode statusCode, string statusDescription)
        {
            Message ackMessage = new NullMessage();
            HttpResponseMessageProperty httpResponseProperty = new HttpResponseMessageProperty
            {
                StatusCode = statusCode,
                SuppressEntityBody = true
            };
            if (statusDescription.Length > 0)
            {
                httpResponseProperty.StatusDescription = statusDescription;
            }

            ackMessage.Properties.Add(HttpResponseMessageProperty.Name, httpResponseProperty);

            return ackMessage;
        }

        private class AspNetCoreHttpContext : HttpRequestContext
        {
            private const string Http11ProtocolString = "HTTP/1.1";
            private readonly HttpContext _aspNetContext;
            // byte[] webSocketInternalBuffer;

            public AspNetCoreHttpContext(IHttpTransportFactorySettings settings, HttpContext aspNetContext)
                : base(settings, null)
            {
                _aspNetContext = aspNetContext;
            }

            public override string HttpMethod => _aspNetContext.Request.Method;

            protected override HttpInput GetHttpInput()
            {
                return new AspNetCoreHttpInput(this);
            }

            public override HttpOutput GetHttpOutput(Message message)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(Http11ProtocolString, _aspNetContext.Request.Protocol))
                {
                    if (HttpTransportSettings.KeepAliveEnabled)
                    {
                        _aspNetContext.Response.Headers["Connection"] = "keep-alive";
                    }
                    else
                    {
                        _aspNetContext.Response.Headers["Connection"] = "close";
                    }
                }

                if (HttpTransportSettings.MessageEncoderFactory.Encoder is ICompressedMessageEncoder compressedMessageEncoder && compressedMessageEncoder.CompressionEnabled)
                {
                    string acceptEncoding = _aspNetContext.Request.Headers[HttpChannelUtilities.AcceptEncodingHeader];
                    compressedMessageEncoder.AddCompressedMessageProperties(message, acceptEncoding);
                }

                return HttpOutput.CreateHttpOutput(_aspNetContext, HttpTransportSettings, message, HttpMethod);
            }

            protected override async Task<SecurityMessageProperty> OnProcessAuthenticationAsync()
            {
                if (HttpTransportSettings.IsAuthenticationRequired)
                {
                    ServiceSecurityContext securityContext = await CreateSecurityContextAsync(_aspNetContext.User);
                    SecurityMessageProperty securityMessageProperty = new()
                    {
                        ServiceSecurityContext = securityContext
                    };
                    return securityMessageProperty;
                }

                return null;
            }

            protected override HttpStatusCode ValidateAuthentication()
            {
                if (HttpTransportSettings.IsAuthenticationRequired)
                {
                    return _aspNetContext.User.Identity.IsAuthenticated
                        ? HttpStatusCode.OK
                        : HttpStatusCode.Unauthorized;
                }

                return HttpStatusCode.OK;
                //return Listener.ValidateAuthentication(listenerContext);
            }

            protected override void OnAbort()
            {
                _aspNetContext.Abort();
                Cleanup();
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

            private async Task<ServiceSecurityContext> CreateSecurityContextAsync(IPrincipal principal)
            {
                if (principal.Identity is WindowsIdentity wid)
                {
                    WindowsSecurityTokenAuthenticator tokenAuthenticator = new WindowsSecurityTokenAuthenticator();
                    SecurityToken windowsToken = new WindowsSecurityToken(wid);
                    ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies = await tokenAuthenticator.ValidateTokenAsync(windowsToken);
                    return new ServiceSecurityContext(authorizationPolicies);
                }
                else if (principal.Identity is GenericIdentity gid)
                {
                    WindowsSecurityTokenAuthenticator tokenAuthenticator = new WindowsSecurityTokenAuthenticator();
                    SecurityToken genericToken = new GenericIdentitySecurityToken(gid, SecurityUniqueId.Create().Value);
                    ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies = await tokenAuthenticator.ValidateTokenAsync(genericToken);
                    return new ServiceSecurityContext(authorizationPolicies);
                }
                else if (principal.Identity is ClaimsIdentity)
                {
                    AuthorizationContext authorizationContext = AuthorizationContext.CreateDefaultAuthorizationContext(null);
                    authorizationContext.Properties.Add(nameof(ClaimsPrincipal), principal);
                    return new ServiceSecurityContext(authorizationContext);
                }

                return null;
            }

            private class AspNetCoreHttpInput : HttpInput
            {
                private readonly AspNetCoreHttpContext _aspNetCoreHttpContext;
                private string _cachedContentType; // accessing the header in System.Net involves a native transition
                private byte[] _preReadBuffer;

                // TODO: ChannelBindingSupport
                public AspNetCoreHttpInput(AspNetCoreHttpContext aspNetCoreHttpContext)
                    : base(aspNetCoreHttpContext.HttpTransportSettings, true, false /* ChannelBindingSupportEnabled */)
                {
                    _aspNetCoreHttpContext = aspNetCoreHttpContext;
                }

                protected override async Task CheckForContentAsync()
                {
                    if (!_aspNetCoreHttpContext._aspNetContext.Request.ContentLength.HasValue)
                    {
                        _preReadBuffer = new byte[1];
                        // TODO: Look into useing PipeReader with look-ahead
                        if (await _aspNetCoreHttpContext._aspNetContext.Request.Body.ReadAsync(_preReadBuffer, 0, 1) == 0)
                        {
                            _preReadBuffer = null;
                        }
                    }
                }

                // TODO: Switch to nullable
                public override long ContentLength => _aspNetCoreHttpContext._aspNetContext.Request.ContentLength ?? -1;

                protected override string ContentTypeCore
                {
                    get
                    {
                        if (_cachedContentType == null)
                        {
                            _cachedContentType = _aspNetCoreHttpContext._aspNetContext.Request.ContentType;
                        }

                        return _cachedContentType;
                    }
                }

                protected override bool HasContent => _preReadBuffer != null || ContentLength > 0;

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
                    HttpRequest request = _aspNetCoreHttpContext._aspNetContext.Request;
                    var requestProperty = new HttpRequestMessageProperty(_aspNetCoreHttpContext._aspNetContext);
                    message.Properties.Add(HttpRequestMessageProperty.Name, requestProperty);
                    String hostAddress = String.Concat(request.IsHttps ? "https://" : "http://", request.Host.HasValue ? request.Host.Value : "localhost");
                    // TODO: Test the Via code
                    message.Properties.Via = new Uri(string.Concat(
                        hostAddress,
                        request.PathBase.ToUriComponent(),
                        request.Path.ToUriComponent(),
                        request.QueryString.ToUriComponent()));

                    IPAddress remoteIPAddress = request.HttpContext.Connection.RemoteIpAddress;
                    int remotePort = request.HttpContext.Connection.RemotePort;

                    if (remoteIPAddress != null)
                    {
                        RemoteEndpointMessageProperty remoteEndpointProperty = new RemoteEndpointMessageProperty(new IPEndPoint(remoteIPAddress, remotePort));
                        message.Properties.Add(RemoteEndpointMessageProperty.Name, remoteEndpointProperty);
                    }
                }

                protected override Stream GetInputStream()
                {
                    if (_preReadBuffer != null)
                    {
                        return new AspNetCoreInputStream(_aspNetCoreHttpContext, _preReadBuffer);
                    }
                    else
                    {
                        return new AspNetCoreInputStream(_aspNetCoreHttpContext);
                    }
                }

                private class AspNetCoreInputStream : DetectEofStream
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
