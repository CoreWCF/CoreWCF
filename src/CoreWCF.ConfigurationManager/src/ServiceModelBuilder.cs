// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using CoreWCF.Description;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Configuration
{
    public class ServiceModelBuilder
    {
        public ServiceModelBuilder(IServiceCollection services)
        {
            Services = services;
        }

        public IServiceCollection Services { get; }

        public ServiceModelBuilder AddServiceEndpoint(Action<ServiceModelOptions> configure)
        {
            return AddServiceEndpoint(string.Empty, configure);
        }

        public ServiceModelBuilder AddServiceEndpoint(string name, Action<ServiceModelOptions> configure)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            Services.Configure(name, configure);

            return this;
        }

        public ServiceModelBuilder AddGlobalBehavior(IEndpointBehavior behavior)
        {
            Services.ConfigureAll<ServiceModelOptions>(option =>
            {
                foreach (var service in option.Services)
                {
                    //service.Behaviors.Add(behavior);
                }
            });

            return this;
        }

        //public ServiceModelBuilder AddDefaultChannel<T>(string name)
        //    where T : class
        //{
        //    Services.AddSingleton(ctx => ctx.GetRequiredService<IChannelFactoryProvider>().CreateChannelFactory<T>(name).CreateChannel());

        //    return this;
        //}
    }
}
