// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using Microsoft.AspNetCore.Connections;

namespace CoreWCF.Channels
{
    public abstract class NetFramingListenOptions : IConnectionBuilder
    {
        private readonly List<Func<ConnectionDelegate, ConnectionDelegate>> _middleware = new List<Func<ConnectionDelegate, ConnectionDelegate>>();

        private int _connectionBufferSize;
        private ConnectionPoolSettings _connectionPoolSettings;
        private HostNameComparisonMode _hostNameComparisonMode;
        private IConnectionReuseHandler _connectionReuseHandler;

        protected NetFramingListenOptions()
        {
            _connectionBufferSize = ConnectionOrientedTransportDefaults.ConnectionBufferSize;
            _connectionPoolSettings = new ConnectionPoolSettings();
            _hostNameComparisonMode = ConnectionOrientedTransportDefaults.HostNameComparisonMode;
        }

        public Uri BaseAddress { get; protected set; }

        public int ConnectionBufferSize
        {
            get => _connectionBufferSize;
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SRCommon.ValueMustBeNonNegative));
                }

                _connectionBufferSize = value;
            }
        }

        public ConnectionPoolSettings ConnectionPoolSettings
        {
            get => _connectionPoolSettings;
            protected set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                _connectionPoolSettings = value;
            }
        }

        public HostNameComparisonMode HostNameComparisonMode
        {
            get => _hostNameComparisonMode;
            set
            {
                HostNameComparisonModeHelper.Validate(value);
                _hostNameComparisonMode = value;
            }
        }

        internal IConnectionReuseHandler ConnectionReuseHandler
        {
            get
            {
                if (_connectionReuseHandler == null)
                {
                    _connectionReuseHandler = new ConnectionReuseHandler(new ConnectionPoolSettings(ConnectionPoolSettings));
                }

                return _connectionReuseHandler;
            }
        }

        public abstract IServiceProvider ApplicationServices { get; }

        // IConnectionBuilder was originally added to model the Kestrel ListenOptions class.
        // This is currently unused, but the goal of this would be to use this implementation
        // of IConnectionBuilder to build the connection plumbing that ends at NetMessageFramingConnectionHandler.
        // This will give more freedom for the source of the connection being something other
        // than Kestrel. For example, when we implement NetTcp port sharing.
        ConnectionDelegate IConnectionBuilder.Build()
        {
            ConnectionDelegate app = context =>
            {
                return Task.CompletedTask;
            };

            for (var i = _middleware.Count - 1; i >= 0; i--)
            {
                Func<ConnectionDelegate, ConnectionDelegate> component = _middleware[i];
                app = component(app);
            }

            return app;
        }

        public IConnectionBuilder Use(Func<ConnectionDelegate, ConnectionDelegate> middleware)
        {
            _middleware.Add(middleware);
            return this;
        }
    }
}
