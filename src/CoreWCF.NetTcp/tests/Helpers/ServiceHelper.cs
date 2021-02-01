using CoreWCF.Configuration;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using CoreWCF.Channels;
using CoreWCF;

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
            .UseNetTcp(0)
            .UseStartup<TStartup>();

        public static string GetNetTcpAddressInUse(this IWebHost host)
        {
            return $"net.tcp://localhost:{host.GetNetTcpPortInUse()}";
        }

        public static int GetNetTcpPortInUse(this IWebHost host)
        {
            var addresses = host.ServerFeatures.Get<IServerAddressesFeature>().Addresses;
            var addressInUse = new Uri(addresses.First(), UriKind.Absolute);
            return addressInUse.Port;
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

        public static CustomBinding GetCustomServerBinding(CompressionFormat serverCompressionFormat, TransferMode transferMode)
        {
            BinaryMessageEncodingBindingElement binaryMessageEncodingElement = new BinaryMessageEncodingBindingElement();
            binaryMessageEncodingElement.CompressionFormat = serverCompressionFormat;
            TcpTransportBindingElement tranportBE = new TcpTransportBindingElement();
            tranportBE.TransferMode = transferMode;
            tranportBE.MaxReceivedMessageSize = int.MaxValue;

            var customBinding = new CustomBinding();
            customBinding.Elements.Add(binaryMessageEncodingElement);
            customBinding.Elements.Add(tranportBE);
            return new CustomBinding(customBinding);
        }
    }
}
