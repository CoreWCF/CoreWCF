// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Net;
using System.Runtime.CompilerServices;
using CoreWCF.Configuration;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Helpers
{
    public static class ServiceHelper
    {

        public static IHost CreateWebHostBuilder<TStartup>(ITestOutputHelper outputHelper = default, string linuxSocketFilepath = "", [CallerMemberName] string callerMethodName = "") where TStartup : class
        {
            var startupType = typeof(TStartup);
            var configureServicesMethod = startupType.GetMethod("ConfigureServices", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, new Type[] { typeof(IServiceCollection) });
            var configureMethod = startupType.GetMethod("Configure", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, new Type[] { typeof(IHost) });
            var startupInstance = Activator.CreateInstance(startupType);
            var hostBuilder = Host.CreateDefaultBuilder(Array.Empty<string>());
            hostBuilder.UseUnixDomainSocket(options =>
            {
                options.Listen(new Uri("net.uds://" + linuxSocketFilepath));
            });
            if (configureServicesMethod != null)
            {
                var configureServiceAction = (Action<IServiceCollection>)configureServicesMethod.CreateDelegate(typeof(Action<IServiceCollection>), startupInstance);
                hostBuilder.ConfigureServices(configureServiceAction);
            }

            IHost host = hostBuilder.Build();
            if (configureMethod != null)
            {
                var configureAction = (Action<IHost>)configureMethod.CreateDelegate(typeof(Action<IHost>), startupInstance);
                configureAction(host);
            }

            return host;
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
