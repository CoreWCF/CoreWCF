// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ClientContract;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class TaskCollectionsTests
    {
        private readonly ITestOutputHelper _output;

        public TaskCollectionsTests(ITestOutputHelper output)
        {
            // No-op on .NET Core but necessary to complete concurrect requests on NetFx
            System.Net.ServicePointManager.DefaultConnectionLimit = 50;
            _output = output;
        }

        [Fact]
        public async Task VariousCollections()
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                ClientContract.ITaskCollectionsTest collectionsTest = null;
                System.ServiceModel.ChannelFactory<ClientContract.ITaskCollectionsTest> channelFactory = null;
                await host.StartAsync();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                channelFactory = new System.ServiceModel.ChannelFactory<ClientContract.ITaskCollectionsTest>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/TaskCollectionsTest.svc")));
                collectionsTest = channelFactory.CreateChannel();

                Task[] array;
                array = new Task[5];
                array[0] = collectionsTest.GetDictionary();
                array[1] = collectionsTest.GetList();
                array[2] = collectionsTest.GetSet();
                array[3] = collectionsTest.GetQueue();
                array[4] = collectionsTest.GetStack();
                await Task.WhenAll(array);

                bool flag = true;
                Task<Dictionary<string, int>> task = array[0] as Task<Dictionary<string, int>>;
                var dictionary = await task;
                Assert.True(dictionary.ContainsKey("Sam"));
                Assert.True(dictionary.ContainsKey("Sara"));
                if (!dictionary.ContainsKey("Sam") || !dictionary.ContainsKey("Sara"))
                {
                    flag = false;
                    _output.WriteLine("Expected collection to contain Sara and Sam.");
                    _output.WriteLine("Actual Result");
                    foreach (string text in dictionary.Keys)
                    {
                        _output.WriteLine(text);
                    }
                }

                Task<LinkedList<int>> task2 = array[1] as Task<LinkedList<int>>;
                var linkedList = await task2;
                Assert.Contains(100, linkedList);
                Assert.Contains(40, linkedList);
                if (!linkedList.Contains(100) || !linkedList.Contains(40))
                {
                    flag = false;
                    _output.WriteLine("Expected collection to contain 100 and 40.");
                    _output.WriteLine("Actual Result");
                    foreach (int num in linkedList)
                    {
                        _output.WriteLine(num.ToString());
                    }
                }

                Task<HashSet<Book>> task3 = array[2] as Task<HashSet<Book>>;
                var hashSet = await task3;
                foreach (Book book in hashSet)
                {
                    Assert.False(!book.Name.Equals("Whoa") && !book.Name.Equals("Dipper"));
                    if (!book.Name.Equals("Whoa") && !book.Name.Equals("Dipper"))
                    {
                        _output.WriteLine("Expected collection to contain Whoa and Dipper.");
                        _output.WriteLine(string.Format("Actual Result {0}", book.Name));
                        flag = false;
                    }
                }

                Task<Queue<string>> task4 = array[3] as Task<Queue<string>>;
                var queue = await task4;
                Assert.True(queue.Contains("Panasonic"));
                Assert.True(queue.Contains("Kodak"));
                if (!queue.Contains("Panasonic") || !queue.Contains("Kodak"))
                {
                    flag = false;
                    _output.WriteLine("Expected collection to contain Panasonic and Kodak.");
                    _output.WriteLine("Actual Result");
                    foreach (string text2 in queue)
                    {
                        _output.WriteLine(text2);
                    }
                }

                Task<Stack<byte>> task5 = array[4] as Task<Stack<byte>>;
                var stack = await task5;
                Assert.True(stack.Contains(45));
                Assert.True(stack.Contains(10));
                if (!stack.Contains(45) || !stack.Contains(10))
                {
                    flag = false;
                    _output.WriteLine("Expected collection to contain 45 and 10.");
                    _output.WriteLine("Actual Result");
                    foreach (byte b in stack)
                    {
                        _output.WriteLine(b.ToString());
                    }
                }
                if (!flag)
                {
                    throw new Exception("Test Failed");
                }

            }
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<TaskCollectionsTest>();
                    builder.AddServiceEndpoint<TaskCollectionsTest, ServiceContract.ITaskCollectionsTest>(new BasicHttpBinding(), "/BasicWcfService/TaskCollectionsTest.svc");
                });
            }
        }
    }
}
