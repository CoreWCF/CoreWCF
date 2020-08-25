using Microsoft.Extensions.DependencyInjection;
using CoreWCF.Channels;
using System;
using System.Collections.Generic;

namespace CoreWCF.Configuration
{
    internal class ServiceBuilder : IServiceBuilder
    {
        private IServiceProvider _serviceProvider;
        private IDictionary<Type, IServiceConfiguration> _services = new Dictionary<Type, IServiceConfiguration>();

        public ServiceBuilder(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ICollection<IServiceConfiguration> ServiceConfigurations => _services.Values;

        public ICollection<Uri> BaseAddresses { get; } = new List<Uri>();

        ICollection<Type> IServiceBuilder.Services => _services.Keys;

        public IServiceProvider ServiceProvider => _serviceProvider;

        public IServiceBuilder AddService<TService>() where TService : class
        {
            return AddService(typeof(TService));
        }

        public IServiceBuilder AddService(Type service)
        {
            if (service is null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(service)));
            }
            var serviceConfig = (IServiceConfiguration)_serviceProvider.GetRequiredService(
                typeof(IServiceConfiguration<>).MakeGenericType(service));
            _services[serviceConfig.ServiceType] = serviceConfig;
            return this;
        }

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, string address)
        {
            return AddServiceEndpoint<TService>(typeof(TContract), binding, address);
        }

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, Uri address)
        {
            return AddServiceEndpoint<TService>(typeof(TContract), binding, address);
        }

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, string address, Uri listenUri)
        {
            return AddServiceEndpoint<TService>(typeof(TContract), binding, address, listenUri);
        }

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, Uri address, Uri listenUri)
        {
            return AddServiceEndpoint<TService>(typeof(TContract), binding, address, listenUri);
        }

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, string address)
        {
            return AddServiceEndpoint<TService>(implementedContract, binding, address, (Uri)null);
        }

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, Uri address)
        {
            return AddServiceEndpoint<TService>(implementedContract, binding, address, (Uri)null);
        }

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, string address, Uri listenUri)
        {
            if (address is null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(address)));
            }

            return AddServiceEndpoint<TService>(implementedContract, binding, new Uri(address, UriKind.RelativeOrAbsolute), listenUri);
        }

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, Uri address, Uri listenUri)
        {
            return AddServiceEndpoint(typeof(TService), implementedContract, binding, address, listenUri);
        }

        public IServiceBuilder AddServiceEndpoint(Type service, Type implementedContract, Binding binding, Uri address, Uri listenUri)
        {
            if (service is null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(service)));
            }

            if (implementedContract is null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(implementedContract)));
            }

            if (binding is null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(binding)));
            }

            if (_services.TryGetValue(service, out IServiceConfiguration serviceConfig))
            {
                serviceConfig.Endpoints.Add(new ServiceEndpointConfiguration()
                {
                    Address = address,
                    Binding = binding,
                    Contract = implementedContract,
                    ListenUri = listenUri
                });
            }
            else
            {
                // TODO: Either find an existing SR to use or create a new one.
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(nameof(service)));
            }

            return this;
        }
    }
}
