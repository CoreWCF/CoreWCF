using CoreWCF.Channels;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Configuration
{
    public interface IServiceBuilder : ICommunicationObject
    {
        ICollection<Type> Services { get; }
        ICollection<Uri> BaseAddresses { get; }

        IServiceBuilder AddService<TService>() where TService : class;
        IServiceBuilder AddService(Type service);
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
