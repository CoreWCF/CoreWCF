// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel;
using ClientContract;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class ContractBehaviorTests
    {
        private readonly ITestOutputHelper _output;

        public ContractBehaviorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("ByHand")]
        [InlineData("CustomAttribute")]
        [InlineData("TwoAttributesDifferentTypes")]
        [InlineData("MisplacedAttributes")]
        [InlineData("CustomAttributesImplementsOther")]
        [InlineData("ByHandImplementsOther")]
#if NET472
        [InlineData("ByHand_UsingHiddenProperty")]
#endif
        public void Variations(string method)
        {
            Startup._method = method;
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                switch (method)
                {
                    case "ByHand":
                        Variation_ByHand(false);
                        break;
                    case "ByHand_UsingHiddenProperty":
                        Variation_ByHand(true);
                        break;
                    case "CustomAttribute":
                        Variation_CustomAttribute();
                        break;
                    case "TwoAttributesDifferentTypes":
                        Variation_TwoAttributesDifferentTypes();
                        break;
                    case "MisplacedAttributes":
                        Variation_MisplacedAttributes();
                        break;
                    case "CustomAttributesImplementsOther":
                        Variation_CustomAttributesImplementsOther();
                        break;
                    case "ByHandImplementsOther":
                        Variation_ByHandImplementsOther();
                        break;
                    default:
                        throw new ApplicationException("Unsupported ID specified!");
                }
            }
        }

        [Fact]
        public void TwoAttributesSameType_Test()
        {
            Startup._method = "TwoAttributesSameType";
            using (IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build())
            {
                Assert.Throws<ArgumentException>(() => host.Start());
            }
        }

        //Variation
        //1.GetEndpointAddress for the service
        //2.CreateChannelFactory
        //2.1:Add the custom behavior - Optional
        //3.Get the BehaviorAttribute instance in the ChannelDescription 
        //4.Open the ChannelFactory
        //5.Check the Behavior static flags 
        //6.Check the Behavior instance flags
        //7.Send a message to the server
        public static ChannelFactory<T> GetChannelFactory<T>()
        {
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            return new ChannelFactory<T>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/ContractBehaviorService.svc")));
        }

        private void Variation_ByHand(bool useHiddenProperty)
        {
            ChannelFactory<IContractBehaviorBasic_ByHand> cf = GetChannelFactory<IContractBehaviorBasic_ByHand>();
            try
            {
                string HelloStr = "ByHand";
                CustomContractBehaviorAttribute cb = new CustomContractBehaviorAttribute();
                if (useHiddenProperty)
                {
#if NET472
                    cf.Endpoint.Contract.Behaviors.Add(cb);
                    HelloStr = "ByHand_UsingHiddenProperty";
#endif
                }
                else
                {
                    cf.Endpoint.Contract.ContractBehaviors.Add(cb);
                }

                cf.Open();
                string expected = "IContractBehavior:ClientContract.CustomContractBehaviorAttribute;";
                Assert.Equal(expected, BehaviorInvokedVerifier.ValidateClientInvokedBehavior(cf.Endpoint));
                IContractBehaviorBasic_ByHand clientProxy = cf.CreateChannel();
                string returnStr = clientProxy.StringMethod(HelloStr);
                Assert.Equal(HelloStr, returnStr);
            }
            catch
            {
                throw;
            }
            finally
            {
                if (cf != null && cf.State == System.ServiceModel.CommunicationState.Opened)
                {
                    cf.Close();
                }
            }
        }

        private void Variation_CustomAttribute()
        {
            ChannelFactory<IContractBehaviorBasic_CustomAttribute> cf = GetChannelFactory<IContractBehaviorBasic_CustomAttribute>();
            try
            {
                cf.Open();
                string expected = "IContractBehavior:ClientContract.CustomContractBehaviorAttribute;";
                Assert.Equal(expected, BehaviorInvokedVerifier.ValidateClientInvokedBehavior(cf.Endpoint));
                IContractBehaviorBasic_CustomAttribute clientProxy = cf.CreateChannel();
                string HelloStr = "CustomAttribute";
                string returnStr = clientProxy.StringMethod(HelloStr);
                Assert.Equal(HelloStr, returnStr);
            }
            catch
            {
                throw;
            }
            finally
            {
                if (cf != null && cf.State == System.ServiceModel.CommunicationState.Opened)
                {
                    cf.Close();
                }
            }
        }

        private void Variation_TwoAttributesDifferentTypes()
        {
            ChannelFactory<IContractBehaviorBasic_TwoAttributesDifferentTypes> cf = GetChannelFactory<IContractBehaviorBasic_TwoAttributesDifferentTypes>();
            try
            {
                cf.Open();
                string expected = "IContractBehavior:ClientContract.CustomContractBehaviorAttribute;IContractBehavior:ClientContract.OtherCustomContractBehaviorAttribute;";
                Assert.Equal(expected, BehaviorInvokedVerifier.ValidateClientInvokedBehavior(cf.Endpoint));
                IContractBehaviorBasic_TwoAttributesDifferentTypes clientProxy = cf.CreateChannel();
                string HelloStr = "TwoAttributesDifferentTypes";
                string returnStr = clientProxy.StringMethod(HelloStr);
                Assert.Equal(HelloStr, returnStr);
            }
            catch
            {
                throw;
            }
            finally
            {
                if (cf != null && cf.State == System.ServiceModel.CommunicationState.Opened)
                {
                    cf.Close();
                }
            }
        }

        private void Variation_MisplacedAttributes()
        {
            ChannelFactory<IContractBehaviorBasic_MisplacedAttributes> cf = GetChannelFactory<IContractBehaviorBasic_MisplacedAttributes>();
            try
            {
                IContractBehaviorBasic_MisplacedAttributes clientProxy = cf.CreateChannel();
                Assert.True(string.IsNullOrEmpty(BehaviorInvokedVerifier.ValidateClientInvokedBehavior(cf.Endpoint)));
                string HelloStr = "MisplacedAttributes";
                string returnStr = clientProxy.StringMethod(HelloStr);
                Assert.Equal(HelloStr, returnStr);
            }
            catch
            {
                throw;
            }
            finally
            {
                if (cf != null && cf.State == System.ServiceModel.CommunicationState.Opened)
                {
                    cf.Close();
                }
            }
        }

        private void Variation_CustomAttributesImplementsOther()
        {
            ChannelFactory<IContractBehaviorBasic_CustomAttributesImplementsOther> cf = GetChannelFactory<IContractBehaviorBasic_CustomAttributesImplementsOther>();
            try
            {
                cf.Open();
                string expected = "IContractBehavior:ClientContract.MyMultiFacetedBehaviorAttribute;";
                Assert.Equal(expected, BehaviorInvokedVerifier.ValidateClientInvokedBehavior(cf.Endpoint));
                IContractBehaviorBasic_CustomAttributesImplementsOther clientProxy = cf.CreateChannel();
                string HelloStr = "CustomAttributesImplementsOther";
                string returnStr = clientProxy.StringMethod(HelloStr);
                Assert.Equal(HelloStr, returnStr);
            }
            catch
            {
                throw;
            }
            finally
            {
                if (cf != null && cf.State == System.ServiceModel.CommunicationState.Opened)
                {
                    cf.Close();
                }
            }
        }

        private void Variation_ByHandImplementsOther()
        {
            ChannelFactory<IContractBehaviorBasic_ByHand> cf = GetChannelFactory<IContractBehaviorBasic_ByHand>();
            try
            {
                var theBehavior = new MyMultiFacetedBehaviorAttribute();
                cf.Endpoint.Contract.ContractBehaviors.Add(theBehavior);
                cf.Open();
                string expected = "IContractBehavior:ClientContract.MyMultiFacetedBehaviorAttribute;";
                Assert.Equal(expected, BehaviorInvokedVerifier.ValidateClientInvokedBehavior(cf.Endpoint));
                IContractBehaviorBasic_ByHand clientProxy = cf.CreateChannel();
                string HelloStr = "ByHandImplementsOther";
                string returnStr = clientProxy.StringMethod(HelloStr);
                Assert.Equal(HelloStr, returnStr);
            }
            catch
            {
                throw;
            }
            finally
            {
                if (cf != null && cf.State == System.ServiceModel.CommunicationState.Opened)
                {
                    cf.Close();
                }
            }
        }

        internal class Startup
        {
            public static string _method = "";
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
                        case "ByHand":
                        case "ByHand_UsingHiddenProperty":
                            builder.AddService<ContractBehaviorBasic_ByHand_Service>();
                            builder.AddServiceEndpoint<ContractBehaviorBasic_ByHand_Service, ServiceContract.IContractBehaviorBasic_ByHand>(new BasicHttpBinding(), "/BasicWcfService/ContractBehaviorService.svc");
                            builder.ConfigureServiceHostBase<ContractBehaviorBasic_ByHand_Service>(serviceHost =>
                            {
                                var cb = new ServiceContract.CustomContractBehaviorAttribute();
                                serviceHost.Description.Endpoints[0].Contract.ContractBehaviors.Add(cb);
                            });
                            break;
                        case "ByHandImplementsOther":
                            builder.AddService<ContractBehaviorBasic_ByHand_Service>();
                            builder.AddServiceEndpoint<ContractBehaviorBasic_ByHand_Service, ServiceContract.IContractBehaviorBasic_ByHand>(new BasicHttpBinding(), "/BasicWcfService/ContractBehaviorService.svc");
                            builder.ConfigureServiceHostBase<ContractBehaviorBasic_ByHand_Service>(serviceHost =>
                            {
                                var cb = new ServiceContract.MyMultiFacetedBehaviorAttribute();
                                serviceHost.Description.Endpoints[0].Contract.ContractBehaviors.Add(cb);
                            });
                            break;
                        case "CustomAttribute":
                            builder.AddService<ContractBehaviorBasic_CustomAttribute_Service>();
                            builder.AddServiceEndpoint<ContractBehaviorBasic_CustomAttribute_Service, ServiceContract.IContractBehaviorBasic_CustomAttribute>(new BasicHttpBinding(), "/BasicWcfService/ContractBehaviorService.svc");
                            break;
                        case "TwoAttributesDifferentTypes":
                            builder.AddService<ContractBehaviorBasic_TwoAttributesDifferentTypes_Service>();
                            builder.AddServiceEndpoint<ContractBehaviorBasic_TwoAttributesDifferentTypes_Service, ServiceContract.IContractBehaviorBasic_TwoAttributesDifferentTypes>(new BasicHttpBinding(), "/BasicWcfService/ContractBehaviorService.svc");
                            break;
                        case "TwoAttributesSameType":
                            builder.AddService<ContractBehaviorBasic_TwoAttributesSameType_Service>();
                            builder.AddServiceEndpoint<ContractBehaviorBasic_TwoAttributesSameType_Service, ServiceContract.IContractBehaviorBasic_TwoAttributesSameType>(new BasicHttpBinding(), "/BasicWcfService/ContractBehaviorService.svc");
                            break;
                        case "MisplacedAttributes":
                            builder.AddService<ContractBehaviorBasic_MisplacedAttributes_Service>();
                            builder.AddServiceEndpoint<ContractBehaviorBasic_MisplacedAttributes_Service, ServiceContract.IContractBehaviorBasic_MisplacedAttributes>(new BasicHttpBinding(), "/BasicWcfService/ContractBehaviorService.svc");
                            break;
                        case "CustomAttributesImplementsOther":
                            builder.AddService<ContractBehaviorBasic_CustomAttributesImplementsOther_Service>();
                            builder.AddServiceEndpoint<ContractBehaviorBasic_CustomAttributesImplementsOther_Service, ServiceContract.IContractBehaviorBasic_CustomAttributesImplementsOther>(new BasicHttpBinding(), "/BasicWcfService/ContractBehaviorService.svc");
                            break;
                        default:
                            throw new ApplicationException("Unsupported test method specified!");
                    }
                });
            }
        }
    }
}
