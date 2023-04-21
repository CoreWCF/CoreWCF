// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace CoreWCF.Channels
{
    [SupportedOSPlatform("windows")]
    public class NamedPipeListenOptions : IConnectionBuilder
    {
        // Some properties which were originally set via ConnectionOrientedTransportBindingElement (eg ConnectionBufferSize) make more sense to
        // add to this class. If there are multiple endpoints using named pipes, on .NET Framework the first endpoint would establish the settings
        // and subsequent ones would need validate that the settings matched. By moving the settings needed to be considered for a shared listener
        // to NamedPipeListenOptions, we remove the need to reconcile multiple bindings and just set it on the classes which are responsible
        // for that part of the IO.
        private readonly List<Func<ConnectionDelegate, ConnectionDelegate>> _middleware = new List<Func<ConnectionDelegate, ConnectionDelegate>>();
        private HostNameComparisonMode _hostNameComparisonMode;
        private int _connectionBufferSize;
        private int _maxPendingAccepts;
        private List<SecurityIdentifier> _allowedUsers;

        internal NamedPipeListenOptions(Uri baseAddress)
        {
            PipeUri.Validate(baseAddress);
            BaseAddress = baseAddress;
            _connectionBufferSize = ConnectionOrientedTransportDefaults.ConnectionBufferSize;
            _hostNameComparisonMode = ConnectionOrientedTransportDefaults.HostNameComparisonMode;
            _maxPendingAccepts = ConnectionOrientedTransportDefaults.GetMaxPendingAccepts();
        }

        public List<SecurityIdentifier> AllowedUsers
        {
            get
            {
                if (_allowedUsers == null)
                {
                    _allowedUsers = new List<SecurityIdentifier>();
                }

                return _allowedUsers;
            }
        }

        // This property will return null if the AllowedUsers getter hasn't been used
        internal List<SecurityIdentifier> InternalAllowedUsers => _allowedUsers;

        public IServiceProvider ApplicationServices => NetNamedPipeServerOptions?.ApplicationServices;
        public Uri BaseAddress { get; }

        public int ConnectionBufferSize
        {
            get
            {
                return _connectionBufferSize;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value, SRCommon.ValueMustBeNonNegative));
                }

                _connectionBufferSize = value;
            }
        }

        public HostNameComparisonMode HostNameComparisonMode
        {
            get
            {
                return _hostNameComparisonMode;
            }

            set
            {
                HostNameComparisonModeHelper.Validate(value);
                _hostNameComparisonMode = value;
            }
        }

        public int MaxPendingAccepts
        {
            get
            {
                return _maxPendingAccepts;
            }

            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SRCommon.ValueMustBePositive));
                }

                _maxPendingAccepts = value;
            }
        }

        public NetNamedPipeOptions NetNamedPipeServerOptions { get; internal set; }

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
