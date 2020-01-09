using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.Text;

namespace DispatcherClient
{
    internal class DispatcherBinding<TService, TContract> : Binding where TService : class
    {
        private Action<IServiceCollection> _configureServices;

        internal DispatcherBinding(Action<IServiceCollection> configureServices)
        {
            _configureServices = configureServices;
        }

        public override string Scheme => "corewcf";

        public override BindingElementCollection CreateBindingElements()
        {
            var messageEncoder = new TextMessageEncodingBindingElement();
            var transportBindingElement = new DispatcherTransportBindingElement<TService, TContract>(_configureServices);
            var elements = new BindingElementCollection(new BindingElement[] { messageEncoder, transportBindingElement });
            return elements;
        }
    }
}
