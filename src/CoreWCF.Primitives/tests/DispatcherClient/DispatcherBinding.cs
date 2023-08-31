﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;
using CoreWCF;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherClient
{
    internal class DispatcherBinding<TService, TContract> : Binding where TService : class
    {
        private readonly Action<IServiceCollection> _configureServices;
        private readonly Action<ServiceHostBase> _configureServiceHostBase;
        private readonly MessageVersion _messageVersion;

        internal DispatcherBinding(Action<IServiceCollection> configureServices, Action<CoreWCF.ServiceHostBase> configureServiceHostBase = default, MessageVersion messageVersion = null)
        {
            _configureServices = configureServices;
            _configureServiceHostBase = configureServiceHostBase;
            if (messageVersion == default)
            {
                messageVersion = MessageVersion.Default;
            }
            _messageVersion = messageVersion;
        }

        public override string Scheme => "corewcf";

        public override BindingElementCollection CreateBindingElements()
        {
            var messageEncoder = new TextMessageEncodingBindingElement() { MessageVersion = _messageVersion };
            var transportBindingElement = new DispatcherTransportBindingElement<TService, TContract>(_configureServices, _configureServiceHostBase);
            var elements = new BindingElementCollection(new BindingElement[] { messageEncoder, transportBindingElement });
            return elements;
        }
    }
}
