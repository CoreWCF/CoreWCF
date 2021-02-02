// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CoreWCF.Configuration
{
    public class FramingConnectionHandshakeBuilder : IFramingConnectionHandshakeBuilder
    {
        private readonly IList<Func<HandshakeDelegate, HandshakeDelegate>> _components = new List<Func<HandshakeDelegate, HandshakeDelegate>>();

        public FramingConnectionHandshakeBuilder(IServiceProvider serviceProvider)
        {
            Properties = new Dictionary<string, object>(StringComparer.Ordinal);
            HandshakeServices = serviceProvider;
        }

        public FramingConnectionHandshakeBuilder(FramingConnectionHandshakeBuilder connectionHandshakeBuilder)
        {
            Properties = new Dictionary<string, object>(connectionHandshakeBuilder.Properties, StringComparer.Ordinal);
        }

        public IServiceProvider HandshakeServices
        {
            get
            {
                return GetProperty<IServiceProvider>("handshake.Services");
            }
            set
            {
                SetProperty("handshake.Services", value);
            }
        }

        public IDictionary<string, object> Properties { get; }

        private T GetProperty<T>(string key)
        {
            return Properties.TryGetValue(key, out object value) ? (T)value : default;
        }

        private void SetProperty<T>(string key, T value)
        {
            Properties[key] = value;
        }

        public IFramingConnectionHandshakeBuilder Use(Func<HandshakeDelegate, HandshakeDelegate> middleware)
        {
            _components.Add(middleware);
            return this;
        }

        public IFramingConnectionHandshakeBuilder New()
        {
            return new FramingConnectionHandshakeBuilder(this);
        }

        public HandshakeDelegate Build()
        {
            HandshakeDelegate app = context =>
            {
                return Task.CompletedTask;
            };

            foreach (Func<HandshakeDelegate, HandshakeDelegate> component in _components.Reverse())
            {
                app = component(app);
            }

            return app;
        }
    }
}
