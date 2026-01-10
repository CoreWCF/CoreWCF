// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.ObjectModel;
using ClientContract;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class InterestingTypeTests
    {
        private readonly ITestOutputHelper _output;

        public InterestingTypeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TypedContractCollectionTest()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<TypedContractCollectionServiceStartup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ITypedContract_Collection>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/TypedContract_CollectionService.svc")));
                ITypedContract_Collection channel = factory.CreateChannel();

                foreach (int numItems in new int[] { 1, 5, 15, 50 })
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
                            Assert.Fail("ArrayList item validation failed");
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
                            Assert.Fail("Collection item validation failed");
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
                            Assert.Fail("MyCollection:CollectionBase item validation failed");
                        }
                    }
                }
            }
        }

        internal class TypedContractCollectionServiceStartup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
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
