// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace CoreWCF.Channels
{
    [SupportedOSPlatform("windows")]
    public class NetNamedPipeOptions
    {
        internal List<NamedPipeListenOptions> CodeBackedListenOptions { get; } = new List<NamedPipeListenOptions>();
        public IServiceProvider ApplicationServices { get; internal set; }

        public void Listen(string baseAddress) => Listen(new Uri(baseAddress));
        public void Listen(Uri baseAddress) => Listen(baseAddress, _ => { });

        public void Listen(string baseAddress, Action<NamedPipeListenOptions> configure) => Listen(new Uri(baseAddress), configure);
        public void Listen(Uri baseAddress, Action<NamedPipeListenOptions> configure)
        {
            if (baseAddress == null) throw new ArgumentNullException(nameof(baseAddress));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var listenOptions = new NamedPipeListenOptions(baseAddress);
            ApplyEndpointDefaults(listenOptions);
            configure(listenOptions);
            CodeBackedListenOptions.Add(listenOptions);
        }

        private void ApplyEndpointDefaults(NamedPipeListenOptions listenOptions)
        {
            listenOptions.NetNamedPipeServerOptions = this;
        }
    }
}
