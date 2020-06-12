using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ClientContract;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class TaskCollectionsTests
    {
        private ITestOutputHelper _output;

        public TaskCollectionsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void VariousCollections()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                ClientContract.ITaskCollectionsTest collectionsTest = null;
                System.ServiceModel.ChannelFactory<ClientContract.ITaskCollectionsTest> channelFactory = null;
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                channelFactory = new System.ServiceModel.ChannelFactory<ClientContract.ITaskCollectionsTest>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/TaskCollectionsTest.svc")));
                collectionsTest = channelFactory.CreateChannel();
                
                Task[] array;
                array = new Task[5];
                array[0] = collectionsTest.GetDictionary();
                array[1] = collectionsTest.GetList();
                array[2] = collectionsTest.GetSet();
                array[3] = collectionsTest.GetQueue();
                array[4] = collectionsTest.GetStack();
                Task.WaitAll(array,TimeSpan.FromSeconds(30));

                bool flag = true;
                Task<Dictionary<string, int>> task = array[0] as Task<Dictionary<string, int>>;
                Assert.True(task.Result.ContainsKey("Sam"));
                Assert.True(task.Result.ContainsKey("Sara"));
                if (!task.Result.ContainsKey("Sam") || !task.Result.ContainsKey("Sara"))
                {
                    flag = false;
                    _output.WriteLine("Expected collection to contain Sara and Sam.");
                    _output.WriteLine("Actual Result");
                    foreach (string text in task.Result.Keys)
                    {
                        _output.WriteLine(text);
                    }
                }

                Task<LinkedList<int>> task2 = array[1] as Task<LinkedList<int>>;
                Assert.Contains(100, task2.Result);
                Assert.Contains(40, task2.Result);
                if (!task2.Result.Contains(100) || !task2.Result.Contains(40))
                {
                    flag = false;
                    _output.WriteLine("Expected collection to contain 100 and 40.");
                    _output.WriteLine("Actual Result");
                    foreach (int num in task2.Result)
                    {
                        _output.WriteLine(num.ToString());
                    }
                }

                Task<HashSet<Book>> task3 = array[2] as Task<HashSet<Book>>;
                foreach (Book book in task3.Result)
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
                Assert.True(task4.Result.Contains("Panasonic"));
                Assert.True(task4.Result.Contains("Kodak"));
                if (!task4.Result.Contains("Panasonic") || !task4.Result.Contains("Kodak"))
                {
                    flag = false;
                    _output.WriteLine("Expected collection to contain Panasonic and Kodak.");
                    _output.WriteLine("Actual Result");
                    foreach (string text2 in task4.Result)
                    {
                        _output.WriteLine(text2);
                    }
                }

                Task<Stack<byte>> task5 = array[4] as Task<Stack<byte>>;
                Assert.True(task5.Result.Contains(45));
                Assert.True(task5.Result.Contains(10));
                if (!task5.Result.Contains(45) || !task5.Result.Contains(10))
                {
                    flag = false;
                    _output.WriteLine("Expected collection to contain 45 and 10.");
                    _output.WriteLine("Actual Result");
                    foreach (byte b in task5.Result)
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

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
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
