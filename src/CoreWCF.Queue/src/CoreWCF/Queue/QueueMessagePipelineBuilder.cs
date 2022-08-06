// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CoreWCF.Queue
{
    public class QueueMessagePipelineBuilder : IQueueMessagePipelineBuilder
    {
        private readonly IList<Func<QueueMessageDispatch, QueueMessageDispatch>> _components = new List<Func<QueueMessageDispatch,QueueMessageDispatch>>();

        public QueueMessagePipelineBuilder(IServiceProvider serviceProvider)
        {
            Properties = new Dictionary<string, object>(StringComparer.Ordinal);
            Services = serviceProvider;
        }

        private QueueMessagePipelineBuilder(QueueMessagePipelineBuilder builder)
        {
            Properties = new Dictionary<string, object>(builder.Properties, StringComparer.Ordinal);
        }

        public IServiceProvider Services
        {
            get
            {
                return GetProperty<IServiceProvider>("QueueMessage.Services");
            }
            set
            {
                SetProperty("QueueMessage.Services", value);
            }
        }

        public IDictionary<string, object> Properties { get; }

        public IQueueMessagePipelineBuilder Use(Func<QueueMessageDispatch, QueueMessageDispatch> middleware)
        {
            _components.Add(middleware);
            return this;
        }

        public IQueueMessagePipelineBuilder New()
        {
            return new QueueMessagePipelineBuilder(this);
        }

        public QueueMessageDispatch Build()
        {
            QueueMessageDispatch app = context => Task.CompletedTask;

            foreach (Func<QueueMessageDispatch, QueueMessageDispatch> component in _components.Reverse())
            {
                app = component(app);
            }

            return app;
        }

        private T GetProperty<T>(string key)
        {
            return Properties.TryGetValue(key, out object value) ? (T)value : default;
        }

        private void SetProperty<T>(string key, T value)
        {
            Properties[key] = value;
        }
    }
}
