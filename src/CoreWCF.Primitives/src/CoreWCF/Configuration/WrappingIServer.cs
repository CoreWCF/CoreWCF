using CoreWCF.Runtime;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Configuration
{
    internal class WrappingIServer : IServer
    {
        private readonly ServiceBuilder _serviceBuilder;

        public WrappingIServer(ServiceBuilder serviceBuilder)
        {
            _serviceBuilder = serviceBuilder;
        }

        public IFeatureCollection Features => InnerServer.Features;

        public IServer InnerServer { get; internal set; }

        public void Dispose()
        {
            InnerServer.Dispose();
        }

        public async Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        {
            await InnerServer.StartAsync(application, cancellationToken).ConfigureAwait(false);
            try
            {
                await _serviceBuilder.OpenAsync(cancellationToken);
            }
            catch(CallbackException e)
            {
                if (e.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                }

                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return InnerServer.StopAsync(cancellationToken);
        }
    }
}