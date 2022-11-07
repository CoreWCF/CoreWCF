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
using Microsoft.Extensions.Options;

namespace CoreWCF.Queue.Common
{
    internal class QueuePollingService : IHostedService
    {
        private readonly QueueMiddleware _queueMiddleware;
        private readonly IServiceProvider _services;
        private readonly List<QueueTransportContext> _queueTransportContexts;
        private readonly IOptions<QueueOptions> _options;
        private readonly IServiceBuilder _serviceBuilder;
        private readonly TaskCompletionSource<bool> _tcs;

        public QueuePollingService(IServiceProvider services, QueueMiddleware queueMiddleware,
            IOptions<QueueOptions> queueOptions)
        {
            _services = services;
            _queueMiddleware = queueMiddleware;
            _options = queueOptions;
            _serviceBuilder = _services.GetRequiredService<IServiceBuilder>();
            _queueTransportContexts = new List<QueueTransportContext>();
            _tcs = new TaskCompletionSource<bool>();
            _serviceBuilder.Opened += (_, _) => _tcs.SetResult(true);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Task.Run(() => WaitAndStart(cancellationToken), cancellationToken);
            return Task.CompletedTask;
        }

        private async Task WaitAndStart(CancellationToken cancellationToken)
        {
            if(_serviceBuilder.State != CommunicationState.Opened)
                await _tcs.Task;

            await Init();
            var tasks = _queueTransportContexts.Select(x => StartFetchingMessage(x, cancellationToken));
            await Task.WhenAll(tasks); // handle cancellation
        }

        private Task Init()
        {
            IDispatcherBuilder dispatcherBuilder = _services.GetRequiredService<IDispatcherBuilder>();
            var optnVal = _options.Value ?? new QueueOptions();
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
                    QueueBaseTransportBindingElement
                        queueTransportBinding = be.Find<QueueBaseTransportBindingElement>();
                    var msgEncBindingElement = be.Find<MessageEncodingBindingElement>();

                    if (queueTransportBinding == null)
                    {
                        continue;
                    }

                    IServiceDispatcher serviceDispatcher = null;
                    var customBinding = new CustomBinding(dispatcher.Binding);
                    var parameters = new BindingParameterCollection { optnVal, _services, };
                    // add service cred
                    if (customBinding.CanBuildServiceDispatcher<IInputChannel>(parameters))
                    {
                        serviceDispatcher =
                            customBinding.BuildServiceDispatcher<IInputChannel>(parameters, dispatcher);
                    }

                    parameters.Add(serviceDispatcher);
                    BindingContext bindingContext = new BindingContext(customBinding, parameters);
                    QueueTransportPump queuePump = queueTransportBinding.BuildQueueTransportPump(bindingContext);

                    _queueTransportContexts.Add(new QueueTransportContext(serviceDispatcher,
                        msgEncBindingElement.CreateMessageEncoderFactory(), queueTransportBinding,
                        _queueMiddleware.Build(), queuePump));
                }
            }

            return Task.CompletedTask;
        }

        private static async Task StartFetchingMessage(QueueTransportContext queueTransport, CancellationToken token)
        {
            await queueTransport.QueuePump.StartPumpAsync(queueTransport, token);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            var tasks = _queueTransportContexts.Select(queueTransport =>
                queueTransport.QueuePump.StopPumpAsync(cancellationToken));
            return Task.WhenAll(tasks);
        }
    }
}
