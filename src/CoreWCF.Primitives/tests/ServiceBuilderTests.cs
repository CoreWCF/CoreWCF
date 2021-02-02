// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class ServiceBuilderTests
    {
        [Fact]
        public void ServiceBuilderCanChain()
        {
            var services = new ServiceCollection();
            services.AddServiceModelServices();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IServiceBuilder builder = serviceProvider.GetRequiredService<IServiceBuilder>();

            Assert.Equal(builder, builder.AddService<SomeService>());
            Assert.Equal(builder, builder.AddService(typeof(SomeService)));
            Assert.Equal(builder, builder.AddServiceEndpoint<SomeService, IService>(new NoBinding(), new Uri("http://localhost:8088/SomeService.svc")));
            Assert.Equal(builder, builder.AddServiceEndpoint<SomeService, IService>(new NoBinding(), "http://localhost:8088/SomeService.svc"));
            Assert.Equal(builder, builder.AddServiceEndpoint<SomeService>(typeof(IService), new NoBinding(), new Uri("http://localhost:8088/SomeService.svc")));
            Assert.Equal(builder, builder.AddServiceEndpoint<SomeService>(typeof(IService), new NoBinding(), "http://localhost:8088/SomeService.svc"));
        }

        public class SomeService : IService { }

        public class NoBinding : Binding
        {
            public override string Scheme => "none";
            public override BindingElementCollection CreateBindingElements()
            {
                return new BindingElementCollection();
            }
        }
    }
}
