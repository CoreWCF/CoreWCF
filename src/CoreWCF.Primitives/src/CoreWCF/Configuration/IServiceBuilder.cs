using CoreWCF.Channels;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Configuration
{
    public interface IServiceBuilder
    {
        ICollection<Type> Services { get; }
        ICollection<Uri> BaseAddresses { get; }
        void AddService<TService>() where TService : class;
        void AddService(Type service);
        void AddServiceEndpoint<TService, TContract>(Binding binding, string address);
        void AddServiceEndpoint<TService, TContract>(Binding binding, Uri address);
        void AddServiceEndpoint<TService, TContract>(Binding binding, string address, Uri listenUri);
        void AddServiceEndpoint<TService, TContract>(Binding binding, Uri address, Uri listenUri);
        void AddServiceEndpoint<TService>(Type implementedContract, Binding binding, string address);
        void AddServiceEndpoint<TService>(Type implementedContract, Binding binding, Uri address);
        void AddServiceEndpoint<TService>(Type implementedContract, Binding binding, string address, Uri listenUri);
        void AddServiceEndpoint<TService>(Type implementedContract, Binding binding, Uri address, Uri listenUri);
        void AddServiceEndpoint(Type service, Type implementedContract, Binding binding, Uri address, Uri listenUri);
    }
}
