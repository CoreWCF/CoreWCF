// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography.X509Certificates;
using System.Net;
using CoreWCF.Configuration;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Helpers
{
    public static class ServiceHelper
    {
        public static IWebHostBuilder CreateWebHostBuilder<TStartup>(ITestOutputHelper outputHelper, IPAddress ipAddress = null, int port = 0) where TStartup : class
        {
            if (ipAddress == null)
            {
                //using .Any breaks the getaddress method
                ipAddress = IPAddress.Loopback;
            }
            return WebHost.CreateDefaultBuilder(Array.Empty<string>())
#if DEBUG
            .ConfigureLogging((ILoggingBuilder logging) =>
            {
                logging.AddProvider(new XunitLoggerProvider(outputHelper));
                logging.AddFilter("Default", LogLevel.Debug);
                logging.AddFilter("Microsoft", LogLevel.Debug);
                logging.SetMinimumLevel(LogLevel.Debug);
            })
#endif // DEBUG
            .UseNetTcp(ipAddress, port)
            .UseStartup<TStartup>();
        }
        public static string GetNetTcpAddressInUse(this IWebHost host)
        {
            System.Collections.Generic.ICollection<string> addresses = host.ServerFeatures.Get<IServerAddressesFeature>().Addresses;
            var addressInUse = new Uri(addresses.First(), UriKind.Absolute);
            return $"net.tcp://{addressInUse.Host}:{addressInUse.Port}";
        }

        public static int GetNetTcpPortInUse(this IWebHost host)
        {
            System.Collections.Generic.ICollection<string> addresses = host.ServerFeatures.Get<IServerAddressesFeature>().Addresses;
            var addressInUse = new Uri(addresses.First(), UriKind.Absolute);
            return addressInUse.Port;
        }

        //only for test, don't use in production code
        public static X509Certificate2 GetServiceCertificate()
        {
            string AspNetHttpsOid = "1.3.6.1.4.1.311.84.1.1";
            X509Certificate2 foundCert = null;
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                // X509Store.Certificates creates a new instance of X509Certificate2Collection with
                // each access to the property. The collection needs to be cleaned up correctly so
                // keeping a single reference to fetched collection.
                store.Open(OpenFlags.ReadOnly);
                var certificates = store.Certificates;
                foreach (var cert in certificates)
                {
                    foreach (var extension in cert.Extensions)
                    {
                        if (AspNetHttpsOid.Equals(extension.Oid?.Value))
                        {
                            // Always clone certificate instances when you don't own the creation
                            foundCert = new X509Certificate2(cert);
                            break;
                        }
                    }

                    if (foundCert != null)
                    {
                        break;
                    }
                }
                // Cleanup
                foreach (var cert in certificates)
                {
                    cert.Dispose();
                }
            }

            return foundCert;
        }

        public static void CloseServiceModelObjects(params System.ServiceModel.ICommunicationObject[] objects)
        {
            foreach (System.ServiceModel.ICommunicationObject comObj in objects)
            {
                try
                {
                    if (comObj == null)
                    {
                        continue;
                    }
                    // Only want to call Close if it is in the Opened state
                    if (comObj.State == System.ServiceModel.CommunicationState.Opened)
                    {
                        comObj.Close();
                    }
                    // Anything not closed by this point should be aborted
                    if (comObj.State != System.ServiceModel.CommunicationState.Closed)
                    {
                        comObj.Abort();
                    }
                }
                catch (TimeoutException)
                {
                    comObj.Abort();
                }
                catch (System.ServiceModel.CommunicationException)
                {
                    comObj.Abort();
                }
            }
        }
    }
}
