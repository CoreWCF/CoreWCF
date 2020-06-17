using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using System;
using System.Linq;
using System.ServiceModel.Description;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class ServiceWithContractInheritanceTests
    {
        private ITestOutputHelper _output;

        public ServiceWithContractInheritanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData(typeof(Services.ServiceWithSCExtendingFromServiceWithSC), typeof(Services.ServiceWithSC))]
        [InlineData(typeof(Services.ServiceWithSCDerivingFromNonSCExtendingSC), typeof(ServiceContract.SCInterface_1138907))]
        [InlineData(typeof(Services.ServiceWithSCDerivingFromSC), typeof(ServiceContract.SCInterface_1138907))]
        public void ServiceWithContractNegative(Type service, Type interf)
        {
            string expectResults = string.Format("The service class of type {0} both defines a ServiceContract and inherits a ServiceContract from type {1}. Contract inheritance can only be used among interface types.  If a class is marked with ServiceContractAttribute, it must be the only type in the hierarchy with ServiceContractAttribute.  Consider moving the ServiceContractAttribute on type {1} to a separate interface that type {1} implements.", service, interf);
            Startup._service = service;
            Startup._interface = interf;

            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            try
            {
                using (host)
                {
                    host.Start();
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine(ex.ToString());
                string actual = ex.Message.ToString();
                Assert.Equal(expectResults, actual);
            }
        }

        [Theory]
        [InlineData("AClient", "ABService")]
        [InlineData("BClient", "ABService")]
        [InlineData("ABClient", "ABService")]
        [InlineData("ABClient", "BService")]
        public void EndpointsWithContractInheritance(string clientType, string serviceType)
        {
            StartupEndpoints.ServiceType = serviceType;
            StartupEndpoints.ClientType = clientType;

            var host = ServiceHelper.CreateWebHostBuilder<StartupEndpoints>(_output).Build();
            using (host)
            {
                host.Start();

                switch (clientType.ToLower())
                {
                    case "abclient":
                        ClientContract.SCInterfaceAB_1144850 abProxy = GetProxy<ClientContract.SCInterfaceAB_1144850>();
                        if (serviceType.ToLower().Equals("aservice"))
                            Assert.Equal("Hello", abProxy.StringMethodA("Hello"));
                        else if (serviceType.ToLower().Equals("bservice"))
                            Assert.Equal("Hello", abProxy.StringMethodB("Hello"));
                        else
                            _output.WriteLine("This ClientType and ServiceType combination is not supported");
                        break;
                    case "aclient":
                        ClientContract.SCInterfaceA_1144850 aProxy = GetProxy<ClientContract.SCInterfaceA_1144850>();
                        Assert.Equal("Hello", aProxy.StringMethodA("Hello"));
                        break;
                    case "bclient":
                        ClientContract.SCInterfaceB_1144850 bProxy = GetProxy<ClientContract.SCInterfaceB_1144850>();
                        Assert.Equal("Hello", bProxy.StringMethodB("Hello"));
                        break;
                    default:
                        throw new ApplicationException("Unsupported ClientType");
                }
            }
        }

        [Theory]
        //[InlineData("DerivedOneWay", "One Way Method")]
        [InlineData("DerivedStringMethod", "Send String to fro on Derived")]
        [InlineData("DerivedReNameMethod", "Method conflicts with Base implementation")]
        [InlineData("BaseTwoWayMethod", "Call Two way voids on Base")]
        // [InlineData("BaseDataContractMethod", "Send Data Contract on Base")]
        [InlineData("BaseReNameMethod", "Method conflicts with Derived implementation")]
        [InlineData("DerivedCallingBaseTwoWayMethod", "Call Two way voids on Base")]
        //[InlineData("DerivedCallingBaseDataContractMethod", "Send Data Contract on Base")]
        [InlineData("DerivedCallingBaseReNameMethod", "Method conflicts with Derived implementation")]
        public void SanityAParentB_857419_Service_Both(string method, string clientString)
        {
            _output.WriteLine("Entered SanityAParentB_857419_Client.Run");
            // Client type: OneWay and TwoWay
            string result = null;
            StartupSanityAParentB._method = method;
            var host = ServiceHelper.CreateWebHostBuilder<StartupSanityAParentB>(_output).Build();

            using (host)
            {
                host.Start();
                switch (method)
                {
                    case "DerivedOneWay":
                        {
                            result = this.Variation_Service_DerivedOneWay(clientString);
                            Assert.Equal(clientString, result);
                        }
                        break;
                    case "DerivedStringMethod":
                        {
                            result = this.Variation_Service_DerivedStringMethod(clientString);
                            Assert.Equal(clientString, result);
                        }
                        break;
                    case "DerivedReNameMethod":
                        {
                            result = this.Variation_Service_DerivedReNameMethod();
                            Assert.Equal("derived", result);
                        }
                        break;
                    case "BaseTwoWayMethod":
                        {
                            result = this.Variation_Service_BaseTwoWayMethod(clientString);
                            Assert.Equal(clientString, result);
                        }
                        break;
                    case "BaseDataContractMethod":
                        {
                            result = this.Variation_Service_BaseDataContractMethod(clientString);
                            Assert.Equal(clientString, result);
                        }
                        break;
                    case "BaseReNameMethod":
                        {
                            result = this.Variation_Service_BaseReNameMethod();
                            Assert.Equal("base", result);
                        }
                        break;
                    case "DerivedCallingBaseTwoWayMethod":
                        {
                            result = this.Variation_Service_DerivedCallingBaseTwoWayMethod(clientString);
                            Assert.Equal(clientString, result);
                        }
                        break;
                    case "DerivedCallingBaseDataContractMethod":
                        {
                            result = this.Variation_Service_DerivedCallingBaseDataContractMethod(clientString);
                            Assert.Equal(clientString, result);
                        }
                        break;
                    case "DerivedCallingBaseReNameMethod":
                        {
                            result = this.Variation_Service_DerivedCallingBaseReNameMethod();
                            Assert.Equal("Derived", result);
                        }
                        break;
                    default:
                        {
                            throw new Exception("Unknown ID : " + method);
                        }
                }
            }
        }

        T GetProxy<T>()
        {
            var httpBinding = ClientHelper.GetBufferedModeBinding();
            System.ServiceModel.ChannelFactory<T> channelFactory = new System.ServiceModel.ChannelFactory<T>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));

            T proxy = channelFactory.CreateChannel();
            return proxy;
        }

        #region Variation       

        private string Variation_Service_DerivedOneWay(string clientString)
        {
            // Create the proxy
            ClientContract.ISanityAParentB_857419_ContractDerived clientProxy = GetProxy<ClientContract.ISanityAParentB_857419_ContractDerived>();
            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_DerivedOneWay]");
            clientProxy.OneWayMethod(clientString);
            string response = clientString;
            _output.WriteLine($"Testing [Variation_Service_DerivedOneWay] returned <{response}>");
            return response;
        }

        private string Variation_Service_DerivedStringMethod(string clientString)
        {
            // Create the proxy
            var clientProxy = this.GetProxy<ClientContract.ISanityAParentB_857419_ContractDerived>();
            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_DerivedStringMethod]");

            string response = clientProxy.StringMethod(clientString);
            _output.WriteLine($"Testing [Variation_Service_DerivedStringMethod] returned <{response}>");
            return response;
        }

        private string Variation_Service_DerivedReNameMethod()
        {
            // Create the proxy          
            var clientProxy = this.GetProxy<ClientContract.ISanityAParentB_857419_ContractDerived>();
            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_DerivedReNameMethod]");
            string response = clientProxy.Method("derived");
            _output.WriteLine($"Testing [Variation_Service_DerivedReNameMethod] returned <{response}>");
            return response;
        }

        private string Variation_Service_BaseTwoWayMethod(string clientString)
        {
            //// Create the proxy
            var clientProxy = this.GetProxy<ClientContract.ISanityAParentB_857419_ContractBase>();
            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_BaseTwoWayMethod]");
            string response = clientProxy.TwoWayMethod(clientString);
            _output.WriteLine($"Testing [Variation_Service_BaseTwoWayMethod] returned <{response}>");
            return response;
        }

        private string Variation_Service_BaseDataContractMethod(string clientString)
        {
            // Create the proxy
            var httpBinding = ClientHelper.GetBufferedModeBinding();
            System.ServiceModel.ChannelFactory<ClientContract.ISanityAParentB_857419_ContractBase> channelFactory = new System.ServiceModel.ChannelFactory<ClientContract.ISanityAParentB_857419_ContractBase>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));


            // var clientProxy = this.GetProxy<ClientContract.ISanityAParentB_857419_ContractBase>();
            foreach (var operation in channelFactory.Endpoint.Contract.Operations)
            {
                DataContractSerializerOperationBehavior behavior =
                         operation.OperationBehaviors.FirstOrDefault(
                             x => x.GetType() == typeof(DataContractSerializerOperationBehavior)) as DataContractSerializerOperationBehavior;
                behavior.DataContractResolver = new ManagerDataContractResolver<ClientContract.MyBaseDataType>();
            }
            var clientProxy = channelFactory.CreateChannel();

            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_BaseTwoWayMethod]");
            var dataObj = new ClientContract.MyBaseDataType();
            dataObj.data = clientString;

            var result = (ClientContract.MyBaseDataType)clientProxy.DataContractMethod(dataObj);
            string response = result.data;

            _output.WriteLine($"Testing [Variation_Service_BaseTwoWayMethod] returned <{response}>");
            return response;
        }

        private string Variation_Service_BaseReNameMethod()
        {
            // Create the proxy
            var clientProxy = this.GetProxy<ClientContract.ISanityAParentB_857419_ContractBase>();
            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_BaseReNameMethod]");
            string response = clientProxy.Method("base");
            _output.WriteLine($"Testing [Variation_Service_BaseReNameMethod] returned <{response}>");
            return response;
        }

        private string Variation_Service_DerivedCallingBaseTwoWayMethod(string clientString)
        {
            // Create the proxy
            var clientProxy = this.GetProxy<ClientContract.ISanityAParentB_857419_ContractDerived>();
            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_DerivedTwoWayMethod]");
            clientProxy.TwoWayMethod(clientString);
            string response = clientString;
            _output.WriteLine($"Testing [Variation_Service_DerivedTwoWayMethod] returned <{response}>");
            return response;
        }

        private string Variation_Service_DerivedCallingBaseDataContractMethod(string clientString)
        {
            // Create the proxy           
            var clientProxy = this.GetProxy<ClientContract.ISanityAParentB_857419_ContractDerived>();

            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_DerivedTwoWayMethod]");

            //Form the DataContract input
            var dataObj = new ClientContract.MyBaseDataType();
            dataObj.data = clientString;

            var result = (MyBaseDataType)clientProxy.DataContractMethod(dataObj);
            string response = result.data;

            _output.WriteLine($"Testing [Variation_Service_DerivedTwoWayMethod] returned <{response}>");
            return response;
        }

        private string Variation_Service_DerivedCallingBaseReNameMethod()
        {
            // Create the proxy
            var clientProxy = this.GetProxy<ClientContract.ISanityAParentB_857419_ContractDerived>();

            // Send the two way message
            _output.WriteLine("Testing [Variation_Service_DerivedReNameMethod]");
            string response = clientProxy.Method("Derived");
            _output.WriteLine($"Testing [Variation_Service_DerivedReNameMethod] returned <{response}>");
            return response;
        }

        #endregion

        internal class Startup
        {
            public static Type _service, _interface;

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    if (_service == typeof(Services.ServiceWithSCExtendingFromServiceWithSC))
                    {
                        builder.AddService<Services.ServiceWithSCExtendingFromServiceWithSC>();
                        builder.AddServiceEndpoint<Services.ServiceWithSCExtendingFromServiceWithSC>(_service, new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                    }

                    if (_service == typeof(Services.ServiceWithSCDerivingFromNonSCExtendingSC))
                    {
                        builder.AddService<Services.ServiceWithSCDerivingFromNonSCExtendingSC>();
                        builder.AddServiceEndpoint<Services.ServiceWithSCDerivingFromNonSCExtendingSC, ServiceContract.NonSCExtendingSC_1138907>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                    }

                    if (_service == typeof(Services.ServiceWithSCDerivingFromSC))
                    {
                        builder.AddService<Services.ServiceWithSCDerivingFromSC>();
                        builder.AddServiceEndpoint<Services.ServiceWithSCDerivingFromSC, ServiceContract.SCInterface_1138907>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                    }
                });
            }
        }

        internal class StartupEndpoints
        {
            public static string ServiceType;
            public static string ClientType;

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                Type serviceContractType = null;
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.Service_1144850>();

                    switch (ServiceType.ToLower())
                    {
                        case "abservice":
                            serviceContractType = typeof(SCInterfaceAB_1144850);
                            builder.AddServiceEndpoint<Services.Service_1144850, SCInterfaceAB_1144850>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                            break;
                        case "aservice":
                            serviceContractType = typeof(SCInterfaceA_1144850);
                            builder.AddServiceEndpoint<Services.Service_1144850, SCInterfaceA_1144850>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                            break;
                        case "bservice":
                            serviceContractType = typeof(SCInterfaceB_1144850);
                            builder.AddServiceEndpoint<Services.Service_1144850, SCInterfaceB_1144850>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                            break;
                        default:
                            throw new ApplicationException("Unsupported ServiceType in tef");
                    }
                });
            }
        }

        internal class StartupSanityAParentB
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
                    builder.AddService<Services.SanityAParentB_857419_Service_Both>();

                    switch (_method)
                    {
                        case "DerivedOneWay":
                        case "DerivedStringMethod":
                        case "DerivedReNameMethod":
                        case "DerivedCallingBaseTwoWayMethod":
                        case "DerivedCallingBaseDataContractMethod":
                        case "DerivedCallingBaseReNameMethod":
                            builder.AddServiceEndpoint<Services.SanityAParentB_857419_Service_Both, ISanityAParentB_857419_ContractDerived>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                            break;
                        case "BaseTwoWayMethod":
                        case "BaseReNameMethod":
                        case "BaseDataContractMethod":
                            builder.AddServiceEndpoint<Services.SanityAParentB_857419_Service_Both, ISanityAParentB_857419_ContractBase>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                            break;
                        default:
                            throw new ApplicationException("Unsupported ServiceType");
                    }
                });
            }
        }
    }
}
