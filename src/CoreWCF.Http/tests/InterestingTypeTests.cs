using ClientContract;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class InterestingTypeTests
    {
        private ITestOutputHelper _output;

        public InterestingTypeTests(ITestOutputHelper output)
        {
            _output = output;
        }

#if NET472
        [Fact]
        public void MarshalledTypeTest()
        {
            var host = ServiceHelper.CreateWebHostBuilder<MarshalledTypeServiceStartup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<IMarshalledTypeService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/MarshalledTypeService.svc")));
                var channel = factory.CreateChannel();

                MarshalledType t = new MarshalledType(100);
                channel.TwoWayMethod1(t);
                Assert.Equal(100, t.GetData());

                t = channel.TwoWayMethod2();
                Assert.Equal(500, t.GetData());

                t = new MarshalledType(500);
                MarshalledType value = channel.TwoWayMethod3(t);
                Assert.Equal(1000, value.GetData());
            }
        }
#endif

        [Fact]
        public void TypedContractCollectionTest()
        {
            var host = ServiceHelper.CreateWebHostBuilder<TypedContractCollectionServiceStartup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ITypedContract_Collection>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/TypedContract_CollectionService.svc")));
                var channel = factory.CreateChannel();
                
                foreach (int numItems in  new int[] {1, 5, 15, 50 })
                {
                    //arraylist
                    var outgoingAL = new ArrayList();
                    for (int item = 0; item < numItems; item++)
                    {
                        outgoingAL.Add(item);
                    }

                    ArrayList responseAL = channel.ArrayListMethod(outgoingAL);
                    Assert.Equal(outgoingAL.Count, responseAL.Count);
                    for (int item = 0; item < responseAL.Count; item++)
                    {
                        if ((int)responseAL[item] != (int)outgoingAL[item])
                        {
                            Assert.True(false, "ArrayList item validation failed");
                        }
                    }

                    //Collection
                    var outgoingCL = new Collection<string>();
                    for (int item = 0; item < numItems; item++)
                    {
                        string s = string.Format("Item " + item);
                        outgoingCL.Add(s);
                    }

                    Collection<string> responseCL = channel.CollectionOfStringsMethod(outgoingCL);
                    Assert.Equal(outgoingCL.Count, responseCL.Count);
                    for (int item = 0; item < responseCL.Count; item++)
                    {
                        if (responseCL[item].CompareTo(outgoingCL[item]) != 0)
                        {
                            Assert.True(false, "Collection item validation failed");
                        }
                    }

                    //CollecitonBase
                    MyCollection outgoingCB = new MyCollection();
                    for (int item = 0; item < numItems; item++)
                    {
                        outgoingCB.Add((short)item);
                    }

                    MyCollection responseCB = channel.CollectionBaseMethod(outgoingCB);
                    Assert.Equal(outgoingCB.Count, responseCB.Count);
                    for (int item = 0; item < responseCB.Count; item++)
                    {
                        if (responseCB[item] != outgoingCB[item])
                        {
                            Assert.True(false, "MyCollection:CollectionBase item validation failed");
                        }
                    }
                }
            }
        }

        internal class MarshalledTypeServiceStartup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.MarshalledTypeService>();
                    builder.AddServiceEndpoint<Services.MarshalledTypeService, ServiceContract.IMarshalledTypeService>(new BasicHttpBinding(), "/BasicWcfService/MarshalledTypeService.svc");
                });
            }
        }

        internal class TypedContractCollectionServiceStartup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TypedContract_CollectionService>();
                    builder.AddServiceEndpoint<Services.TypedContract_CollectionService, ServiceContract.ITypedContract_Collection>(new BasicHttpBinding(), "/BasicWcfService/TypedContract_CollectionService.svc");
                });
            }
        }
    }
}
