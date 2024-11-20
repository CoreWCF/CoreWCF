// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Description;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Configuration
{
    internal class ServiceBuilder : CommunicationObject, IServiceBuilder
    {
        private readonly IDictionary<Type, IServiceConfiguration> _services = new Dictionary<Type, IServiceConfiguration>();
        private readonly TaskCompletionSource<object> _openingCompletedTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        public ServiceBuilder(IServiceProvider serviceProvider, IApplicationLifetime appLifetime)
        {
            ServiceProvider = serviceProvider;
            appLifetime.ApplicationStarted.Register(() =>
            {
                if (State == CommunicationState.Created)
                {
                    // Using discard as any exceptions are swallowed from the ApplicationStarted
                    // callback and no place to await the OpenAsync call. Should only hit this code
                    // when hosting in IIS.
                    _ = OpenAsync();
                }
            });
            Opened += OpenedCallback;
        }

        public ICollection<IServiceConfiguration> ServiceConfigurations => _services.Values;

        public ICollection<Uri> BaseAddresses { get; } = new List<Uri>();

        ICollection<Type> IServiceBuilder.Services => _services.Keys;

        public IServiceProvider ServiceProvider { get; }

        protected override TimeSpan DefaultCloseTimeout => TimeSpan.FromMinutes(1);

        protected override TimeSpan DefaultOpenTimeout => TimeSpan.FromMinutes(1);

        public IServiceBuilder AddService<TService>() where TService : class
        {
            return AddService(typeof(TService));
        }

        public IServiceBuilder AddService<TService>(Action<ServiceOptions> options) where TService : class
        {
            return AddService(typeof(TService), options);
        }

        public IServiceBuilder AddService(Type service)
        {
            if (service is null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(service)));
            }
            var serviceConfig = (IServiceConfiguration)ServiceProvider.GetRequiredService(
                typeof(IServiceConfiguration<>).MakeGenericType(service));
            _services[serviceConfig.ServiceType] = serviceConfig;
            return this;
        }

        public IServiceBuilder AddService(Type service, Action<ServiceOptions> options)
        {
            if (service is null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(service)));
            }
            var serviceConfig = (IServiceConfiguration)ServiceProvider.GetRequiredService(
                typeof(IServiceConfiguration<>).MakeGenericType(service));
            _services[serviceConfig.ServiceType] = serviceConfig;
            ServiceConfigurationDelegateHolder holder = (ServiceConfigurationDelegateHolder)ServiceProvider
                .GetRequiredService(typeof(ServiceConfigurationDelegateHolder<>).MakeGenericType(service));
            holder.AddServiceOptionsDelegate(options);
            return this;
        }


        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, string address)
        {
            return AddServiceEndpoint<TService>(typeof(TContract), binding, address);
        }

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, string address, Action<ServiceEndpoint> configureEndpoint)
        {
            return AddServiceEndpoint<TService>(typeof(TContract), binding, address, configureEndpoint);
        }

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, Uri address)
        {
            return AddServiceEndpoint<TService>(typeof(TContract), binding, address);
        }

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, Uri address, Action<ServiceEndpoint> configureEndpoint)
        {
            return AddServiceEndpoint<TService>(typeof(TContract), binding, address, configureEndpoint);
        }

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, string address, Uri listenUri)
        {
            return AddServiceEndpoint<TService>(typeof(TContract), binding, address, listenUri);
        }

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, string address, Uri listenUri, Action<ServiceEndpoint> configureEndpoint)
        {
            return AddServiceEndpoint<TService>(typeof(TContract), binding, address, listenUri, configureEndpoint);
        }

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, Uri address, Uri listenUri)
        {
            return AddServiceEndpoint<TService>(typeof(TContract), binding, address, listenUri);
        }

        public IServiceBuilder AddServiceEndpoint<TService, TContract>(Binding binding, Uri address, Uri listenUri, Action<ServiceEndpoint> configureEndpoint)
        {
            return AddServiceEndpoint<TService>(typeof(TContract), binding, address, listenUri, configureEndpoint);
        }

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, string address)
        {
            return AddServiceEndpoint<TService>(implementedContract, binding, address, (Uri)null);
        }

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, string address, Action<ServiceEndpoint> configureEndpoint)
        {
            return AddServiceEndpoint<TService>(implementedContract, binding, address, (Uri)null, configureEndpoint);
        }

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, Uri address)
        {
            return AddServiceEndpoint<TService>(implementedContract, binding, address, (Uri)null);
        }

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, Uri address, Action<ServiceEndpoint> configureEndpoint)
        {
            return AddServiceEndpoint<TService>(implementedContract, binding, address, (Uri)null, configureEndpoint);
        }

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, string address, Uri listenUri)
        {
            if (address is null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(address)));
            }

            return AddServiceEndpoint<TService>(implementedContract, binding, new Uri(address, UriKind.RelativeOrAbsolute), listenUri);
        }

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, string address, Uri listenUri, Action<ServiceEndpoint> configureEndpoint)
        {
            if (address is null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(address)));
            }

            return AddServiceEndpoint<TService>(implementedContract, binding, new Uri(address, UriKind.RelativeOrAbsolute), listenUri, configureEndpoint);
        }

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, Uri address, Uri listenUri)
        {
            return AddServiceEndpoint(typeof(TService), implementedContract, binding, address, listenUri, null);
        }

        public IServiceBuilder AddServiceEndpoint<TService>(Type implementedContract, Binding binding, Uri address, Uri listenUri, Action<ServiceEndpoint> configureEndpoint)
        {
            return AddServiceEndpoint(typeof(TService), implementedContract, binding, address, listenUri, configureEndpoint);
        }

        public IServiceBuilder AddServiceEndpoint(Type service, Type implementedContract, Binding binding, Uri address, Uri listenUri)
        {
            return AddServiceEndpoint(service, implementedContract, binding, address, listenUri, null);
        }

        public IServiceBuilder AddServiceEndpoint(Type service, Type implementedContract, Binding binding, Uri address, Uri listenUri, Action<ServiceEndpoint> configureEndpoint)
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

            if (address is null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(address)));
            }

            if (_services.TryGetValue(service, out IServiceConfiguration serviceConfig))
            {
                serviceConfig.Endpoints.Add(new ServiceEndpointConfiguration()
                {
                    Address = address,
                    Binding = binding,
                    Contract = implementedContract,
                    ListenUri = listenUri,
                    ConfigureEndpoint = configureEndpoint
                });
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError((new ArgumentException(SR.Format(SR.UnknownServiceConfiguration), nameof(service))));
            }

            return this;
        }

        protected override void OnAbort()
        {
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        protected override Task OnOpenAsync(CancellationToken token) => Task.CompletedTask;

        private void OpenedCallback(object sender, EventArgs e)
        {
            // Using a callback to complete the TCS means the state will have transitioned to Opened before completing the TCS
            // which means if another thread is waiting on this TCS, ServiceBuilder is in the expected state when it continues.
            // This callback needs to run before any other event handlers as they can (and the HTTP middleware indirectly does) depend on
            // the TCS being completed before they run.
            _openingCompletedTcs.TrySetResult(null);
        }

        protected override void OnFaulted()
        {
            base.OnFaulted();
            _openingCompletedTcs.TrySetResult(null);
        }

        // This is to allow the ServiceHostObjectModel to wait until all the Opening event handlers have ran
        // to do some configuration such as adding base addresses before the actual service dispatcher is created.
        internal Task WaitForOpening()
        {
            return _openingCompletedTcs.Task;
        }
    }
}
