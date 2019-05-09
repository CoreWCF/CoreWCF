using Microsoft.ServiceModel.Channels;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ServiceModel.Configuration
{
    public interface IServiceBuilder
    {
        ICollection<Type> Services { get; }
        ICollection<Uri> BaseAddresses { get; }
        void AddService<TService>() where TService : class;
        void AddServiceEndpoint<TService, TContract>(Binding binding, string address);
        void AddServiceEndpoint<TService, TContract>(Binding binding, Uri address);
        void AddServiceEndpoint<TService, TContract>(Binding binding, string address, Uri listenUri);
        void AddServiceEndpoint<TService, TContract>(Binding binding, Uri address, Uri listenUri);
        void AddServiceEndpoint<TService>(Type implementedContract, Binding binding, string address);
        void AddServiceEndpoint<TService>(Type implementedContract, Binding binding, Uri address);
        void AddServiceEndpoint<TService>(Type implementedContract, Binding binding, string address, Uri listenUri);
        void AddServiceEndpoint<TService>(Type implementedContract, Binding binding, Uri address, Uri listenUri);
    }
}
