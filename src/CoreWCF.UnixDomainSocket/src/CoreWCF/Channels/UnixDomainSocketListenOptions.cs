// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Connections;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    public class UnixDomainSocketListenOptions : IConnectionBuilder
    {
        // Some properties which were originally set via ConnectionOrientedTransportBindingElement (eg ConnectionBufferSize) make more sense to
        // add to this class. If there are multiple endpoints using named pipes, on .NET Framework the first endpoint would establish the settings
        // and subsequent ones would need validate that the settings matched. By moving the settings needed to be considered for a shared listener
        // to NamedPipeListenOptions, we remove the need to reconcile multiple bindings and just set it on the classes which are responsible
        // for that part of the IO.
        private readonly List<Func<ConnectionDelegate, ConnectionDelegate>> _middleware = new List<Func<ConnectionDelegate, ConnectionDelegate>>();
        private List<SecurityIdentifier> _allowedUsers;

        internal UnixDomainSocketListenOptions(Uri baseAddress)
        {
            UnixDomainSocketUri.Validate(baseAddress);
            BaseAddress = baseAddress;
            FilePath = baseAddress.AbsolutePath;
        }

        /*https://github.com/CoreWCF/CoreWCF/issues/1194
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
        }*/

        // This property will return null if the AllowedUsers getter hasn't been used
        internal List<SecurityIdentifier> InternalAllowedUsers => _allowedUsers;

        public IServiceProvider ApplicationServices => UnixDomainSocketServerOptions?.ApplicationServices;
        public Uri BaseAddress { get; }
        public string FilePath { get; }

        public UnixDomainSocketOptions UnixDomainSocketServerOptions { get; internal set; }

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
