// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public interface IServiceBuilder : ICommunicationObject
    {
        ICollection<Type> Services { get; }
        ICollection<Uri> BaseAddresses { get; }

        IServiceBuilder AddService<TService>() where TService : class;
        IServiceBuilder AddService<TService>(Action<ServiceOptions> options) where TService : class;
        IServiceBuilder AddService(Type service);
        IServiceBuilder AddService(Type service, Action<ServiceOptions> options);
        IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, string address);
        IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, Uri address);
        IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, string address, Uri listenUri);
        IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, Uri address, Uri listenUri);
        IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, string address);
        IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, Uri address);
        IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, string address, Uri listenUri);
        IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, Uri address, Uri listenUri);
        IServiceBuilder AddServiceEndpoint(Type service, Type implementedContract, Binding binding, Uri address, Uri listenUri);
    }
}
