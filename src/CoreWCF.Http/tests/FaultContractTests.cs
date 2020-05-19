using CoreWCF.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class FaultContractTests
    {
        private ITestOutputHelper _output;

        public FaultContractTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void FaultOnDiffContractAndOps()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestFaultOpContract>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/FaultOnDiffContractsAndOpsService.svc")));
                var channel = factory.CreateChannel();

                var factory2 = new System.ServiceModel.ChannelFactory<ClientContract.ITestFaultOpContractTypedClient>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/FaultOnDiffContractsAndOpsService.svc")));
                var channel2 = factory2.CreateChannel();

                //test variations count
                int count = 9;
                string faultToThrow = "Test fault thrown from a service";

                //Variation_TwoWayMethod
                try
                {
                    string s = channel.TwoWay_Method("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);
                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //Variation_TwoWayVoidMethod
                try
                {
                    channel.TwoWayVoid_Method("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);
                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //Variation_TwoWayStreamMethod
                try
                {
                    string testValue = "This is a string that will be converted to a byte array";
                    Stream inputStream = new MemoryStream();
                    byte[] bytes = Encoding.UTF8.GetBytes(testValue.ToCharArray());
                    foreach (byte b in bytes)
                        inputStream.WriteByte(b);
                    inputStream.Position = 0;

                    Stream outputStream = channel.TwoWayStream_Method(inputStream);
                    StreamReader sr = new StreamReader(outputStream, Encoding.UTF8);
                    sr.ReadToEnd();
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);
                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //Variation_TwoWayAsyncMethod
                try
                {
                    string response = channel.TwoWayAsync_MethodAsync("").GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);
                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //Variation_MessageContractMethod
                // Send the two way message
                var fmc = new ClientContract.FaultMsgContract();
                fmc.ID = 123;
                fmc.Name = "";
                try
                {
                    ClientContract.FaultMsgContract fmcResult = channel.MessageContract_Method(fmc); ;
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);
                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //Variation_UntypedMethod
                System.ServiceModel.Channels.MessageVersion mv = System.ServiceModel.Channels.MessageVersion.Soap11;
                System.ServiceModel.Channels.Message msgOut = System.ServiceModel.Channels.Message.CreateMessage(mv, "http://tempuri.org/ITestFaultOpContract/Untyped_Method");
                System.ServiceModel.Channels.Message msgIn = channel.Untyped_Method(msgOut);
                if (msgIn.IsFault)
                {
                    count--;
                    System.ServiceModel.Channels.MessageFault mf = System.ServiceModel.Channels.MessageFault.CreateFault(msgIn, int.MaxValue);
                    Assert.Equal(faultToThrow, mf.GetDetail<string>());
                }

                //Variation_UntypedMethodReturns
                msgOut = System.ServiceModel.Channels.Message.CreateMessage(mv, "http://tempuri.org/ITestFaultOpContract/Untyped_MethodReturns");
                msgIn = channel.Untyped_MethodReturns(msgOut);
                if (msgIn.IsFault)
                {
                    count--;
                    System.ServiceModel.Channels.MessageFault mf = System.ServiceModel.Channels.MessageFault.CreateFault(msgIn, int.MaxValue);
                    Assert.Equal(faultToThrow, mf.GetDetail<string>());
                }

                //Variation_TypedToUntypedMethod
                try
                {
                    channel2.Untyped_Method("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);
                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                //Variation_TypedToUntypedMethodReturns
                try
                {
                    channel2.Untyped_MethodReturns("");
                }
                catch (Exception e)
                {
                    count--;
                    Assert.NotNull(e);
                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }

                Assert.Equal(0, count);
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
                    builder.AddService<FaultOnDiffContractsAndOpsService>();
                    builder.AddServiceEndpoint<FaultOnDiffContractsAndOpsService, ServiceContract.ITestFaultOpContract>(new BasicHttpBinding(), "/BasicWcfService/FaultOnDiffContractsAndOpsService.svc");
                });
            }
        }
    }

    [ServiceBehavior]
    public class FaultOnDiffContractsAndOpsService : ServiceContract.ITestFaultOpContract
    {
        #region TwoWay_Methods
        public string TwoWay_Method(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public void TwoWayVoid_Method(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return;
        }
        #endregion

        #region TwoWayStream Method
        public Stream TwoWayStream_Method(Stream s)
        {
            if (s.ReadByte() != -1)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }
            else
            {
                StreamReader sr = new StreamReader(s, Encoding.UTF8);
                sr.ReadToEnd();
            }

            return new MemoryStream();
        }
        #endregion

        #region TwoWayAsync Method
        delegate string TwoWayMethodAsync(string s);

        public async System.Threading.Tasks.Task<string> TwoWayAsync_MethodAsync(string s)
        {
            TwoWayMethodAsync del = ProcessTwoWayAsync;
            var workTask = System.Threading.Tasks.Task.Run(() => del.Invoke(s));
            return await workTask;
        }

        // Worker
        public string ProcessTwoWayAsync(string s)
        {
            // This is where the incoming message processing is handled.
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return "Async call was valid";
        }
        #endregion

        #region MessageContract Methods
        public ServiceContract.FaultMsgContract MessageContract_Method(ServiceContract.FaultMsgContract fmc)
        {
            if (fmc.Name.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return fmc;
        }

        public string MessageContractParams_Method(int id, string name, DateTime dateTime)
        {
            if (name.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return $"{id} {name} {dateTime}";
        }
        #endregion

        #region Untyped Method
        public Message Untyped_Method(Message msgIn)
        {
            MessageVersion mv = OperationContext.Current.IncomingMessageHeaders.MessageVersion;
            string faultToThrow = "Test fault thrown from a service";
            if (msgIn != null)
            {
                throw new FaultException<string>(faultToThrow);
            }

            return Message.CreateMessage(mv, MessageFault.CreateFault(new FaultCode("Sender"), new FaultReason("Unspecified ServiceModel Fault"), "unspecified",
                new System.Runtime.Serialization.DataContractSerializer(typeof(string)), "", ""), "");
        }

        public Message Untyped_MethodReturns(Message msgIn)
        {
            MessageVersion mv = OperationContext.Current.IncomingMessageHeaders.MessageVersion;
            string faultToThrow = "Test fault thrown from a service";
            if (msgIn != null)
            {
                return Message.CreateMessage(mv, MessageFault.CreateFault(new FaultCode("Sender"), new FaultReason("Unspecified ServiceModel Fault"), faultToThrow,
                new System.Runtime.Serialization.DataContractSerializer(typeof(string)), "", ""), "");
            }

            return Message.CreateMessage(mv, MessageFault.CreateFault(new FaultCode("Sender"), new FaultReason("Unspecified ServiceModel Fault"), "unspeficied",
                new System.Runtime.Serialization.DataContractSerializer(typeof(string)), "", ""), "");
        }
        #endregion
    }
}
