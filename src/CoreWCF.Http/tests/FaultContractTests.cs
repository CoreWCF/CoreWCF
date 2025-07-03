// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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
    public class FaultContractTests
    {
        private readonly ITestOutputHelper _output;

        public FaultContractTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task FaultOnDiffContractAndOps()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestFaultOpContract>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/FaultOnDiffContractsAndOpsService.svc")));
                ClientContract.ITestFaultOpContract channel = factory.CreateChannel();

                var factory2 = new System.ServiceModel.ChannelFactory<ClientContract.ITestFaultOpContractTypedClient>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/FaultOnDiffContractsAndOpsService.svc")));
                ClientContract.ITestFaultOpContractTypedClient channel2 = factory2.CreateChannel();

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
                    {
                        inputStream.WriteByte(b);
                    }

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
                    string response = await channel.TwoWayAsync_MethodAsync("");
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
                var fmc = new ClientContract.FaultMsgContract
                {
                    ID = 123,
                    Name = ""
                };
                try
                {
                    ClientContract.FaultMsgContract fmcResult = channel.MessageContract_Method(fmc);
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

        [Theory]
        [InlineData("somefault")]
        [InlineData("outerfault")]
        [InlineData("complexfault")]
        public void DatacontractFaults(string f)
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestDataContractFault>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/DatacontractFaults.svc")));
                ClientContract.ITestDataContractFault channel = factory.CreateChannel();

                var factory2 = new System.ServiceModel.ChannelFactory<ClientContract.ITestDataContractFaultTypedClient>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/DatacontractFaults.svc")));
                ClientContract.ITestDataContractFaultTypedClient channel2 = factory2.CreateChannel();

                //test variations
                int count = 9;
                try
                {
                    channel.TwoWayVoid_Method(f);
                }
                catch (Exception e)
                {
                    count--;
                    FaultExceptionValidation(f, e);
                }

                try
                {
                    string s = channel.TwoWay_Method(f);
                }
                catch (Exception e)
                {
                    count--;
                    FaultExceptionValidation(f, e);
                }

                try
                {
                    Stream inputStream = new MemoryStream();
                    byte[] bytes = Encoding.UTF8.GetBytes(f.ToCharArray());
                    foreach (byte b in bytes)
                    {
                        inputStream.WriteByte(b);
                    }

                    inputStream.Position = 0;
                    Stream outputStream = channel.TwoWayStream_Method(inputStream);
                    StreamReader sr = new StreamReader(outputStream, Encoding.UTF8);
                    string outputText = sr.ReadToEnd();
                    Assert.Fail($"Error, Received Input: {outputText}");
                }
                catch (Exception e)
                {
                    count--;
                    FaultExceptionValidation(f, e);
                }

                try
                {
                    string response = await channel.TwoWayAsync_Method(f);
                    Assert.Fail($"Error, Client received: {response}");
                }
                catch (Exception e)
                {
                    count--;
                    FaultExceptionValidation(f, e);
                }

                try
                {
                    var fmc = new ClientContract.FaultMsgContract
                    {
                        ID = 123,
                        Name = f
                    };
                    ClientContract.FaultMsgContract fmcResult = channel.MessageContract_Method(fmc);
                    Assert.Fail($"Error, Client received: {fmcResult.Name}");
                }
                catch (Exception e)
                {
                    count--;
                    FaultExceptionValidation(f, e);
                }

                System.ServiceModel.Channels.Message msgOut = System.ServiceModel.Channels.Message.CreateMessage(System.ServiceModel.Channels.MessageVersion.Soap11, "http://tempuri.org/ITestDataContractFault/Untyped_Method", f);
                System.ServiceModel.Channels.Message msgIn = channel.Untyped_Method(msgOut);
                if (msgIn.IsFault)
                {
                    System.ServiceModel.Channels.MessageFault mf = System.ServiceModel.Channels.MessageFault.CreateFault(msgIn, int.MaxValue);
                    switch (f.ToLower())
                    {
                        case "somefault":
                            count--;
                            ClientContract.SomeFault sf = mf.GetDetail<ClientContract.SomeFault>();
                            Assert.Equal(123456789, sf.ID);
                            Assert.Equal("SomeFault", sf.message);
                            break;
                        case "outerfault":
                            count--;
                            ClientContract.OuterFault of = mf.GetDetail<ClientContract.OuterFault>();
                            sf = of.InnerFault;
                            Assert.Equal(123456789, sf.ID);
                            Assert.Equal("SomeFault as innerfault", sf.message);
                            break;
                        case "complexfault":
                            count--;
                            ClientContract.ComplexFault cf = mf.GetDetail<ClientContract.ComplexFault>();
                            string exp = "50:This is a test error string for fault tests.:123456789:SomeFault in complexfault:0123456789101112131415161718192021222324252627282930313233343536373839404142434445464748495051525354555657585960616263646566676869707172737475767778798081828384858687888990919293949596979899100101102103104105106107108109110111112113114115116117118119120121122123124125126127:2147483647-214748364801-150-50:123456789:SomeFault in complexfaultnull234:Second somefault in complexfault";
                            Assert.Equal(exp, ComplexFaultToString(cf));
                            break;
                        default:
                            break;
                    }
                }

                msgOut = System.ServiceModel.Channels.Message.CreateMessage(System.ServiceModel.Channels.MessageVersion.Soap11, "http://tempuri.org/ITestDataContractFault/Untyped_MethodReturns", f);
                msgIn = channel.Untyped_MethodReturns(msgOut);
                if (msgIn.IsFault)
                {
                    System.ServiceModel.Channels.MessageFault mf = System.ServiceModel.Channels.MessageFault.CreateFault(msgIn, int.MaxValue);
                    switch (f)
                    {
                        case "somefault":
                            count--;
                            ClientContract.SomeFault sf = mf.GetDetail<ClientContract.SomeFault>();
                            Assert.Equal(123456789, sf.ID);
                            Assert.Equal("SomeFault", sf.message);
                            break;
                        case "outerfault":
                            count--;
                            ClientContract.OuterFault of = mf.GetDetail<ClientContract.OuterFault>();
                            sf = of.InnerFault;
                            Assert.Equal(123456789, sf.ID);
                            Assert.Equal("SomeFault as innerfault", sf.message);
                            break;
                        case "complexfault":
                            count--;
                            ClientContract.ComplexFault cf = mf.GetDetail<ClientContract.ComplexFault>();
                            string exp = "50:This is a test error string for fault tests.:123456789:SomeFault in complexfault:0123456789101112131415161718192021222324252627282930313233343536373839404142434445464748495051525354555657585960616263646566676869707172737475767778798081828384858687888990919293949596979899100101102103104105106107108109110111112113114115116117118119120121122123124125126127:2147483647-214748364801-150-50:123456789:SomeFault in complexfaultnull234:Second somefault in complexfault";
                            Assert.Equal(exp, ComplexFaultToString(cf));
                            break;
                        default:
                            break;
                    }
                }

                try
                {
                    string response = channel2.Untyped_Method(f);
                }
                catch (Exception e)
                {
                    count--;
                    FaultExceptionValidation(f, e);
                }

                try
                {
                    string response = channel2.Untyped_MethodReturns(f);
                }
                catch (Exception e)
                {
                    count--;
                    FaultExceptionValidation(f, e);
                }

                Assert.Equal(0, count);
            }
        }

        private void FaultExceptionValidation(string faultType, Exception e)
        {
            switch (faultType)
            {
                case "somefault":
                    Assert.NotNull(e);
                    Assert.IsType<System.ServiceModel.FaultException<ClientContract.SomeFault>>(e);
                    var ex = (System.ServiceModel.FaultException<ClientContract.SomeFault>)e;
                    ClientContract.SomeFault sf = ex.Detail;
                    Assert.Equal(123456789, sf.ID);
                    Assert.Equal("SomeFault", sf.message);
                    break;
                case "outerfault":
                    Assert.NotNull(e);
                    Assert.IsType<System.ServiceModel.FaultException<ClientContract.OuterFault>>(e);
                    var oex = (System.ServiceModel.FaultException<ClientContract.OuterFault>)e;
                    ClientContract.OuterFault of = oex.Detail;
                    sf = of.InnerFault;
                    Assert.Equal(123456789, sf.ID);
                    Assert.Equal("SomeFault as innerfault", sf.message);
                    break;
                case "complexfault":
                    string exp = "50:This is a test error string for fault tests.:123456789:SomeFault in complexfault:0123456789101112131415161718192021222324252627282930313233343536373839404142434445464748495051525354555657585960616263646566676869707172737475767778798081828384858687888990919293949596979899100101102103104105106107108109110111112113114115116117118119120121122123124125126127:2147483647-214748364801-150-50:123456789:SomeFault in complexfaultnull234:Second somefault in complexfault";
                    Assert.NotNull(e);
                    Assert.IsType<System.ServiceModel.FaultException<ClientContract.ComplexFault>>(e);
                    var cex = (System.ServiceModel.FaultException<ClientContract.ComplexFault>)e;
                    Assert.Equal(exp, ComplexFaultToString(cex.Detail));
                    break;
                default:
                    break;
            }
        }

        private string ComplexFaultToString(ClientContract.ComplexFault cf)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(cf.ErrorInt);
            sb.Append(':');
            sb.Append(cf.ErrorString);
            sb.Append(':');
            sb.Append(cf.SomeFault.ID);
            sb.Append(':');
            sb.Append(cf.SomeFault.message);
            sb.Append(':');
            for (int i = 0; i < cf.ErrorByteArray.Length; i++)
            {
                sb.Append(cf.ErrorByteArray[i]);
            }

            sb.Append(':');
            for (int i = 0; i < cf.ErrorIntArray.Length; i++)
            {
                sb.Append(cf.ErrorIntArray[i]);
            }

            sb.Append(':');
            for (int i = 0; i < cf.SomeFaultArray.Length; i++)
            {
                if (cf.SomeFaultArray[i] != null)
                {
                    sb.Append(cf.SomeFaultArray[i].ID);
                    sb.Append(':');
                    sb.Append(cf.SomeFaultArray[i].message);
                }
                else
                {
                    sb.Append("null");
                }
            }

            return sb.ToString();
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
                    builder.AddService<FaultOnDiffContractsAndOpsService>();
                    builder.AddServiceEndpoint<FaultOnDiffContractsAndOpsService, ServiceContract.ITestFaultOpContract>(new BasicHttpBinding(), "/BasicWcfService/FaultOnDiffContractsAndOpsService.svc");
                    builder.AddService<DatacontractFaultService>();
                    builder.AddServiceEndpoint<DatacontractFaultService, ServiceContract.ITestDataContractFault>(new BasicHttpBinding(), "/BasicWcfService/DatacontractFaults.svc");
                });
            }
        }
    }
}
