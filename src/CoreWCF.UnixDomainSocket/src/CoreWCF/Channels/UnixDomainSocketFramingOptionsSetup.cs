using System;
using CoreWCF.Channels;
using CoreWCF.Channels.Framing;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using Microsoft.AspNetCore.Connections;
using System.IO;

namespace CoreWCF.Channels
{
    internal class UnixDomainSocketFramingOptionsSetup : IConfigureOptions<KestrelServerOptions>
    {
        private readonly ILogger<UnixDomainSocketOptions> _logger;
        private readonly ILogger<FramingConnection> _framingConnectionLogger;
        private readonly IServiceProvider _serviceProvider;
        private readonly UnixDomainSocketOptions _options;
        private readonly IServiceBuilder _serviceBuilder;

        public UnixDomainSocketFramingOptionsSetup(IOptions<UnixDomainSocketOptions> options, IServiceBuilder serviceBuilder, ILogger<UnixDomainSocketOptions> logger, ILogger<FramingConnection> framingConnectionLogger, IServiceProvider serviceProvider)
        {
            _options = options.Value ?? new UnixDomainSocketOptions();
            _serviceBuilder = serviceBuilder;
            _logger = logger;
            _framingConnectionLogger = framingConnectionLogger;
            _serviceProvider = serviceProvider;
        }

        public List<ListenOptions> ListenOptions { get; } = new List<ListenOptions>();

        public bool AttachUDS { get; set; }

        public void Configure(KestrelServerOptions options)
        {
            if (AttachUDS)
            {
                options.ApplicationServices = _serviceProvider;
                foreach (UnixDomainSocketListenOptions listenOption in _options.CodeBackedListenOptions)
                {
                    string filePath = listenOption.FilePath;
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                    catch(Exception ex)
                    {
                        throw;
                    }
                    
                    
                    //Provide Path here
                    options.ListenUnixSocket(filePath, builder =>
                    {
                        // builder.Use(ConvertExceptionsAndAddLogging);
                        builder.UseConnectionHandler<NetMessageFramingConnectionHandler>();
                        // Save the ListenOptions to be able to get final port number for adding BaseAddresses later
                        ListenOptions.Add(builder);
                    });
                }

                _serviceBuilder.Opening += OnServiceBuilderOpening;
            }
        }

        
        private ConnectionDelegate ConvertExceptionsAndAddLogging(ConnectionDelegate next)
        {
            return (ConnectionContext context) =>
            {
                var logger = new ConnectionIdWrappingLogger(_framingConnectionLogger, context.ConnectionId);
                context.Features.Set<ILogger>(logger);

                //TODO: Add a public api mechanism to enable connection logging in RELEASE build
#if DEBUG
                context.Transport = new UnixDomainSocketExceptionConvertingDuplexPipe(new LoggingDuplexPipe(context.Transport, logger) { LoggingEnabled = true });
#else
                context.Transport = new UnixDomainSocketExceptionConvertingDuplexPipe(context.Transport);
#endif
                return next(context);
            };
        }

        private void OnServiceBuilderOpening(object sender, EventArgs e)
        {
            UpdateServiceBuilderBaseAddresses();
        }

        internal void UpdateServiceBuilderBaseAddresses()
        {
            foreach (ListenOptions listenOptions in ListenOptions)
            {
                string address = listenOptions.SocketPath;
                var baseAddress = new Uri($"net.uds://{address}/");
                _logger.LogDebug($"Adding base address {baseAddress} to ServiceBuilderOptions");
                _serviceBuilder.BaseAddresses.Add(baseAddress);
            }

            ListenOptions.Clear();
        }
    }
}

