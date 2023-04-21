// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CoreWCF.Queue.Common.Configuration
{
    internal class QueueMiddlewareBuilder : IQueueMiddlewareBuilder
    {
        private readonly IList<Func<QueueMessageDispatcherDelegate, QueueMessageDispatcherDelegate>> _components = new List<Func<QueueMessageDispatcherDelegate,QueueMessageDispatcherDelegate>>();

        public QueueMiddlewareBuilder(IServiceProvider serviceProvider)
        {
            Properties = new Dictionary<string, object>(StringComparer.Ordinal);
            Services = serviceProvider;
        }

        private QueueMiddlewareBuilder(QueueMiddlewareBuilder builder)
        {
            Properties = new Dictionary<string, object>(builder.Properties, StringComparer.Ordinal);
        }

        public IServiceProvider Services
        {
            get
            {
                return GetProperty("QueueMessage.Services") as IServiceProvider;
            }
            set
            {
                SetProperty("QueueMessage.Services", value);
            }
        }

        public IDictionary<string, object> Properties { get; }

        public IQueueMiddlewareBuilder Use(Func<QueueMessageDispatcherDelegate, QueueMessageDispatcherDelegate> middleware)
        {
            _components.Add(middleware);
            return this;
        }

        public IQueueMiddlewareBuilder New()
        {
            return new QueueMiddlewareBuilder(this);
        }

        public QueueMessageDispatcherDelegate Build()
        {
            QueueMessageDispatcherDelegate app = _ => Task.CompletedTask;

            foreach (Func<QueueMessageDispatcherDelegate, QueueMessageDispatcherDelegate> component in _components.Reverse())
            {
                app = component(app);
            }

            return app;
        }

        private object GetProperty(string key)
        {
            return Properties.TryGetValue(key, out object value) ? value : default;
        }

        private void SetProperty(string key, object value)
        {
            Properties[key] = value;
        }
    }
}
