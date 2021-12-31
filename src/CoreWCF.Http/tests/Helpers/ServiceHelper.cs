// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;
using CoreWCF.Channels;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using ServiceContract;
using System;
using System.IO;
using System.Diagnostics;
using System.Net;
#if NET472
using System.Security.Authentication;
#endif // NET472
using System.Text;
using Xunit.Abstractions;
using System.Security.Cryptography.X509Certificates;

namespace Helpers
{
    public static class ServiceHelper
    {
        public static Binding GetBufferedModHttp1Binding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding();
            HttpTransportBindingElement httpTransportBindingElement = basicHttpBinding.CreateBindingElements().Find<HttpTransportBindingElement>();
            MessageVersion messageVersion = basicHttpBinding.MessageVersion;
            MessageEncodingBindingElement encodingBindingElement = new BinaryMessageEncodingBindingElement();
            httpTransportBindingElement.TransferMode = TransferMode.Streamed;
            return new CustomBinding(new BindingElement[]
            {
                encodingBindingElement,
                httpTransportBindingElement
            })
            {
                SendTimeout = TimeSpan.FromMinutes(20.0),
                ReceiveTimeout = TimeSpan.FromMinutes(20.0),
                OpenTimeout = TimeSpan.FromMinutes(20.0),
                CloseTimeout = TimeSpan.FromMinutes(20.0)
            };
        }

        //public static Binding GetBufferedModHttp2Binding()
        //{
        //    BasicHttpBinding basicHttpBinding = new BasicHttpBinding();
        //    HttpTransportBindingElement httpTransportBindingElement = basicHttpBinding.CreateBindingElements().Find<HttpTransportBindingElement>();
        //    MessageVersion messageVersion = basicHttpBinding.MessageVersion;
        //    MessageEncodingBindingElement encodingBindingElement = new TextMessageEncodingBindingElement(messageVersion, Encoding.Unicode);
        //    httpTransportBindingElement.TransferMode = TransferMode.Streamed;
        //    return new CustomBinding(new BindingElement[]
        //    {
        //        encodingBindingElement,
        //        httpTransportBindingElement
        //    })
        //    {
        //        SendTimeout = TimeSpan.FromMinutes(20.0),
        //        ReceiveTimeout = TimeSpan.FromMinutes(20.0),
        //        OpenTimeout = TimeSpan.FromMinutes(20.0),
        //        CloseTimeout = TimeSpan.FromMinutes(20.0)
        //    };
        //}
        //public static Binding GetBufferedModHttp3Binding()
        //{
        //    BasicHttpBinding basicHttpBinding = new BasicHttpBinding();
        //    HttpTransportBindingElement httpTransportBindingElement = basicHttpBinding.CreateBindingElements().Find<HttpTransportBindingElement>();
        //    MessageVersion messageVersion = basicHttpBinding.MessageVersion;
        //    MessageEncodingBindingElement encodingBindingElement = new TextMessageEncodingBindingElement(messageVersion, Encoding.UTF8);
        //    httpTransportBindingElement.TransferMode = TransferMode.Streamed;
        //    return new CustomBinding(new BindingElement[]
        //    {
        //        encodingBindingElement,
        //        httpTransportBindingElement
        //    })
        //    {
        //        SendTimeout = TimeSpan.FromMinutes(20.0),
        //        ReceiveTimeout = TimeSpan.FromMinutes(20.0),
        //        OpenTimeout = TimeSpan.FromMinutes(20.0),
        //        CloseTimeout = TimeSpan.FromMinutes(20.0)
        //    };
        //}

        public static IWebHostBuilder CreateHttpSysBuilder<TStartup>(ITestOutputHelper outputHelper = default) where TStartup : class =>
            WebHost.CreateDefaultBuilder(Array.Empty<string>())
#if DEBUG
            .ConfigureLogging((ILoggingBuilder logging) =>
            {
                if (outputHelper != default)
                    logging.AddProvider(new XunitLoggerProvider(outputHelper));
                logging.AddFilter("Default", LogLevel.Debug);
                logging.AddFilter("Microsoft", LogLevel.Debug);
                logging.SetMinimumLevel(LogLevel.Debug);
            })
#endif // DEBUG
            .UseHttpSys(options =>
            {
                options.Authentication.Schemes = Microsoft.AspNetCore.Server.HttpSys.AuthenticationSchemes.None;
                options.Authentication.AllowAnonymous = true;
                options.AllowSynchronousIO = true;
                options.UrlPrefixes.Add("http://+:80/Temporary_Listen_Addresses/CoreWCFTestServices");
                options.UrlPrefixes.Add("http://+:80/Temporary_Listen_Addresses/CoreWCFTestServices/MorePath");
            })
            .UseStartup<TStartup>();

        public static IWebHostBuilder CreateWebHostBuilder<TStartup>(ITestOutputHelper outputHelper = default) where TStartup : class =>
            WebHost.CreateDefaultBuilder(Array.Empty<string>())
#if DEBUG
            .ConfigureLogging((ILoggingBuilder logging) =>
            {
                if(outputHelper != default)
                    logging.AddProvider(new XunitLoggerProvider(outputHelper));
                logging.AddFilter("Default", LogLevel.Debug);
                logging.AddFilter("Microsoft", LogLevel.Debug);
                logging.SetMinimumLevel(LogLevel.Debug);
            })
#endif // DEBUG
            .UseKestrel(options =>
            {
                    options.AllowSynchronousIO = true;
                    options.Listen(IPAddress.Loopback, 8080, listenOptions =>
                    {
                        if (Debugger.IsAttached)
                        {
                            listenOptions.UseConnectionLogging();
                        }
                    });
                })
            .UseStartup<TStartup>();

        public static IWebHostBuilder CreateWebHostBuilder(ITestOutputHelper outputHelper, Type startupType) =>
            WebHost.CreateDefaultBuilder(Array.Empty<string>())
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
                options.AllowSynchronousIO = true;
                options.Listen(IPAddress.Loopback, 8080, listenOptions =>
                {
                    if (Debugger.IsAttached)
                    {
                        listenOptions.UseConnectionLogging();
                    }
                });
            })
            .UseStartup(startupType);

        public static IWebHostBuilder CreateHttpsWebHostBuilder<TStartup>(ITestOutputHelper outputHelper = default) where TStartup : class =>
            WebHost.CreateDefaultBuilder(Array.Empty<string>())
#if DEBUG
            .ConfigureLogging((ILoggingBuilder logging) =>
            {
                if(outputHelper != default)
                    logging.AddProvider(new XunitLoggerProvider(outputHelper));
                logging.AddFilter("Default", LogLevel.Debug);
                logging.AddFilter("Microsoft", LogLevel.Debug);
                logging.SetMinimumLevel(LogLevel.Debug);
            })
#endif // DEBUG
            .UseKestrel(options =>
            {
                options.Listen(address: IPAddress.Loopback, 8444, listenOptions =>
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
            .UseStartup<TStartup>();

        public static IWebHostBuilder CreateHttpsWebHostBuilder(ITestOutputHelper outputHelper, Type startupType) =>
            WebHost.CreateDefaultBuilder(Array.Empty<string>())
#if DEBUG
            .ConfigureLogging((ILoggingBuilder logging) =>
            {
                if (outputHelper != default)
                    logging.AddProvider(new XunitLoggerProvider(outputHelper));
                logging.AddFilter("Default", LogLevel.Debug);
                logging.AddFilter("Microsoft", LogLevel.Debug);
                logging.SetMinimumLevel(LogLevel.Debug);
            })
#endif // DEBUG
            .UseKestrel(options =>
            {
                options.Listen(address: IPAddress.Loopback, 8444, listenOptions =>
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
            .UseStartup(startupType);

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

        public class NoneSerializableStream : MemoryStream
        {
        }

        public static void PopulateStreamWithStringBytes(Stream stream, string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            byte[] array = bytes;
            for (int i = 0; i < array.Length; i++)
            {
                byte value = array[i];
                stream.WriteByte(value);
            }

            stream.Position = 0L;
        }

        public static Stream GetStreamWithStringBytes(string s)
        {
            Stream stream = new NoneSerializableStream();
            PopulateStreamWithStringBytes(stream, s);
            return stream;
        }

        public static string GetStringFrom(Stream s)
        {
            StreamReader streamReader = new StreamReader(s, Encoding.UTF8);
            return streamReader.ReadToEnd();
        }

        public static MessageContractStreamNoHeader GetMessageContractStreamNoHeader(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                throw new ArgumentNullException("input cannot bindingElement null to make GetMessageContractStreamNoHeader");
            }

            Stream streamWithStringBytes = GetStreamWithStringBytes(s);
            return new MessageContractStreamNoHeader
            {
                stream = streamWithStringBytes
            };
        }

        public static MessageContractStreamOneIntHeader GetMessageContractStreamOneIntHeader(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                throw new ArgumentNullException("input cannot bindingElement null to make GetMessageContractStreamNoHeader");
            }

            Stream streamWithStringBytes = GetStreamWithStringBytes(s);
            return new MessageContractStreamOneIntHeader
            {
                input = streamWithStringBytes
            };
        }

        public static MessageContractStreamTwoHeaders GetMessageContractStreamTwoHeaders(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                throw new ArgumentNullException("input cannot bindingElement null to make GetMessageContractStreamTwoHeaders");
            }
            Stream streamWithStringBytes = GetStreamWithStringBytes(s);
            return new MessageContractStreamTwoHeaders
            {
                Stream = streamWithStringBytes
            };
        }

        public static string GetStringFrom(MessageContractStreamTwoHeaders input)
        {
            if (input == null)
            {
                throw new ArgumentNullException("MessageContractStreamTwoHeaders is null");
            }
            Stream stream = input.Stream;
            return GetStringFrom(stream);
        }

        public static string GetStringFrom(MessageContractStreamNoHeader input)
        {
            if (input == null)
            {
                throw new ArgumentNullException("MessageContractStreamNoHeader is null");
            }
            Stream stream = input.stream;
            return GetStringFrom(stream);
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
    }
}
