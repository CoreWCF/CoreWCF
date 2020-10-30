using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClientContract;
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
    public class AsyncNetAdoptionTests
    {
        public ITestOutputHelper _output;

        public AsyncNetAdoptionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private const string bookName = "Great Expectations";
        private const string bookPublisher = "Penguin Publishers";

        [Fact]
        public void ChainedTaskTest()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                
                    Task<List<Book>> task = RunChainedService(typeof(ChainedServiceTask), TimeSpan.FromMilliseconds(5000));

                    StringBuilder sb = new StringBuilder();
                    foreach (Book book in task.Result)
                    {
                        sb.Append(book.Name);
                        sb.Append(",");
                        sb.Append(book.Publisher);
                        _output.WriteLine(book.Name);
                        if (book.ISBN == null || book.ISBN.Equals(Guid.Empty))
                        {
                            throw new Exception("Task should execute all the chained tasks before returning.");
                        }
                    }
                    string expectedResult = String.Format("{0},{1}", bookName, bookPublisher);
                    if (sb.ToString() != expectedResult)
                    {
                        throw new Exception(string.Format("Incorrect response, expected response {0}, actual response {1}", expectedResult, sb.ToString()));
                    }

                    _output.WriteLine("Variation passed");
            }
        }

        public Task<List<Book>> RunChainedService(Type serviceType, TimeSpan timeout)
        {
            var httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ISampleServiceTaskServerside>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/SampleServiceTask.svc")));
            ISampleServiceTaskServerside serviceProxy = factory.CreateChannel();

            Task<List<Book>> task = serviceProxy.SampleMethodAsync(bookName, bookPublisher);
            task.Wait(TimeSpan.FromMilliseconds(5000));
            return task;              
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
                    builder.AddService<SampleServiceTask>();
                    builder.AddServiceEndpoint<SampleServiceTask, ServiceContract.ISampleServiceTaskServerside>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/SampleServiceTask.svc");
                });
            }
        }
    }
}

