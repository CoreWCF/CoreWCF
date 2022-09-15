// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common.Configuration;
using CoreWCF.Queue.CoreWCF.Queue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CoreWCF.Queue.Common
{
    public class QueuePollingService : IHostedService
    {
        private readonly QueueHandShakeMiddleWare _queueHandShakeMiddleWare;
        private IServiceProvider _services;
        private IServiceScopeFactory _servicesScopeFactory;
        List<QueueTransportContext> _queueTransportContexts;
        private IOptions<QueueOptions> _options;
        private CancellationTokenSource _cancellationTokenSource;
        private IServiceBuilder _serviceBuilder;

        public QueuePollingService(IServiceProvider services,
            IServiceScopeFactory servicesScopeFactory, QueueHandShakeMiddleWare queueHandShakeMiddle, IOptions<QueueOptions> queueOPtions)
        {
            _services = services;
            _servicesScopeFactory = servicesScopeFactory;
            _queueHandShakeMiddleWare = queueHandShakeMiddle;
            _options = queueOPtions;
            _serviceBuilder = _services.GetRequiredService<IServiceBuilder>();
            _cancellationTokenSource = new CancellationTokenSource();
            _queueTransportContexts = new List<QueueTransportContext>();
            _serviceBuilder.Opened += _serviceBuilder_Opened;
            
        }

        private void _serviceBuilder_Opened(object sender, EventArgs e)
        {
            _= Init();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
           //await _serviceBuilder.OpenAsync(cancellationToken);
           // await Init();
            var tasks =  _queueTransportContexts.Select(_queueTransport => StartFetchingMessage(_queueTransport));
            await Task.WhenAll(tasks); // handle cancellation

        }

        private async Task Init()
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
                    QueueBaseTransportBindingElement queueTransportBinding = be.Find<QueueBaseTransportBindingElement>();
                    var msgEncBindingelement = be.Find<MessageEncodingBindingElement>();

                    if (queueTransportBinding == null)
                    {
                        continue;
                    }

                    IServiceDispatcher _serviceDispatcher = null;
                    var _customBinding = new CustomBinding(dispatcher.Binding);
                    var parameters = new BindingParameterCollection();
                    //
                    parameters.Add(optnVal);
                    parameters.Add(_services);
                    // add service cred
                    if (_customBinding.CanBuildServiceDispatcher<IInputChannel>(parameters))
                    {
                        _serviceDispatcher = _customBinding.BuildServiceDispatcher<IInputChannel>(parameters, dispatcher);
                    }
                    parameters.Add(_serviceDispatcher);
                    BindingContext bindingContext = new BindingContext(_customBinding, parameters);
                    QueueTransportPump queuePump = queueTransportBinding.BuildQueueTransportPump(bindingContext);

                    _queueTransportContexts.Add(new QueueTransportContext
                    {
                        QueuePump = queuePump,
                        ServiceDispatcher = _serviceDispatcher,
                        QueueBindingElement = queueTransportBinding,
                        MessageEncoderFactory = msgEncBindingelement.CreateMessageEncoderFactory(),
                        QueueHandShakeDelegate = _queueHandShakeMiddleWare.Build()

                    });

                }
            }

        }

        private async Task StartFetchingMessage(QueueTransportContext queueTransport)
        {
            await queueTransport.QueuePump.StartPumpAsync(queueTransport, CancellationToken.None);

            /*
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < queueTransport.QueueBindingElement.MaxPendingReceives; i++)
            {
                tasks.Add(FetchAndProcessAsync(queueTransport, _cancellationTokenSource.Token));

            }
            await Task.WhenAll(tasks);*/
        }



        public Task StopAsync(CancellationToken cancellationToken)
        {
            var tasks = _queueTransportContexts.Select(_queueTransport => _queueTransport.QueuePump.StopPumpAsync(cancellationToken));
            return Task.WhenAll(tasks);
        }
    }
}
