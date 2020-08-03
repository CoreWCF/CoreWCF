using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using System;
using System.Collections;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class ProgrammingModelTests
    {
        private ITestOutputHelper _output;

        public ProgrammingModelTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("XmlElementVoidByte")]
        [InlineData("XmlArrayVoidVoid")]
        [InlineData("XmlArrayItemIntFloat")]
        [InlineData("XmlEltAttribBoolDblDec")]
        [InlineData("XmlArrayItemMsgBodyChStrByteArr")]
        [InlineData("XmlAttribWithMessagePropDTIntCut")]
        [InlineData("BodyHeaderTempUriNsCustArrStrArrList")]
        [InlineData("BodyEmptyNsDecStrBool")]
        public void TypedMessageTypedMethod(string msgVariation)
        {
            Startup._msgVeriation = msgVariation;
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();

                switch (msgVariation)
                {
                    case "XmlElementVoidByte":
                        try
                        {
                            var clientProxy = ClientHelper.GetProxy<ClientContract.ITypedMessageTypedMethodMyService>();
                            byte b = 1;
                            clientProxy.Method1(b);
                            ClientContract.FooMessage1 msg = new ClientContract.FooMessage1();
                            msg.request = "request";
                            msg.ID = 2;
                            ClientContract.Foo foo = new ClientContract.Foo();
                            foo.FooName = TypedMessageTypedMethodConstants.foo;
                            msg.foo = foo;
                            clientProxy.MyOperation(msg);
                            System.Threading.Thread.CurrentThread.Join(5000);
                            Assert.True(ResultHelper.fromMethod == TypedMessageTypedMethodConstants.byteval && ResultHelper.fromMessage == TypedMessageTypedMethodConstants.foo);
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine(ex.ToString());
                        }
                        break;
                    case "XmlArrayVoidVoid":
                        try
                        {
                            var clientProxy = ClientHelper.GetProxy<ClientContract.ITypedMessageTypedMethodMyService2>();
                            ClientContract.FooMessage2 msg2 = new ClientContract.FooMessage2();
                            ClientContract.Foo[] foos = new ClientContract.Foo[2];
                            foos[0] = new ClientContract.Foo();
                            foos[1] = new ClientContract.Foo();
                            foos[0].FooName = TypedMessageTypedMethodConstants.foos;
                            foos[1].FooName = "Another" + TypedMessageTypedMethodConstants.foos;
                            msg2.Name = 2;
                            msg2.foos = foos;
                            clientProxy.Method2();
                            clientProxy.MyOperation(msg2);
                            System.Threading.Thread.CurrentThread.Join(5000);
                            Assert.True(ResultHelper.fromMethod == TypedMessageTypedMethodConstants.voidvoid && ResultHelper.fromMessage == TypedMessageTypedMethodConstants.foos);
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine(ex.ToString());
                        }
                        break;
                    case "XmlArrayItemIntFloat":
                        try
                        {
                            var clientProxy = ClientHelper.GetProxy<ClientContract.ITypedMessageTypedMethodMyService3>();
                            ClientContract.FooMessage3 msg3 = new ClientContract.FooMessage3();
                            ClientContract.Foo[] fooFloat = new ClientContract.Foo[2];
                            fooFloat[0] = new ClientContract.Foo();
                            fooFloat[1] = new ClientContract.Foo();
                            fooFloat[0].FooName = TypedMessageTypedMethodConstants.foos;
                            fooFloat[1].FooName = "Another" + TypedMessageTypedMethodConstants.foos;
                            msg3.Name = 2;
                            msg3.foos = fooFloat;
                            ResultHelper.fromClient = clientProxy.Method3(3.2f).ToString();
                            clientProxy.MyOperation(msg3);
                            System.Threading.Thread.CurrentThread.Join(5000);
                            Assert.True(ResultHelper.fromMethod == TypedMessageTypedMethodConstants.intFloatMethod && ResultHelper.fromMessage == TypedMessageTypedMethodConstants.foos && ResultHelper.fromClient == TypedMessageTypedMethodConstants.intFloatMethod[0].ToString());
                            Assert.True(ResultHelper.fromMethod == TypedMessageTypedMethodConstants.intFloatMethod && ResultHelper.fromMessage == TypedMessageTypedMethodConstants.foos);
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine(ex.ToString());
                        }
                        break;
                    case "XmlEltAttribBoolDblDec":
                        try
                        {
                            var clientProxy = ClientHelper.GetProxy<ClientContract.ITypedMessageTypedMethodMyService4>();
                            ClientContract.FooMessage4 msg4 = new ClientContract.FooMessage4();
                            msg4.newID = 0;
                            msg4.ID = 1;
                            msg4.Name = "name";
                            msg4.Address = "address";
                            ResultHelper.fromClient = clientProxy.Method4(3.2d, 10).ToString();
                            clientProxy.MyOperation(msg4);
                            System.Threading.Thread.CurrentThread.Join(5000);
                            Assert.True(ResultHelper.fromMethod == TypedMessageTypedMethodConstants.boolDblDecMethod && ResultHelper.fromMessage == TypedMessageTypedMethodConstants.zero && ResultHelper.fromClient == TypedMessageTypedMethodConstants.falsestr);
                            Assert.True(ResultHelper.fromMethod == TypedMessageTypedMethodConstants.boolDblDecMethod && ResultHelper.fromMessage == TypedMessageTypedMethodConstants.zero);
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine(ex.ToString());
                        }
                        break;
                    case "XmlArrayItemMsgBodyChStrByteArr":
                        try
                        {
                            var clientProxy = ClientHelper.GetProxy<ClientContract.ITypedMessageTypedMethodMyService5>();
                            ClientContract.FooMessage5 msg5 = new ClientContract.FooMessage5();
                            ClientContract.Foo[] foos = new ClientContract.Foo[2];
                            foos[0] = new ClientContract.Foo();
                            foos[1] = new ClientContract.Foo();
                            foos[0].FooName = TypedMessageTypedMethodConstants.foos;
                            msg5.Name = 1;
                            msg5.foos = foos;
                            byte[] byteArr = { 1, 2, 3, 4, 5 };
                            ResultHelper.fromClient = clientProxy.Method5(TypedMessageTypedMethodConstants.teststr, byteArr).ToString();
                            clientProxy.MyOperation(msg5);
                            System.Threading.Thread.CurrentThread.Join(5000);
                            Assert.True(ResultHelper.fromMethod.Contains(TypedMessageTypedMethodConstants.teststr) && ResultHelper.fromMessage == TypedMessageTypedMethodConstants.foos && ResultHelper.fromClient == TypedMessageTypedMethodConstants.teststr[0].ToString());

                            Assert.True(ResultHelper.fromMethod.Contains(TypedMessageTypedMethodConstants.teststr) && ResultHelper.fromMessage == TypedMessageTypedMethodConstants.foos);
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine(ex.ToString());
                        }
                        break;
                    case "XmlAttribWithMessagePropDTIntCut":
                        try
                        {
                            var clientProxy = ClientHelper.GetProxy<ClientContract.ITypedMessageTypedMethodMyService6>();
                            ClientContract.FooMessage6 msg6 = new ClientContract.FooMessage6();
                            msg6.ID = 0;
                            ClientContract.Foo foo = new ClientContract.Foo();
                            foo.FooName = TypedMessageTypedMethodConstants.foo;
                            DateTime dateTime = clientProxy.Method6(0, foo);
                            ResultHelper.fromClient = dateTime.DayOfWeek.ToString();
                            clientProxy.MyOperation(msg6);
                            System.Threading.Thread.CurrentThread.Join(5000);
                            Assert.True(ResultHelper.fromMethod == DateTime.Now.DayOfWeek.ToString() + TypedMessageTypedMethodConstants.zero && ResultHelper.fromClient == DateTime.Now.DayOfWeek.ToString());
                            Assert.True(ResultHelper.fromMethod == DateTime.Now.DayOfWeek.ToString() + TypedMessageTypedMethodConstants.zero);
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine(ex.ToString());
                        }
                        break;
                    case "BodyHeaderTempUriNsCustArrStrArrList":
                        try
                        {
                            var clientProxy = ClientHelper.GetProxy<ClientContract.ITypedMessageTypedMethodMyService7>();
                            ClientContract.Person person = new ClientContract.Person();
                            person.address = TypedMessageTypedMethodConstants.foo;
                            person.name = TypedMessageTypedMethodConstants.foo;
                            ArrayList personlist = new ArrayList();
                            personlist.Add(person);
                            ResultHelper.fromClient = clientProxy.Method7(TypedMessageTypedMethodConstants.teststr, personlist)[0].FooName;
                            clientProxy.MyOperation(person);
                            System.Threading.Thread.CurrentThread.Join(5000);
                            Assert.True(ResultHelper.fromMethod == TypedMessageTypedMethodConstants.foo && ResultHelper.fromMessage == TypedMessageTypedMethodConstants.foo && ResultHelper.fromClient == DateTime.Now.DayOfWeek.ToString());
                            Assert.True(ResultHelper.fromMethod == TypedMessageTypedMethodConstants.foo + TypedMessageTypedMethodConstants.teststr && ResultHelper.fromMessage == TypedMessageTypedMethodConstants.foo);
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine(ex.ToString());
                        }
                        break;
                    case "BodyEmptyNsDecStrBool":
                        try
                        {
                            var clientProxy = ClientHelper.GetProxy<ClientContract.ITypedMessageTypedMethodMyService8>();
                            ClientContract.Address address = new ClientContract.Address();
                            address.address = TypedMessageTypedMethodConstants.teststr;
                            ClientContract.Manager mgr = new ClientContract.Manager();
                            mgr.Address = address;
                            mgr.name = TypedMessageTypedMethodConstants.foo;
                            clientProxy.MyOperation(mgr);
                            ResultHelper.fromClient = clientProxy.Method8(TypedMessageTypedMethodConstants.teststr, true).ToString();
                            System.Threading.Thread.CurrentThread.Join(5000);
                            Assert.True(ResultHelper.fromMethod == TypedMessageTypedMethodConstants.teststr + TypedMessageTypedMethodConstants.truestr + TypedMessageTypedMethodConstants.zero && ResultHelper.fromMessage == TypedMessageTypedMethodConstants.teststr && ResultHelper.fromClient == TypedMessageTypedMethodConstants.zero);
                            Assert.True(ResultHelper.fromMethod == TypedMessageTypedMethodConstants.teststr + TypedMessageTypedMethodConstants.truestr + TypedMessageTypedMethodConstants.zero && ResultHelper.fromMessage == TypedMessageTypedMethodConstants.teststr);
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine(ex.ToString());
                        }
                        break;
                }
            }
        }
    }

    internal class Startup
    {
        public static string _msgVeriation;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(builder =>
            {
                switch (_msgVeriation)
                {
                    case "XmlElementVoidByte":
                        builder.AddService<Services.TypedMessageTypedMethodMyService>();
                        builder.AddServiceEndpoint<Services.TypedMessageTypedMethodMyService, ServiceContract.ITypedMessageTypedMethodMyService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                        break;
                    case "XmlArrayVoidVoid":
                        builder.AddService<Services.TypedMessageTypedMethodMyService2>();
                        builder.AddServiceEndpoint<Services.TypedMessageTypedMethodMyService2, ServiceContract.ITypedMessageTypedMethodMyService2>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                        break;
                    case "XmlArrayItemIntFloat":
                        builder.AddService<Services.TypedMessageTypedMethodMyService3>();
                        builder.AddServiceEndpoint<Services.TypedMessageTypedMethodMyService3, ServiceContract.ITypedMessageTypedMethodMyService3>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                        break;
                    case "XmlEltAttribBoolDblDec":
                        builder.AddService<Services.TypedMessageTypedMethodMyService4>();
                        builder.AddServiceEndpoint<Services.TypedMessageTypedMethodMyService4, ServiceContract.ITypedMessageTypedMethodMyService4>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                        break;
                    case "XmlArrayItemMsgBodyChStrByteArr":
                        builder.AddService<Services.TypedMessageTypedMethodMyService5>();
                        builder.AddServiceEndpoint<Services.TypedMessageTypedMethodMyService5, ServiceContract.ITypedMessageTypedMethodMyService5>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                        break;
                    case "XmlAttribWithMessagePropDTIntCut":
                        builder.AddService<Services.TypedMessageTypedMethodMyService6>();
                        builder.AddServiceEndpoint<Services.TypedMessageTypedMethodMyService6, ServiceContract.ITypedMessageTypedMethodMyService6>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                        break;
                    case "BodyHeaderTempUriNsCustArrStrArrList":
                        builder.AddService<Services.TypedMessageTypedMethodMyService7>();
                        builder.AddServiceEndpoint<Services.TypedMessageTypedMethodMyService7, ServiceContract.ITypedMessageTypedMethodMyService7>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                        break;
                    case "BodyEmptyNsDecStrBool":
                        builder.AddService<Services.TypedMessageTypedMethodMyService8>();
                        builder.AddServiceEndpoint<Services.TypedMessageTypedMethodMyService8, ServiceContract.ITypedMessageTypedMethodMyService8>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                        break;
                }
            });
        }
    }
}
