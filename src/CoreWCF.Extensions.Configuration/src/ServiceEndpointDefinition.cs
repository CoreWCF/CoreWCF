// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;

namespace CoreWCF.Extensions.Configuration
{
    /// <summary>
    /// One service endpoint read from configuration: the address, binding and contract that WCF calls the ABC of an
    /// endpoint, together with the service type exposing it.
    /// </summary>
    public sealed class ServiceEndpointDefinition
    {
        public ServiceEndpointDefinition(Type serviceType, Type contract, Binding binding, Uri address, Uri listenUri)
        {
            ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            Binding = binding ?? throw new ArgumentNullException(nameof(binding));
            Address = address ?? throw new ArgumentNullException(nameof(address));
            ListenUri = listenUri;
        }

        /// <summary>
        /// The service implementation type.
        /// </summary>
        public Type ServiceType { get; }

        /// <summary>
        /// The contract the endpoint exposes.
        /// </summary>
        public Type Contract { get; }

        /// <summary>
        /// The binding carrying the endpoint.
        /// </summary>
        public Binding Binding { get; }

        /// <summary>
        /// The endpoint address.
        /// </summary>
        public Uri Address { get; }

        /// <summary>
        /// The address to listen on when it differs from <see cref="Address"/>, otherwise <see langword="null"/>.
        /// </summary>
        public Uri ListenUri { get; }
    }
}
