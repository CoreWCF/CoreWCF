using ClientContract;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Services;
using System;
using System.ServiceModel;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class OperationBehaviorTests
    {
        private ITestOutputHelper _output;

        public OperationBehaviorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("ByHand")]
        [InlineData("ByHand_UsingHiddenProperty")]
        [InlineData("StarAction")]
        [InlineData("CustomAttribute")]
        [InlineData("TwoAttributesDifferentTypes")]
        [InlineData("MisplacedAttributes")]
        [InlineData("CustomAttributesImplementsOther")]
        public void Variations(string method)
        {
            Startup._method = method;
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
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
                    case "StarAction":
                        Variation_StarAction();
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
                    default:
                        throw new ApplicationException("Unsupported ID specified!");
                }
            }
        }

        [Fact]
        public void TwoAttributesSameType_Test()
        {
            Startup._method = "TwoAttributesSameType";
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            Assert.Throws<ArgumentException>(()=> host.Start());
            host.Dispose();
        }

        //Variations
        public static ChannelFactory<T> GetChannelFactory<T>()
        {
            var httpBinding = ClientHelper.GetBufferedModeBinding();
            return new ChannelFactory<T>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/OperationBehaviorService.svc")));
        }

        private void Variation_ByHand(bool useHiddenProperty)
        {
            ChannelFactory<IOperationBehaviorBasic_ByHand> cf = GetChannelFactory<IOperationBehaviorBasic_ByHand>();
            try
            {
                string HelloStr = "ByHand";
                MyOperationBehaviorAttribute cb = new MyOperationBehaviorAttribute();
                if (useHiddenProperty)
                {
                    cf.Endpoint.Contract.Operations[0].Behaviors.Add(cb);
                    HelloStr = "ByHand_UsingHiddenProperty";
                }
                else
                {
                    cf.Endpoint.Contract.Operations[0].OperationBehaviors.Add(cb);
                }

                cf.Open();
                string expected = "IOperationBehavior:ClientContract.MyOperationBehaviorAttribute;";
                Assert.Equal(expected, BehaviorInvokedVerifier.ValidateClientInvokedBehavior(cf.Endpoint));
                IOperationBehaviorBasic_ByHand clientProxy = cf.CreateChannel();
                string returnStr = clientProxy.StringMethod(HelloStr);
                Assert.Equal(HelloStr, returnStr);
            }
            finally
            {
                if (cf != null)
                    cf.Close();
            }
        }

        private void Variation_StarAction()
        {
            ChannelFactory<IOperationBehaviorBasic_ForStarAction> cf = GetChannelFactory<IOperationBehaviorBasic_ForStarAction>();
            try
            {
                string HelloStr = "StarAction";
                cf.Open();
                IOperationBehaviorBasic_ForStarAction clientProxy = cf.CreateChannel();
                string returnStr = clientProxy.StringMethod(HelloStr);
                Assert.Equal(HelloStr, returnStr);
            }
            finally
            {
                if (cf != null)
                    cf.Close();
            }
        }

        private void Variation_CustomAttribute()
        {
            ChannelFactory<IOperationBehaviorBasic_CustomAttribute> cf = GetChannelFactory<IOperationBehaviorBasic_CustomAttribute>();
            try
            {
                cf.Open();
                string expected = "IOperationBehavior:ClientContract.MyOperationBehaviorAttribute;";
                Assert.Equal(expected, BehaviorInvokedVerifier.ValidateClientInvokedBehavior(cf.Endpoint));
                IOperationBehaviorBasic_CustomAttribute clientProxy = cf.CreateChannel();
                string HelloStr = "CustomAttribute";
                string returnStr = clientProxy.StringMethod(HelloStr);
                Assert.Equal(HelloStr, returnStr);
            }
            finally
            {
                if (cf != null)
                    cf.Close();
            }
        }

        private void Variation_TwoAttributesDifferentTypes()
        {
            ChannelFactory<IOperationBehaviorBasic_TwoAttributesDifferentTypes> cf = GetChannelFactory<IOperationBehaviorBasic_TwoAttributesDifferentTypes>();
            try
            {
                cf.Open();
                string expected = "IOperationBehavior:ClientContract.MyOperationBehaviorAttribute;IOperationBehavior:ClientContract.MyOtherOperationBehaviorAttribute;";
                Assert.Equal(expected, BehaviorInvokedVerifier.ValidateClientInvokedBehavior(cf.Endpoint));
                IOperationBehaviorBasic_TwoAttributesDifferentTypes clientProxy = cf.CreateChannel();
                string HelloStr = "TwoAttributesDifferentTypes";
                string returnStr = clientProxy.StringMethod(HelloStr);
                Assert.Equal(HelloStr, returnStr);
            }
            finally
            {
                if (cf != null)
                    cf.Close();
            }           
        }

        private void Variation_MisplacedAttributes()
        {
            ChannelFactory<IOperationBehaviorBasic_MisplacedAttributes> cf = GetChannelFactory<IOperationBehaviorBasic_MisplacedAttributes>();
            try
            {
                IOperationBehaviorBasic_MisplacedAttributes clientProxy = cf.CreateChannel();
                Assert.True(string.IsNullOrEmpty(BehaviorInvokedVerifier.ValidateClientInvokedBehavior(cf.Endpoint)));
                string HelloStr = "MisplacedAttributes";
                string returnStr = clientProxy.StringMethod(HelloStr);
                Assert.Equal(HelloStr, returnStr);
            }
            finally
            {
                if (cf != null)
                    cf.Close();
            }
        }

        private void Variation_CustomAttributesImplementsOther()
        {
            ChannelFactory<IOperationBehaviorBasic_CustomAttributesImplementsOther> cf = GetChannelFactory<IOperationBehaviorBasic_CustomAttributesImplementsOther>();
            try
            {
                cf.Open();
                string expected = "IOperationBehavior:ClientContract.MyMultiFacetedBehaviorAttribute;";
                Assert.Equal(expected, BehaviorInvokedVerifier.ValidateClientInvokedBehavior(cf.Endpoint));
                IOperationBehaviorBasic_CustomAttributesImplementsOther clientProxy = cf.CreateChannel();
                string HelloStr = "CustomAttributesImplementsOther";
                string returnStr = clientProxy.StringMethod(HelloStr);
                Assert.Equal(HelloStr, returnStr);
            }
            finally
            {
                if (cf != null)
                    cf.Close();
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
                    switch(_method)
                    {                     
                        case "ByHand":
                        case "ByHand_UsingHiddenProperty":
                            builder.AddService<OperationBehaviorBasic_ByHand_Service>();
                            builder.AddServiceEndpoint<OperationBehaviorBasic_ByHand_Service, ServiceContract.IOperationBehaviorBasic_ByHand>(new BasicHttpBinding(), "/BasicWcfService/OperationBehaviorService.svc");
                            builder.ConfigureServiceHostBase<OperationBehaviorBasic_ByHand_Service>(serviceHost =>
                            {
                                OperationDescription od = serviceHost.Description.Endpoints[0].Contract.Operations[0];
                                od.OperationBehaviors.Add(new ServiceContract.MyOperationBehaviorAttribute());
                            });
                            break;
                        case "StarAction":
                            builder.AddService<OperationBehaviorBasic_StarAction_Service>();
                            builder.AddServiceEndpoint<OperationBehaviorBasic_StarAction_Service, ServiceContract.IOperationBehaviorBasic_ForStarAction>(new BasicHttpBinding(), "/BasicWcfService/OperationBehaviorService.svc");
                            builder.ConfigureServiceHostBase<OperationBehaviorBasic_StarAction_Service>(serviceHost =>
                            {
                                //doesn't work, similar cause as https://github.com/CoreWCF/CoreWCF/issues/193
                                //related validation is skipped in the corresponding service
                                var behavior = new ServiceContract.CustomStarActionBehavior();
                                ContractDescription cd = serviceHost.Description.Endpoints[0].Contract;
                                cd.ContractBehaviors.Add(behavior);
                                OperationDescription od = cd.Operations[0];
                                od.OperationBehaviors.Add(behavior);
                            });
                            break;
                        case "CustomAttribute":
                            builder.AddService<OperationBehaviorBasic_CustomAttribute_Service>();
                            builder.AddServiceEndpoint<OperationBehaviorBasic_CustomAttribute_Service, ServiceContract.IOperationBehaviorBasic_CustomAttribute>(new BasicHttpBinding(), "/BasicWcfService/OperationBehaviorService.svc");
                            break;
                        case "TwoAttributesDifferentTypes":
                            builder.AddService<OperationBehaviorBasic_TwoAttributesDifferentTypes_Service>();
                            builder.AddServiceEndpoint<OperationBehaviorBasic_TwoAttributesDifferentTypes_Service, ServiceContract.IOperationBehaviorBasic_TwoAttributesDifferentTypes>(new BasicHttpBinding(), "/BasicWcfService/OperationBehaviorService.svc");
                            break;
                        case "TwoAttributesSameType":
                            builder.AddService<OperationBehaviorBasic_TwoAttributesSameType_Service>();
                            builder.AddServiceEndpoint<OperationBehaviorBasic_TwoAttributesSameType_Service, ServiceContract.IOperationBehaviorBasic_TwoAttributesSameType>(new BasicHttpBinding(), "/BasicWcfService/OperationBehaviorService.svc");
                            break;
                        case "MisplacedAttributes":
                            builder.AddService<OperationBehaviorBasic_MisplacedAttributes_Service>();
                            builder.AddServiceEndpoint<OperationBehaviorBasic_MisplacedAttributes_Service, ServiceContract.IOperationBehaviorBasic_MisplacedAttributes>(new BasicHttpBinding(), "/BasicWcfService/OperationBehaviorService.svc");
                            break;
                        case "CustomAttributesImplementsOther":
                            builder.AddService<OperationBehaviorBasic_CustomAttributesImplementsOther_Service>();
                            builder.AddServiceEndpoint<OperationBehaviorBasic_CustomAttributesImplementsOther_Service, ServiceContract.IOperationBehaviorBasic_CustomAttributesImplementsOther>(new BasicHttpBinding(), "/BasicWcfService/OperationBehaviorService.svc");
                            break;                           
                        default:
                            throw new ApplicationException("Unsupported test method specified!");
                    }
                });
            }
        }
    }    
}
