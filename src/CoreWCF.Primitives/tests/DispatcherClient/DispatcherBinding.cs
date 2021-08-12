// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.Text;
using CoreWCF;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherClient
{
    internal class DispatcherBinding<TService, TContract> : Binding where TService : class
    {
        private readonly Action<IServiceCollection> _configureServices;
        private readonly Action<ServiceHostBase> _configureServiceHostBase;

        internal DispatcherBinding(Action<IServiceCollection> configureServices, Action<CoreWCF.ServiceHostBase> configureServiceHostBase = default)
        {
            _configureServices = configureServices;
            _configureServiceHostBase = configureServiceHostBase;
        }

        public override string Scheme => "corewcf";

        public override BindingElementCollection CreateBindingElements()
        {
            var messageEncoder = new TextMessageEncodingBindingElement();
            var transportBindingElement = new DispatcherTransportBindingElement<TService, TContract>(_configureServices, _configureServiceHostBase);
            var elements = new BindingElementCollection(new BindingElement[] { messageEncoder, transportBindingElement });
            return elements;
        }
    }
}
