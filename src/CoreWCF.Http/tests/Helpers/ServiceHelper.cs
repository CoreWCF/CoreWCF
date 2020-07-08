using System;
using System.Net;
using CoreWCF;
using CoreWCF.Channels;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
#if NET472
using System.Security.Authentication;
#endif // NET472
using Xunit.Abstractions;

namespace Helpers
{
    public static class ServiceHelper
    {
        public static CustomBinding GetBinding()
        {
            HttpTransportBindingElement httpTransportBindingElement = new CoreWCF.BasicHttpBinding().CreateBindingElements().Find<HttpTransportBindingElement>();
            httpTransportBindingElement.TransferMode = TransferMode.Streamed;
            httpTransportBindingElement.MaxReceivedMessageSize = long.MaxValue;
            httpTransportBindingElement.MaxBufferSize = int.MaxValue;
            BinaryMessageEncodingBindingElement binaryMessageEncodingBindingElement = new BinaryMessageEncodingBindingElement();
            return new CustomBinding(new BindingElement[]
            {
                binaryMessageEncodingBindingElement,
                httpTransportBindingElement
            })
            {
                SendTimeout = TimeSpan.FromMinutes(12.0),
                ReceiveTimeout = TimeSpan.FromMinutes(12.0),
                OpenTimeout = TimeSpan.FromMinutes(12.0),
                CloseTimeout = TimeSpan.FromMinutes(12.0)
            };
        }

        public static Binding GetBufferedModHttpBinding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding();
            HttpTransportBindingElement transportBindingElement = basicHttpBinding.CreateBindingElements().Find<HttpTransportBindingElement>();
            return ConfigureHttpBinding(transportBindingElement);

        }

        private static CustomBinding ConfigureHttpBinding(HttpTransportBindingElement transportBindingElement)
        {
            BinaryMessageEncodingBindingElement binaryMessageEncodingBindingElement = new BinaryMessageEncodingBindingElement();
            transportBindingElement.TransferMode = TransferMode.Streamed;
            transportBindingElement.MaxReceivedMessageSize = 2147483647L;
            transportBindingElement.MaxBufferSize = int.MaxValue;
            //transportBindingElement.UseDefaultWebProxy = false;
            CustomBinding customBinding = new CustomBinding(new BindingElement[]
            {
                binaryMessageEncodingBindingElement,
                transportBindingElement
            });
            ConfigureTimeout(customBinding);
            return customBinding;
        }

        private static void ConfigureTimeout(Binding binding)
        {
            int num = 12;
            num *= 2;
            binding.SendTimeout = TimeSpan.FromMinutes((double)num);
            binding.ReceiveTimeout = TimeSpan.FromMinutes((double)num);
            binding.OpenTimeout = TimeSpan.FromMinutes(5.0);
            binding.CloseTimeout = TimeSpan.FromMinutes(5.0);
        }

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
            .UseKestrel(options =>
                {
                    options.Listen(IPAddress.Loopback, 8080, listenOptions =>
                    {
                        if (Debugger.IsAttached)
                        {
                            listenOptions.UseConnectionLogging();
                        }
                    });
                })
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
                options.Listen(IPAddress.Loopback, 8080, listenOptions =>
                {
                    if (Debugger.IsAttached)
                    {
                        listenOptions.UseConnectionLogging();
                    }
                });
                options.Listen(address: IPAddress.Loopback, 8443, listenOptions =>
                {
                    listenOptions.UseHttps(httpsOptions =>
                    {
#if NET472
                        httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls;
#endif // NET472
                    });
                    if (Debugger.IsAttached)
                    {
                        listenOptions.UseConnectionLogging();
                    }
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

    }
}
