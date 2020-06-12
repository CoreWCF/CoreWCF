﻿using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using System;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class ServiceContractNameSpaceTests
    {
        private ITestOutputHelper _output;
        public ServiceContractNameSpaceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
#if NET472
        [InlineData("XmlCharacters", "String From Client")]
        [InlineData("WhiteSpace", "String From Client")]
        [InlineData("XMLEncoded", "String From Client")]        
        [InlineData("XMLReservedCharacters", "String From Client")]
#endif
        [InlineData("NonAlphaCharacters", "String From Client")]
        [InlineData("LocalizedCharacters", "String From Client")]
        [InlineData("SurrogateCharacters", "String From Client")]
        [InlineData("URI", "String From Client")]
        public void SerivceContractNameSpace_784756(string method, string clientString)
        {
            string result = null;
            SerivceContractNameSpace._method = method;
            var host = ServiceHelper.CreateWebHostBuilder<SerivceContractNameSpace>(_output).Build();
            using (host)
            {
                host.Start();
                switch (method)
                {
                    case "XmlCharacters":
                        {
                            result = this.Variation_Service_XmlCharacters(clientString);
                            Assert.Equal(clientString, result);
                        }
                        break;
                    case "WhiteSpace":
                        {
                            result = this.Variation_Service_WhiteSpace(clientString);
                            Assert.Equal(clientString, result);
                        }
                        break;
                    case "XMLEncoded":
                        {
                            result = this.Variation_Service_XMLEncoded(clientString);
                            Assert.Equal(clientString, result);
                        }
                        break;

                    case "XMLReservedCharacters":
                        {
                            result = this.Variation_Service_XMLReservedCharacters(clientString);
                            Assert.Equal(clientString, result);
                        }
                        break;
                    case "NonAlphaCharacters":
                        {
                            result = this.Variation_Service_NonAlphaCharacters(clientString);
                            Assert.Equal(clientString, result);
                        }
                        break;
                    case "LocalizedCharacters":
                        {
                            result = this.Variation_Service_LocalizedCharacters(clientString);
                            Assert.Equal(clientString, result);
                        }
                        break;
                    case "SurrogateCharacters":
                        {
                            result = this.Variation_Service_SurrogateCharacters(clientString);
                            Assert.Equal(clientString, result);
                        }
                        break;
                    case "URI":
                        {
                            result = this.Variation_Service_URI(clientString);
                            Assert.Equal(clientString, result);
                        }
                        break;
                    default:
                        {
                            _output.WriteLine("Unknown ID : " + method);
                            Assert.Equal(clientString, result);
                            break;
                        }
                }
            }
        }

        private string Variation_Service_XmlCharacters(string clientString)
        {
            // Create the proxy
            var clientProxy = ClientHelper.GetProxy<ClientContract.IServiceContractNamespace_784756_XmlCharacters_Service>();
            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_XmlCharacters]");
            string response = clientProxy.Method1(clientString);
            _output.WriteLine("Testing [Variation_Service_XmlCharacters] returned <{0}>", response);
            return response;
        }

        private string Variation_Service_WhiteSpace(string clientString)
        {
            // Create the proxy            
            var clientProxy = ClientHelper.GetProxy<ClientContract.IServiceContractNamespace_784756_WhiteSpace_Service>();
            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_WhiteSpace]");
            string response = clientProxy.Method2(clientString);
            _output.WriteLine("Testing [Variation_Service_WhiteSpace] returned <{0}>", response);
            return response;
        }

        private string Variation_Service_XMLEncoded(string clientString)
        {
            // Create the proxy           
            var clientProxy = ClientHelper.GetProxy<ClientContract.IServiceContractNamespace_784756_XMLEncoded_Service>();
            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_XMLEncoded]");
            string response = clientProxy.Method3(clientString);
            _output.WriteLine("Testing [Variation_Service_XMLEncoded] returned <{0}>", response);
            return response;
        }

        private string Variation_Service_NonAlphaCharacters(string clientString)
        {
            // Create the proxy         
            var clientProxy = ClientHelper.GetProxy<ClientContract.IServiceContractNamespace_784756_NonAlphaCharacters_Service>();
            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_NonAlphaCharacters]");
            string response = clientProxy.Method4(clientString);
            _output.WriteLine("Testing [Variation_Service_NonAlphaCharacters] returned <{0}>", response);
            return response;
        }

        private string Variation_Service_LocalizedCharacters(string clientString)
        {
            // Create the proxy            
            var clientProxy = ClientHelper.GetProxy<ClientContract.IServiceContractNamespace_784756_LocalizedCharacters_Service>();
            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_LocalizedCharacters]");
            string response = clientProxy.Method5(clientString);
            _output.WriteLine("Testing [Variation_Service_LocalizedCharacters] returned <{0}>", response);
            return response;
        }

        private string Variation_Service_SurrogateCharacters(string clientString)
        {
            // Create the proxy           
            var clientProxy = ClientHelper.GetProxy<ClientContract.IServiceContractNamespace_784756_SurrogateCharacters_Service>();
            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_SurrogateCharacters]");
            string response = clientProxy.Method6(clientString);
            _output.WriteLine("Testing [Variation_Service_SurrogateCharacters] returned <{0}>", response);
            return response;
        }

        private string Variation_Service_XMLReservedCharacters(string clientString)
        {
            // Create the proxy        
            var clientProxy = ClientHelper.GetProxy<ClientContract.IServiceContractNamespace_784756_XMLReservedCharacters_Service>();
            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_XMLReservedCharacters]");
            string response = clientProxy.Method7(clientString);
            _output.WriteLine("Testing [Variation_Service_XMLReservedCharacters] returned <{0}>", response);
            return response;
        }

        private string Variation_Service_URI(string clientString)
        {
            // Create the proxy           
            var clientProxy = ClientHelper.GetProxy<ClientContract.IServiceContractNamespace_784756_URI_Service>();
            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_URI]");
            string response = clientProxy.Method8(clientString);
            _output.WriteLine("Testing [Variation_Service_URI] returned <{0}>", response);
            return response;
        }
    }

    internal class SerivceContractNameSpace
    {
        public static string _method;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(builder =>
            {
                switch (_method)
                {
                    case "XmlCharacters":
                        builder.AddService<Services.ServiceContractNamespace_784756_XmlCharacters_Service>();
                        builder.AddServiceEndpoint<Services.ServiceContractNamespace_784756_XmlCharacters_Service, IServiceContractNamespace_784756_XmlCharacters_Service>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                        break;
                    case "WhiteSpace":
                        builder.AddService<Services.ServiceContractNamespace_784756_WhiteSpace_Service>();
                        builder.AddServiceEndpoint<Services.ServiceContractNamespace_784756_WhiteSpace_Service, IServiceContractNamespace_784756_WhiteSpace_Service>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                        break;
                    case "XMLEncoded":
                        builder.AddService<Services.ServiceContractNamespace_784756_XMLEncoded_Service>();
                        builder.AddServiceEndpoint<Services.ServiceContractNamespace_784756_XMLEncoded_Service, IServiceContractNamespace_784756_XMLEncoded_Service>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                        break;
                    case "NonAlphaCharacters":
                        builder.AddService<Services.ServiceContractNamespace_784756_NonAlphaCharacters_Service>();
                        builder.AddServiceEndpoint<Services.ServiceContractNamespace_784756_NonAlphaCharacters_Service, IServiceContractNamespace_784756_NonAlphaCharacters_Service>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                        break;
                    case "LocalizedCharacters":
                        builder.AddService<Services.ServiceContractNamespace_784756_LocalizedCharacters_Service>();
                        builder.AddServiceEndpoint<Services.ServiceContractNamespace_784756_LocalizedCharacters_Service, IServiceContractNamespace_784756_LocalizedCharacters_Service>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                        break;
                    case "SurrogateCharacters":
                        builder.AddService<Services.ServiceContractNamespace_784756_SurrogateCharacters_Service>();
                        builder.AddServiceEndpoint<Services.ServiceContractNamespace_784756_SurrogateCharacters_Service, IServiceContractNamespace_784756_SurrogateCharacters_Service>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                        break;
                    case "XMLReservedCharacters":
                        builder.AddService<Services.ServiceContractNamespace_784756_XMLReservedCharacters_Service>();
                        builder.AddServiceEndpoint<Services.ServiceContractNamespace_784756_XMLReservedCharacters_Service, IServiceContractNamespace_784756_XMLReservedCharacters_Service>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                        break;
                    case "URI":
                        builder.AddService<Services.ServiceContractNamespace_784756_URI_Service>();
                        builder.AddServiceEndpoint<Services.ServiceContractNamespace_784756_URI_Service, IServiceContractNamespace_784756_URI_Service>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                        break;
                    default:
                        throw new ApplicationException("Unsupported ServiceType");
                }
            });
        }
    }
}
