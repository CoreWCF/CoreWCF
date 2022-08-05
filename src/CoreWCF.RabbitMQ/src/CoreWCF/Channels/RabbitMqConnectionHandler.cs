// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using CoreWCF.Configuration;
using CoreWCF.Queue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels
{
    internal class RabbitMqConnectionHandler : IQueueConnectionHandler
    {
        private readonly IServiceProvider _services;
        private static UriPrefixTable<TransportContext> s_addressTable;

        public RabbitMqConnectionHandler(IServiceBuilder serviceBuilder, IServiceProvider services)
        {
            _services = services;
            serviceBuilder.Opened += OnServiceBuilderOpened;
        }

        private void OnServiceBuilderOpened(object sender, EventArgs e)
        {
            // Trigger building all of the services to improve first request time and to catch any service config issues
            _services.GetRequiredService<UriPrefixTable<TransportContext>>();
        }

        internal static UriPrefixTable<TransportContext> BuildAddressTable(IServiceProvider services)
        {
            ILogger<RabbitMqConnectionHandler> logger = services.GetRequiredService<ILogger<RabbitMqConnectionHandler>>();
            IServiceBuilder serviceBuilder = services.GetRequiredService<IServiceBuilder>();
            IDispatcherBuilder dispatcherBuilder = services.GetRequiredService<IDispatcherBuilder>();
            IQueueMessagePipelineBuilder builder = services.GetRequiredService<IQueueMessagePipelineBuilder>();
            var addressTable = new UriPrefixTable<TransportContext>();
            foreach (Type serviceType in serviceBuilder.Services)
            {
                List<IServiceDispatcher> dispatchers = dispatcherBuilder.BuildDispatchers(serviceType);
                foreach (IServiceDispatcher dispatcher in dispatchers)
                {
                    if (dispatcher.BaseAddress == null)
                    {
                        // TODO: Should we throw? Ignore?
                        continue;
                    }

                    BindingElementCollection be = dispatcher.Binding.CreateBindingElements();
                    var encodingBindingElement = be.Find<MessageEncodingBindingElement>();

                    QueueMessageDispatch handshake = BuildHandshake(builder);


                    logger.LogDebug($"Registering URI {dispatcher.BaseAddress} with {nameof(RabbitMqConnectionHandler)}");
                    addressTable.RegisterUri(dispatcher.BaseAddress, HostNameComparisonMode.Exact,
                        new TransportContext(handshake, dispatcher,
                            encodingBindingElement.CreateMessageEncoderFactory().Encoder, dispatcher.Binding));
                }
            }

            s_addressTable = addressTable;
            return addressTable;
        }

        private static QueueMessageDispatch BuildHandshake(IQueueMessagePipelineBuilder pipelineBuilder)
        {
            pipelineBuilder.UseMiddleware<RabbitMqFetchMessageMiddleware>();
            pipelineBuilder.UseMiddleware<QueueProcessMessageMiddleware>();

            return pipelineBuilder.Build();
        }

        public QueueMessageContext GetContext(PipeReader reader, string queueUrl)
        {
            var transportContext = Lookup(queueUrl);
            var context = new QueueMessageContext
            {
                Reader = reader,
                LocalAddress = new EndpointAddress(queueUrl),
                QueueTransportContext = new QueueTransportContext
                {
                    MessageEncoder = transportContext.MessageEncoder,
                    QueueHandShakeDelegate = transportContext.QueueDispatch,
                    ServiceDispatcher = transportContext.ServiceDispatcher,
                    Binding = transportContext.Binding,
                },
                Properties = new Dictionary<string, object>(),
            };
            return context;
        }

        private TransportContext Lookup(string queueUrl)
        {
            var uri = new Uri(queueUrl);
            if (s_addressTable.TryLookupUri(uri, HostNameComparisonMode.Exact, out TransportContext dispatch))
                return dispatch;

            throw new Exception("Not found dispatch");
        }

        internal class TransportContext
        {
            public TransportContext(QueueMessageDispatch queueDispatch, IServiceDispatcher serviceDispatcher,
                MessageEncoder messageEncoder, Binding binding)
            {
                QueueDispatch = queueDispatch;
                ServiceDispatcher = serviceDispatcher;
                MessageEncoder = messageEncoder;
                Binding = binding;
            }

            public QueueMessageDispatch QueueDispatch { get; }
            public IServiceDispatcher ServiceDispatcher { get; }
            public MessageEncoder MessageEncoder { get; }
            public Binding Binding { get; }
        }
    }
}
