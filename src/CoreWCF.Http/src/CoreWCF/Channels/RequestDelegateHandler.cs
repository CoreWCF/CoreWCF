using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using CoreWCF.Runtime;
using CoreWCF.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CoreWCF.Channels
{
    internal class RequestDelegateHandler
    {
        internal const long DefaultMaxBufferPoolSize = 512 * 1024;
        private IServiceDispatcher _serviceDispatcher;
        private IDefaultCommunicationTimeouts _timeouts;
        private IServiceScopeFactory _servicesScopeFactory;
        private HttpTransportSettings _httpSettings;
        private IReplyChannel _replyChannel;
        private IServiceChannelDispatcher _channelDispatcher;

        public RequestDelegateHandler(IServiceDispatcher serviceDispatcher, IServiceScopeFactory servicesScopeFactory)
        {
            _serviceDispatcher = serviceDispatcher;
            _timeouts = _serviceDispatcher.Binding;
            _servicesScopeFactory = servicesScopeFactory;
            BuildHandler();
        }

        private void BuildHandler()
        {
            var be = _serviceDispatcher.Binding.CreateBindingElements();
            var mebe = be.Find<MessageEncodingBindingElement>();
            if (mebe == null)
            {
                throw new ArgumentException("Must provide a MessageEncodingBindingElement", nameof(_serviceDispatcher.Binding));
            }

            var tbe = be.Find<HttpTransportBindingElement>();
            if (tbe == null)
            {
                throw new ArgumentException("Must provide a HttpTransportBindingElement", nameof(_serviceDispatcher.Binding));
            }

            var httpSettings = new HttpTransportSettings();
            httpSettings.BufferManager = BufferManager.CreateBufferManager(DefaultMaxBufferPoolSize, tbe.MaxBufferSize);
            httpSettings.OpenTimeout = _serviceDispatcher.Binding.OpenTimeout;
            httpSettings.ReceiveTimeout = _serviceDispatcher.Binding.ReceiveTimeout;
            httpSettings.SendTimeout = _serviceDispatcher.Binding.SendTimeout;
            httpSettings.CloseTimeout = _serviceDispatcher.Binding.CloseTimeout;
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
            _channelDispatcher = _serviceDispatcher.CreateServiceChannelDispatcher(_replyChannel);
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
            return _channelDispatcher.DispatchAsync(requestContext, context.RequestAborted);
        }
    }
}