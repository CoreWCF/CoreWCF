// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System;

namespace CoreWCF.Channels
{
    public class NetTcpOptions
    {
        internal List<TcpListenOptions> CodeBackedListenOptions { get; } = new List<TcpListenOptions>();
        public IServiceProvider ApplicationServices { get; internal set; }

        public void Listen(string baseAddress)
        {
            try
            {
                var baseAddressUri = new Uri(baseAddress);
                Listen(new Uri(baseAddress));
            }
            catch(UriFormatException ufe)
            {
                throw new CommunicationException($"Unable to parse base address {baseAddress}", ufe);
            }
        }

        public void Listen(Uri baseAddress) => Listen(baseAddress, _ => { });

        public void Listen(string baseAddress, Action<TcpListenOptions> configure) => Listen(new Uri(baseAddress), configure);

        public void Listen(Uri baseAddress, Action<TcpListenOptions> configure)
        {
            if (baseAddress == null) throw new ArgumentNullException(nameof(baseAddress));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var listenOptions = new TcpListenOptions(baseAddress);
            ApplyEndpointDefaults(listenOptions);
            configure(listenOptions);
            CodeBackedListenOptions.Add(listenOptions);
        }

        private void ApplyEndpointDefaults(TcpListenOptions listenOptions)
        {
            listenOptions.NetTcpServerOptions = this;
        }
    }
}
