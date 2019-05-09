using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Runtime;
using Microsoft.ServiceModel.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Microsoft.ServiceModel.Channels
{
    internal class RequestDelegateHandler
    {
        internal const long DefaultMaxBufferPoolSize = 512 * 1024;
        private IServiceDispatcher _dispatcher;
        private IDefaultCommunicationTimeouts _timeouts;
        private IServiceScopeFactory _servicesScopeFactory;
        private HttpTransportSettings _httpSettings;
        private IReplyChannel _replyChannel;

        public RequestDelegateHandler(IServiceDispatcher dispatcher, IServiceScopeFactory servicesScopeFactory)
        {
            _dispatcher = dispatcher;
            _timeouts = _dispatcher.Binding;
            _servicesScopeFactory = servicesScopeFactory;
            BuildHandler();
        }

        private void BuildHandler()
        {
            var be = _dispatcher.Binding.CreateBindingElements();
            var mebe = be.Find<MessageEncodingBindingElement>();
            if (mebe == null)
            {
                throw new ArgumentException("Must provide a MessageEncodingBindingElement", nameof(_dispatcher.Binding));
            }

            var tbe = be.Find<HttpTransportBindingElement>();
            if (tbe == null)
            {
                throw new ArgumentException("Must provide a HttpTransportBindingElement", nameof(_dispatcher.Binding));
            }

            var httpSettings = new HttpTransportSettings();
            httpSettings.BufferManager = BufferManager.CreateBufferManager(DefaultMaxBufferPoolSize, tbe.MaxBufferSize);
            httpSettings.OpenTimeout = _dispatcher.Binding.OpenTimeout;
            httpSettings.ReceiveTimeout = _dispatcher.Binding.ReceiveTimeout;
            httpSettings.SendTimeout = _dispatcher.Binding.SendTimeout;
            httpSettings.CloseTimeout = _dispatcher.Binding.CloseTimeout;
            httpSettings.MaxBufferSize = tbe.MaxBufferSize;
            httpSettings.MaxReceivedMessageSize = tbe.MaxReceivedMessageSize;
            httpSettings.MessageEncoderFactory = mebe.CreateMessageEncoderFactory();
            httpSettings.ManualAddressing = tbe.ManualAddressing;
            httpSettings.TransferMode = tbe.TransferMode;
            httpSettings.KeepAliveEnabled = tbe.KeepAliveEnabled;
            httpSettings.AnonymousUriPrefixMatcher = new HttpAnonymousUriPrefixMatcher();
            _httpSettings = httpSettings;
            var scope = _servicesScopeFactory.CreateScope();
            _replyChannel = new AspNetCoreReplyChannel(_servicesScopeFactory.CreateScope().ServiceProvider);
        }

        internal Task HandleRequest(HttpContext context)
        {
            var requestContext = HttpRequestContext.CreateContext(_httpSettings, context);
            var httpInput = requestContext.GetHttpInput(true);
            Exception requestException;
            Message requestMessage = httpInput.ParseIncomingMessage(out requestException);
            if ((requestMessage == null) && (requestException == null))
            {
                throw Fx.Exception.AsError(
                        new ProtocolException(
                            SR.MessageXmlProtocolError,
                            new XmlException(SR.MessageIsEmpty)));
            }
            requestContext.SetMessage(requestMessage, requestException);
            return _dispatcher.DispatchAsync(requestContext, _replyChannel, context.RequestAborted);
        }
    }
}