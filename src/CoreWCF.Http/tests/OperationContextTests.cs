using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class OperationContextTests
    {
        private ITestOutputHelper _output;
        public OperationContextTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void SingleAsyncInsideOCSandAwait()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();

                Scenarios.SingleAsyncTaskInsideOCSandAwait(null, _output);
                WatiForSignal(Scenarios.manualResetEvent1, new TimeSpan(0, 3, 0));
            }
        }

        private void WatiForSignal(ManualResetEvent mre, TimeSpan timeSpan)
        {
            if (!mre.WaitOne(timeSpan))
            {
                throw new Exception($"Test timed out as the manual reset event didn't receive signal within the allocated {timeSpan.Milliseconds} milliseconds");
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
                    builder.AddService<Services.MathService>();
                    builder.AddServiceEndpoint<Services.MathService, IMathService>(new CoreWCF.BasicHttpBinding() { SendTimeout = TimeSpan.FromMinutes(4), ReceiveTimeout = TimeSpan.FromMinutes(4) }, "/BasicWcfService/basichttp.svc");
                });
            }
        }
    }

    internal static class Scenarios
    {
        public static ManualResetEvent manualResetEvent1 = new ManualResetEvent(false);
        public static ManualResetEvent manualResetEvent2 = new ManualResetEvent(false);

        public async static void SingleAsyncTaskInsideOCSandAwait(object obj, ITestOutputHelper output)
        {
            Helpers.GeneratedClient.MathServiceClient mathClient = null;

            try
            {
                string url = "http://localhost:8080/BasicWcfService/basichttp.svc";
                mathClient = new Helpers.GeneratedClient.MathServiceClient(ClientHelper.GetBufferedModeBinding(), new System.ServiceModel.EndpointAddress(url));

                Task<Tuple<System.ServiceModel.OperationContext, int>> resultTask;
                Tuple<System.ServiceModel.OperationContext, int> result;
                using (System.ServiceModel.OperationContextScope ocs = new System.ServiceModel.OperationContextScope(mathClient.InnerChannel))
                {
                    System.ServiceModel.OperationContext oc = System.ServiceModel.OperationContext.Current;
                    OperationContextUtility.AddMessageHeader(oc);
                    resultTask = mathClient.AddAsync(10, 20).ContinueWith((tsk) => new Tuple<System.ServiceModel.OperationContext, int>(oc, tsk.Result));
                }

                result = await resultTask;

                Assert.True(result.Item2 == 30, $"Test failed: Expected 30, but received {result.Item2}");

                OperationContextUtility.VerifyResponseHavingMessageHeader(result.Item1, Helpers.Constants.MessageHeaderNameMathServiceAddResponse, Helpers.Constants.MessageHeaderNamespace);
                OperationContextUtility.VerifyMessageProperties(result.Item1);
            }
            finally
            {
                mathClient.Close();
                //signal the mre
                Scenarios.manualResetEvent1.Set();
            }
        }
    }
}
