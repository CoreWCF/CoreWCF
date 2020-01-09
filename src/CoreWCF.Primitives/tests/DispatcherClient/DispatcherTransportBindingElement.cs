using Microsoft.Extensions.DependencyInjection;
using System;
using System.ServiceModel.Channels;

namespace DispatcherClient
{
    internal class DispatcherTransportBindingElement<TService, TContract> : TransportBindingElement where TService : class
    {
        private Action<IServiceCollection> _configureServices;

        internal DispatcherTransportBindingElement(Action<IServiceCollection> configureServices)
        {
            _configureServices = configureServices;
        }

        public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
        {
            return new DispatcherChannelFactory<TChannel, TService, TContract>(_configureServices);
        }

        public override bool CanBuildChannelFactory<TChannel>(BindingContext context)
        {
            if (typeof(TChannel) == typeof(IRequestChannel) ||
                typeof(TChannel) == typeof(IRequestSessionChannel))
            {
                return true;
            }

            return false;
        }

        public override string Scheme => "corewcf";

        public override BindingElement Clone()
        {
            return this;
        }
    }
}
