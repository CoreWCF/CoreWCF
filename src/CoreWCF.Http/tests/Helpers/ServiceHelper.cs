using CoreWCF;
using CoreWCF.Channels;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
#if NET472
using System.Security.Authentication;
#endif // NET472
using Xunit.Abstractions;

namespace Helpers
{
    public static class ServiceHelper
    {
        public static IWebHostBuilder CreateWebHostBuilder<TStartup>(ITestOutputHelper outputHelper) where TStartup : class =>
            WebHost.CreateDefaultBuilder(new string[0])
#if DEBUG
            .ConfigureLogging((ILoggingBuilder logging) =>
            {
                logging.AddProvider(new XunitLoggerProvider(outputHelper));
                logging.AddFilter("Default", LogLevel.Debug);
                logging.AddFilter("Microsoft", LogLevel.Debug);
                logging.SetMinimumLevel(LogLevel.Debug);
            })
#endif // DEBUG
            .UseUrls("http://localhost:8080")
            .UseStartup<TStartup>();

        public static IWebHostBuilder CreateHttpsWebHostBuilder<TStartup>(ITestOutputHelper outputHelper) where TStartup : class =>
            WebHost.CreateDefaultBuilder(new string[0])
#if DEBUG
            .ConfigureLogging((ILoggingBuilder logging) =>
            {
                logging.AddProvider(new XunitLoggerProvider(outputHelper));
                logging.AddFilter("Default", LogLevel.Debug);
                logging.AddFilter("Microsoft", LogLevel.Debug);
                logging.SetMinimumLevel(LogLevel.Debug);
            })
#endif // DEBUG
            .UseKestrel(options =>
            {
                options.Listen(IPAddress.Loopback, 8080);
                options.Listen(address: IPAddress.Loopback, 8443, listenOptions =>
                {
                    listenOptions.UseHttps(httpsOptions =>
                    {
#if NET472
                        httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls;
#endif // NET472
                    });
                });
            })
            .UseUrls("http://localhost:8080", "https://localhost:8443")
            .UseStartup<TStartup>();

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

        public static Binding GetCustomBinding()
        {
            HttpTransportBindingElement httpBE = new HttpTransportBindingElement();
            httpBE.MaxReceivedMessageSize = int.MaxValue;
            httpBE.TransferMode = TransferMode.StreamedResponse;
            CustomBinding binding = new CustomBinding(new TextMessageEncodingBindingElement(), httpBE)
            {
                SendTimeout = TimeSpan.FromMinutes(3),
                ReceiveTimeout = TimeSpan.FromMinutes(3)
            };
            return binding;
        }
    }
}
