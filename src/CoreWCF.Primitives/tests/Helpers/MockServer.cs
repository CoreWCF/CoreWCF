// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;

namespace Helpers
{

    internal class MockServer : IServer
    {
        public MockServer()
        {
            var addresses = new ServerAddressesFeature();
            addresses.Addresses.Add("http://localhost/");
            Features.Set<IServerAddressesFeature>(addresses);
        }

        public IFeatureCollection Features { get; } = new FeatureCollection();

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
