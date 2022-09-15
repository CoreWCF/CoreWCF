using System;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Queue.Common
{
    public class QueueHandShakeMiddleWare
    {
        private readonly IQueueMiddlewareBuilder _queueMiddlewareBuilder;
        private readonly IServiceProvider _services;
        private readonly IServiceScopeFactory _servicesScopeFactory;

        public QueueHandShakeMiddleWare(
            IServiceProvider services,
            IServiceScopeFactory servicesScopeFactory, IQueueMiddlewareBuilder queueBuilder)
        {
            _services = services;
            _servicesScopeFactory = servicesScopeFactory;
            _queueMiddlewareBuilder = InitQueueMiddleWare(queueBuilder);
        }

        private IQueueMiddlewareBuilder InitQueueMiddleWare(IQueueMiddlewareBuilder queueBuilder)
        {
            queueBuilder.UseMiddleware<QueueFetchMessage>();
            queueBuilder.UseMiddleware<QueueProcessMessage>();
            return queueBuilder;
        }

        public QueueMessageDispatcherDelegate Build()
        {
            return _queueMiddlewareBuilder.Build();
        }
    }
}

