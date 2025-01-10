// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Security.Policy;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace CoreWCF.Channels
{
    public class TcpListenOptions : NetFramingListenOptions
    {
        // Some properties which were originally set via ConnectionOrientedTransportBindingElement (eg ConnectionBufferSize) make more sense to
        // add to this class. If there are multiple endpoints using NetTcp, on .NET Framework the first endpoint would establish the settings
        // and subsequent ones would need validate that the settings matched. By moving the settings needed to be considered for a shared listener
        // to TcpListenOptions, we remove the need to reconcile multiple bindings and just set it on the classes which are responsible
        // for that part of the IO.
        internal TcpListenOptions(Uri baseAddress)
        {
            if (baseAddress.Scheme != Uri.UriSchemeNetTcp)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(baseAddress), SR.TcpUriSchemeWrong);
            BaseAddress = baseAddress;
        }

        public override IServiceProvider ApplicationServices => NetTcpServerOptions?.ApplicationServices;

        // This is the Kestrel ListenOptions which will be used to create the listener, and can be used to get the IPEndpoint
        internal ListenOptions ListenOptions { get; set; }

        public IPEndPoint IPEndpoint => ListenOptions?.IPEndPoint;

        public NetTcpOptions NetTcpServerOptions { get; internal set; }
    }
}
