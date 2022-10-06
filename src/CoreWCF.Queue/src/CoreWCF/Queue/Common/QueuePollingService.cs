// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CoreWCF.Queue.Common
{
    internal class QueuePollingService : IHostedService
    {
        private readonly QueueMiddleware _queueMiddleware;
        private readonly IServiceProvider _services;
        private readonly List<QueueTransportContext> _queueTransportContexts;
        private readonly IServiceBuilder _serviceBuilder;

        public QueuePollingService(IServiceProvider services, QueueMiddleware queueMiddleware)
        {
            _services = services;
            _queueMiddleware = queueMiddleware;
            _serviceBuilder = _services.GetRequiredService<IServiceBuilder>();
            _serviceBuilder.Opened += _serviceBuilder_Opened;
            _queueTransportContexts = new List<QueueTransportContext>();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var tasks = _queueTransportContexts.Select(queueTransport => StartFetchingMessage(queueTransport, cancellationToken));
            await Task.WhenAll(tasks);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            var tasks = _queueTransportContexts.Select(queueTransport =>
                queueTransport.QueuePump.StopPumpAsync(cancellationToken));
            return Task.WhenAll(tasks);
        }

        private void _serviceBuilder_Opened(object sender, EventArgs e)
        {
            Init();
        }

        private void Init()
        {
            IDispatcherBuilder dispatcherBuilder = _services.GetRequiredService<IDispatcherBuilder>();
            foreach (Type serviceType in _serviceBuilder.Services)
            {
                List<IServiceDispatcher> dispatchers = dispatcherBuilder.BuildDispatchers(serviceType);
                foreach (IServiceDispatcher dispatcher in dispatchers)
                {
                    if (dispatcher.BaseAddress == null)
                    {
                        continue;
                    }

                    BindingElementCollection be = dispatcher.Binding.CreateBindingElements();
                    QueueBaseTransportBindingElement queueTransportBinding = be.Find<QueueBaseTransportBindingElement>();

                    var msgEncBindingElement = be.Find<MessageEncodingBindingElement>();

                    if (queueTransportBinding == null)
                    {
                        continue;
                    }

                    IServiceDispatcher serviceDispatcher = null;
                    var customBinding = new CustomBinding(dispatcher.Binding);
                    var parameters = new BindingParameterCollection();

                    parameters.Add(_services);
                    if (customBinding.CanBuildServiceDispatcher<IInputChannel>(parameters))
                    {
                        serviceDispatcher = customBinding.BuildServiceDispatcher<IInputChannel>(parameters, dispatcher);
                    }
                    parameters.Add(serviceDispatcher);
                    BindingContext bindingContext = new BindingContext(customBinding, parameters);
                    QueueTransportPump queuePump = queueTransportBinding.BuildQueueTransportPump(bindingContext);

                    _queueTransportContexts.Add(new QueueTransportContext
                    {
                        QueuePump = queuePump,
                        ServiceDispatcher = serviceDispatcher,
                        QueueBindingElement = queueTransportBinding,
                        MessageEncoderFactory = msgEncBindingElement.CreateMessageEncoderFactory(),
                        QueueMessageDispatcher = _queueMiddleware.Build()
                    });
                }
            }
        }

        private static async Task StartFetchingMessage(QueueTransportContext queueTransport, CancellationToken token)
        {
            await queueTransport.QueuePump.StartPumpAsync(queueTransport, token);
        }
    }
}
