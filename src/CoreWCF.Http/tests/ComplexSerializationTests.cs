// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class ComplexSerializationTests
    {
        private ITestOutputHelper _output;
        private const ServiceContract.CallBacksCalled DeserializationCallbacks = ServiceContract.CallBacksCalled.OnDeserializing | ServiceContract.CallBacksCalled.OnDeserialized;
        public ComplexSerializationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("Variation_DataContractEvents_All")]
        [InlineData("Variation_DataContractEvents_OnSerializing")]
        [InlineData("Variation_DataContractEvents_OnSerialized")]
        [InlineData("Variation_DataContractEvents_OnDeserialized")]
        [InlineData("Variation_DataContractEvents_OnDeserializing")]
        public void DataContractEventsComplexSerialization(string variation)
        {
            var host = ServiceHelper.CreateWebHostBuilder<StartupDataContractEvents>(_output).Build();
            using (host)
            {
                host.Start();
                ClientContract.IDataContractEventsService proxy = ClientHelper.GetProxy<ClientContract.IDataContractEventsService>(host);
                switch (variation)
                {
                    case "Variation_DataContractEvents_All":
                        {
                            this.Variation_DataContractEvents_All(proxy);
                        }
                        break;
                    case "Variation_DataContractEvents_OnSerializing":
                        {
                            this.Variation_DataContractEvents_OnSerializing(proxy);
                        }
                        break;
                    case "Variation_DataContractEvents_OnSerialized":
                        {
                            this.Variation_DataContractEvents_OnSerialized(proxy);
                        }
                        break;
                    case "Variation_DataContractEvents_OnDeserialized":
                        {
                            this.Variation_DataContractEvents_OnDeserialized(proxy);
                        }
                        break;
                    case "Variation_DataContractEvents_OnDeserializing":
                        {
                            this.Variation_DataContractEvents_OnDeserializing(proxy);
                        }
                        break;
                    default:
                        {
                            _output.WriteLine("Unknown Variation in TEF {0}", variation);
                            break;
                        }
                }
            }
        }

        [Theory]
        [InlineData("Variation_TypeWithDCInheritingFromSer")]
        [InlineData("Variation_TypeWithSerInheritingFromDC")]
        [InlineData("Variation_BaseDC")]
        public void DataContractInheritanceComplexSerialization(string variation)
        {
            var host = ServiceHelper.CreateWebHostBuilder<StartupDataContractInheritance>(_output).Build();
            using (host)
            {
                host.Start();
                ClientContract.IDataContractInheritanceService proxy = ClientHelper.GetProxy<ClientContract.IDataContractInheritanceService>(host);

                switch (variation)
                {
                    case "Variation_TypeWithDCInheritingFromSer":
                        {
                            this.Variation_TypeWithDCInheritingFromSer(proxy);
                        }
                        break;
                    case "Variation_TypeWithSerInheritingFromDC":
                        {
                            this.Variation_TypeWithSerInheritingFromDC(proxy);

                        }
                        break;
                    case "Variation_BaseDC":
                        {
                            this.Variation_BaseDC(proxy);
                        }
                        break;

                    default:
                        {
                            _output.WriteLine("Unknown Variation in TEF {0}", variation);
                            break;
                        }
                }
            }
        }

        [Theory]
        [InlineData("Variation_TypeWithRelativeNamespace")]
        [InlineData("Variation_TypeWithNumberInNamespace")]
        [InlineData("Variation_TypeWithEmptyNamespace")]
        [InlineData("Variation_TypeWithDefaultNamespace")]
        [InlineData("Variation_TypeWithLongNamespace")]
        [InlineData("Variation_TypeWithUnicodeInNamespace")]
        [InlineData("Variation_TypeWithKeywordsInNamespace")]
        public void DataContractNameSpaceComplexSerialization(string variation)
        {
            var host = ServiceHelper.CreateWebHostBuilder<StartupDataContractNameSpace>(_output).Build();
            using (host)
            {
                host.Start();
                ClientContract.IDataContractNamespaceService proxy = ClientHelper.GetProxy<ClientContract.IDataContractNamespaceService>(host);

                switch (variation)
                {
                    case "Variation_TypeWithRelativeNamespace":
                        {
                            this.Variation_TypeWithRelativeNamespace(proxy);
                        }
                        break;
                    case "Variation_TypeWithNumberInNamespace":
                        {
                            this.Variation_TypeWithNumberInNamespace(proxy);
                        }
                        break;
                    case "Variation_TypeWithEmptyNamespace":
                        {
                            this.Variation_TypeWithEmptyNamespace(proxy);
                        }
                        break;
                    case "Variation_TypeWithDefaultNamespace":
                        {
                            this.Variation_TypeWithDefaultNamespace(proxy);
                        }
                        break;

                    case "Variation_TypeWithLongNamespace":
                        {
                            this.Variation_TypeWithLongNamespace(proxy);
                        }
                        break;

                    case "Variation_TypeWithUnicodeInNamespace":
                        {
                            this.Variation_TypeWithUnicodeInNamespace(proxy);
                        }
                        break;
                    case "Variation_TypeWithKeywordsInNamespace":
                        {
                            this.Variation_TypeWithKeywordsInNamespace(proxy);
                        }
                        break;
                    default:
                        {
                            _output.WriteLine("Unknown Variation in TEF {0}", variation);
                            break;
                        }
                }
            }
        }

        [Theory]
        [InlineData("Variation_MyISerClass")]
        [InlineData("Variation_MyISerStruct")]
        [InlineData("Variation_MyISerClassFromClass")]
        [InlineData("Variation_MyISerClassFromSerializable")]
        [InlineData("Variation_BoxedStructHolder")]
        public void ISerializableComplexSerialization(string variation)
        {
            var host = ServiceHelper.CreateWebHostBuilder<StartupISerializable>(_output).Build();
            using (host)
            {
                host.Start();
                ClientContract.IISerializableService proxy = ClientHelper.GetProxy<ClientContract.IISerializableService>(host);

                switch (variation)
                {
                    case "Variation_MyISerClass":
                        {
                            this.Variation_MyISerClass(proxy);
                        }
                        break;
                    case "Variation_MyISerStruct":
                        {
                            this.Variation_MyISerStruct(proxy);
                        }
                        break;
                    case "Variation_MyISerClassFromClass":
                        {
                            this.Variation_MyISerClassFromClass(proxy);
                        }
                        break;
                    case "Variation_MyISerClassFromSerializable":
                        {
                            this.Variation_MyISerClassFromSerializable(proxy);
                        }
                        break;
                    case "Variation_BoxedStructHolder":
                        {
                            this.Variation_BoxedStructHolder(proxy);
                        }
                        break;
                    default:
                        {
                            _output.WriteLine("Unknown Variation in TEF {0}", variation);
                            break;
                        }
                }
            }
        }

        [Theory]
        [InlineData("Variation_IReadWriteXmlLotsOfData")]
        [InlineData("Variation_IReadWriteXmlNestedWriteString")]
        [InlineData("Variation_IReadWriteXmlWriteAttributesFromReader")]
        [InlineData("Variation_IReadWriteXmlWriteStartAttribute")]
        [InlineData("Variation_IReadWriteXmlWriteName")]
        public void IXMLSerializableComplexSerialization(string variation)
        {
            var host = ServiceHelper.CreateWebHostBuilder<StartupIXMLSerializable>(_output).Build();
            using (host)
            {
                host.Start();
                ClientContract.IIXMLSerializableService proxy = ClientHelper.GetProxy<ClientContract.IIXMLSerializableService>(host);

                switch (variation)
                {
                    case "Variation_IReadWriteXmlLotsOfData":
                        {
                            this.Variation_IReadWriteXmlLotsOfData(proxy);
                        }
                        break;
                    case "Variation_IReadWriteXmlNestedWriteString":
                        {
                            this.Variation_IReadWriteXmlNestedWriteString(proxy);
                        }
                        break;
                    case "Variation_IReadWriteXmlWriteAttributesFromReader":
                        {
                            this.Variation_IReadWriteXmlWriteAttributesFromReader(proxy);
                        }
                        break;
                    case "Variation_IReadWriteXmlWriteStartAttribute":
                        {
                            this.Variation_IReadWriteXmlWriteStartAttribute(proxy);
                        }
                        break;
                    case "Variation_IReadWriteXmlWriteName":
                        {
                            this.Variation_IReadWriteXmlWriteName(proxy);
                        }
                        break;
                    default:
                        {
                            _output.WriteLine("Unknown Variation in TEF {0}", variation);
                            break;
                        }
                }
            }
        }

        [Theory]
        [InlineData("Variation_ClientOldServerNew", "Versioning_789896_Service_New")]
        [InlineData("Variation_ClientOldServerOld", "Versioning_789896_Service_New")]
        [InlineData("Variation_ClientOldServerOld", "Versioning_789896_Service_Old")]
        [InlineData("Variation_ClientOldServerNew", "Versioning_789896_Service_Old")]
        public void VersionComplexSerialization(string variation, string serviceMethod)
        {
            StartupVersioning._method = serviceMethod;
            var host = ServiceHelper.CreateWebHostBuilder<StartupVersioning>(_output).Build();
            using (host)
            {
                host.Start();
                if (serviceMethod == "Versioning_789896_Service_New")
                {
                    ClientContract.IVersioningClientOld proxy = ClientHelper.GetProxy<ClientContract.IVersioningClientOld>(host);

                    switch (variation)
                    {
                        case "Variation_ClientOldServerNew":
                            {
                                this.Variation_ClientOldServerNew(proxy);
                            }
                            break;
                        case "Variation_ClientOldServerOld":
                            {
                                this.Variation_ClientOldServerOld(proxy);
                            }
                            break;
                        default:
                            {
                                _output.WriteLine("Unknown Variation : {0}", variation);
                                break;
                            }
                    }
                }

                if (serviceMethod == "Versioning_789896_Service_Old")
                {
                    ClientContract.IVersioningClientNew proxy = ClientHelper.GetProxy<ClientContract.IVersioningClientNew>(host);
                    switch (variation)
                    {
                        case "Variation_ClientNewServerOld":
                            {
                                this.Variation_ClientNewServerOld(proxy);
                            }
                            break;
                        case "Variation_ClientNewServerNew":
                            {
                                this.Variation_ClientNewServerNew(proxy);
                            }
                            break;
                        default:
                            {
                                _output.WriteLine("Unknown Variation : {0}", variation);
                                break;
                            }
                    }
                }
            }
        }

        private void Variation_ClientOldServerNew(ClientContract.IVersioningClientOld clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_ClientOldServerNew] Method_ClientOldServerNew ");
            ServiceContract.OldContractA input = new ServiceContract.OldContractA();
            ServiceContract.OldContractA output = clientProxy.Method_OldContractA(input);
            Assert.True(output != null, "ClientOldServerNew Method_ClientOldServerNew output should not be null ");

            _output.WriteLine("Testing [Variation_ClientOldServerNew] Method_ClientOldServerNew_out ");
            input = new ServiceContract.OldContractA();
            output = null;
            clientProxy.Method_OldContractA_out(input, out output);
            Assert.True(output != null, "ClientOldServerNew Method_ClientOldServerNew_out output should not be null ");

            _output.WriteLine("Testing [Variation_ClientOldServerNew] Method_ClientOldServerNew_ref ");
            input = new ServiceContract.OldContractA();
            output = null;
            output = clientProxy.Method_OldContractA_ref(ref input);
            Assert.True(output != null, "ClientOldServerNew Method_ClientOldServerNew_ref output should not be null ");
        }

        private void Variation_ClientOldServerOld(ClientContract.IVersioningClientOld clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_ClientOldServerOld] Method_ClientOldServerOld ");
            ServiceContract.OldContractA input = new ServiceContract.OldContractA();
            ServiceContract.OldContractA output = clientProxy.Method_OldContractA(input);
            Assert.True(output != null, "ClientOldServerOld Method_ClientOldServerOld output should not be null ");

            _output.WriteLine("Testing [Variation_ClientOldServerOld] Method_ClientOldServerOld_out ");
            input = new ServiceContract.OldContractA();
            output = null;
            clientProxy.Method_OldContractA_out(input, out output);
            Assert.True(output != null, "ClientOldServerOld Method_ClientOldServerOld_out output should not be null ");

            _output.WriteLine("Testing [Variation_ClientOldServerOld] Method_ClientOldServerOld_ref ");
            input = new ServiceContract.OldContractA();
            output = null;
            output = clientProxy.Method_OldContractA_ref(ref input);
            _output.WriteLine("Method_ClientOldServerOld_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
            Assert.True(output != null, "ClientOldServerOld Method_ClientOldServerOld_ref output should not be null ");
        }

        private void Variation_ClientNewServerOld(ClientContract.IVersioningClientNew clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_ClientNewServerOld] Method_ClientNewServerOld ");
            ServiceContract.NewContractA input = new ServiceContract.NewContractA();
            ServiceContract.NewContractA output = clientProxy.Method_NewContractA(input);
            Assert.True(output != null, "ClientNewServerOld Method_ClientNewServerOld output should not be null ");
            _output.WriteLine("Testing [Variation_ClientNewServerOld] Method_ClientNewServerOld_out ");
            input = new ServiceContract.NewContractA();
            output = null;
            clientProxy.Method_NewContractA_out(input, out output);
            Assert.True(output != null, "ClientNewServerOld Method_ClientNewServerOld_out output should not be null ");

            _output.WriteLine("Testing [Variation_ClientNewServerOld] Method_ClientNewServerOld_ref ");
            input = new ServiceContract.NewContractA();
            output = null;
            output = clientProxy.Method_NewContractA_ref(ref input);
            _output.WriteLine("Method_ClientNewServerOld_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
            Assert.True(output != null, "ClientNewServerOld Method_ClientNewServerOld_ref output should not be null ");
        }

        private void Variation_ClientNewServerNew(ClientContract.IVersioningClientNew clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_ClientNewServerNew] Method_ClientNewServerNew ");
            ServiceContract.NewContractA input = new ServiceContract.NewContractA();
            ServiceContract.NewContractA output = clientProxy.Method_NewContractA(input);
            Assert.True(output != null, "ClientNewServerNew Method_ClientNewServerNew output should not be null ");

            _output.WriteLine("Testing [Variation_ClientNewServerNew] Method_ClientNewServerNew_out ");
            input = new ServiceContract.NewContractA();
            output = null;
            clientProxy.Method_NewContractA_out(input, out output);
            Assert.True(output != null, "ClientNewServerNew Method_ClientNewServerNew_out output should not be null ");

            _output.WriteLine("Testing [Variation_ClientNewServerNew] Method_ClientNewServerNew_ref ");
            input = new ServiceContract.NewContractA();
            output = null;
            output = clientProxy.Method_NewContractA_ref(ref input);
            _output.WriteLine("Method_ClientNewServerNew_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
            Assert.True(output != null, "ClientNewServerNew Method_ClientNewServerNew_ref output should not be null ");
        }

        private void Variation_DataContractEvents_All(ClientContract.IDataContractEventsService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_DataContractEvents_All] Method_All ");
            ServiceContract.DataContractEvents_All input = new ServiceContract.DataContractEvents_All();
            ServiceContract.DataContractEvents_All output = clientProxy.Method_All(input);
            Assert.True(output != null, "DataContractEvents_All Method_All output should not be null ");
            Assert.True(ServiceContract.DataContractEvents_All.Validate(input, output), string.Format("Method_All Recived bad values input: {0} {2} output: {1} ", input.myCallBacksCalled, output.myCallBacksCalled, Environment.NewLine));

            _output.WriteLine("Testing [Variation_DataContractEvents_All] Method_All_out ");
            input = new ServiceContract.DataContractEvents_All();
            output = null;
            clientProxy.Method_All_out(input, out output);
            Assert.True(ServiceContract.DataContractEvents_All.Validate(input, output), string.Format("Method_All_out Recived bad values input: {0} {2} output: {1} ", input.myCallBacksCalled, output.myCallBacksCalled, Environment.NewLine));

            _output.WriteLine("Testing [Variation_DataContractEvents_All] Method_All_ref ");
            input = new ServiceContract.DataContractEvents_All();
            output = null;
            output = clientProxy.Method_All_ref(ref input);
            Assert.True(output != null, "DataContractEvents_All Method_All_ref output should not be null ");
            Assert.False((input.myCallBacksCalled != DeserializationCallbacks || output.myCallBacksCalled != DeserializationCallbacks), string.Format("Method_All_ref Recived bad values input: {0} output: {1}", input.myCallBacksCalled, output.myCallBacksCalled, Environment.NewLine));
        }

        private void Variation_DataContractEvents_OnSerializing(ClientContract.IDataContractEventsService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_DataContractEvents_OnSerializing] Method_OnSerializing ");
            ServiceContract.DataContractEvents_OnSerializing input = new ServiceContract.DataContractEvents_OnSerializing();
            ServiceContract.DataContractEvents_OnSerializing output = clientProxy.Method_OnSerializing(input);
            Assert.True(output != null, "DataContractEvents_OnSerializing Method_OnSerializing output should not be null ");
            Assert.True(ServiceContract.DataContractEvents_OnSerializing.Validate(input, output), string.Format("Method_OnSerializing Recived bad values input: {0} {2} output: {1} ", input.myCallBacksCalled, output.myCallBacksCalled, Environment.NewLine));

            _output.WriteLine("Testing [Variation_DataContractEvents_OnSerializing] Method_OnSerializing_out ");
            input = new ServiceContract.DataContractEvents_OnSerializing();
            output = null;
            clientProxy.Method_OnSerializing_out(input, out output);
            Assert.True(output != null, "DataContractEvents_OnSerializing Method_OnSerializing_out output should not be null ");
            Assert.True(ServiceContract.DataContractEvents_OnSerializing.Validate(input, output), string.Format("Method_OnSerializing_out Recived bad values input: {0} {2} output: {1} ", input.myCallBacksCalled, output.myCallBacksCalled, Environment.NewLine));

            _output.WriteLine("Testing [Variation_DataContractEvents_OnSerializing] Method_OnSerializing_ref ");
            input = new ServiceContract.DataContractEvents_OnSerializing();
            output = null;
            output = clientProxy.Method_OnSerializing_ref(ref input);
            _output.WriteLine("Method_OnSerializing_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());

            Assert.True(output != null, "DataContractEvents_OnSerializing Method_OnSerializing_ref output should not  be  null ");
            Assert.False((input.myCallBacksCalled != ServiceContract.CallBacksCalled.None || output.myCallBacksCalled != ServiceContract.CallBacksCalled.None), string.Format("Method_All_ref Recived bad values input: {0} {2} output: {1} ", input.myCallBacksCalled, output.myCallBacksCalled, Environment.NewLine));
        }

        private void Variation_DataContractEvents_OnSerialized(ClientContract.IDataContractEventsService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_DataContractEvents_OnSerialized] Method_OnSerialized ");
            ServiceContract.DataContractEvents_OnSerialized input = new ServiceContract.DataContractEvents_OnSerialized();
            ServiceContract.DataContractEvents_OnSerialized output = clientProxy.Method_OnSerialized(input);
            Assert.True(output != null, "DataContractEvents_OnSerialized Method_OnSerialized output should not be null ");
            Assert.True(ServiceContract.DataContractEvents_OnSerialized.Validate(input, output), string.Format("Method_OnSerialized Recived bad values input: {0} {2} output: {1} ", input.myCallBacksCalled, output.myCallBacksCalled, Environment.NewLine));

            _output.WriteLine("Testing [Variation_DataContractEvents_OnSerialized] Method_OnSerialized_out ");
            input = new ServiceContract.DataContractEvents_OnSerialized();
            output = null;
            clientProxy.Method_OnSerialized_out(input, out output);
            Assert.True(output != null, "DataContractEvents_OnSerialized Method_OnSerialized_out output should not be null ");
            Assert.True(ServiceContract.DataContractEvents_OnSerialized.Validate(input, output), string.Format("Method_OnSerialized_out Recived bad values input: {0} {2} output: {1} ", input.myCallBacksCalled, output.myCallBacksCalled, Environment.NewLine));
            _output.WriteLine("Testing [Variation_DataContractEvents_OnSerialized] Method_OnSerialized_ref ");

            input = new ServiceContract.DataContractEvents_OnSerialized();
            output = null;
            output = clientProxy.Method_OnSerialized_ref(ref input);
            _output.WriteLine("Method_OnSerialized_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
            Assert.True(output != null, "DataContractEvents_OnSerialized Method_OnSerialized_ref output should not be null ");
            Assert.False((input.myCallBacksCalled != ServiceContract.CallBacksCalled.None || output.myCallBacksCalled != ServiceContract.CallBacksCalled.None), string.Format("Method_All_ref Recived bad values input: {0} {2} output: {1} ", input.myCallBacksCalled, output.myCallBacksCalled, Environment.NewLine));
        }

        private void Variation_DataContractEvents_OnDeserializing(ClientContract.IDataContractEventsService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_DataContractEvents_OnDeserializing] Method_OnDeserializing ");
            ServiceContract.DataContractEvents_OnDeserializing input = new ServiceContract.DataContractEvents_OnDeserializing();
            ServiceContract.DataContractEvents_OnDeserializing output = clientProxy.Method_OnDeserializing(input);
            Assert.True(output != null, "DataContractEvents_OnDeserializing Method_OnDeserializing output should not be null ");
            Assert.True(ServiceContract.DataContractEvents_OnDeserializing.Validate(input, output), string.Format("Method_OnDeserializing Recived bad values input: {0} {2} output: {1} ", input.myCallBacksCalled, output.myCallBacksCalled, Environment.NewLine));

            _output.WriteLine("Testing [Variation_DataContractEvents_OnDeserializing] Method_OnDeserializing_out ");
            input = new ServiceContract.DataContractEvents_OnDeserializing();
            output = null;
            clientProxy.Method_OnDeserializing_out(input, out output);
            Assert.True(output != null, "DataContractEvents_OnDeserializing Method_OnDeserializing_out output should not be null ");
            Assert.True(ServiceContract.DataContractEvents_OnDeserializing.Validate(input, output), string.Format("Method_OnDeserializing_out Recived bad values input: {0} {2} output: {1} ", input.myCallBacksCalled, output.myCallBacksCalled, Environment.NewLine));

            _output.WriteLine("Testing [Variation_DataContractEvents_OnDeserializing] Method_OnDeserializing_ref ");
            input = new ServiceContract.DataContractEvents_OnDeserializing();
            output = null;
            output = clientProxy.Method_OnDeserializing_ref(ref input);
            _output.WriteLine("Method_OnDeserializing_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());

            Assert.True(output != null, "DataContractEvents_OnDeserializing Method_OnDeserializing_ref output should not be null ");
            Assert.False((input.myCallBacksCalled != ServiceContract.CallBacksCalled.OnDeserializing || output.myCallBacksCalled != ServiceContract.CallBacksCalled.OnDeserializing), string.Format("Method_All_ref Recived bad values input: {0} {2} output: {1} ", input.myCallBacksCalled, output.myCallBacksCalled, Environment.NewLine));

        }

        private void Variation_DataContractEvents_OnDeserialized(ClientContract.IDataContractEventsService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_DataContractEvents_OnDeserialized] Method_OnDeserialized ");
            ServiceContract.DataContractEvents_OnDeserialized input = new ServiceContract.DataContractEvents_OnDeserialized();
            ServiceContract.DataContractEvents_OnDeserialized output = clientProxy.Method_OnDeserialized(input);
            Assert.True(output != null, "DataContractEvents_OnDeserialized Method_OnDeserialized output should not be null ");
            Assert.True(ServiceContract.DataContractEvents_OnDeserialized.Validate(input, output), string.Format("Method_OnDeserialized Recived bad values input: {0} {2} output: {1} ", input.myCallBacksCalled, output.myCallBacksCalled, Environment.NewLine));

            _output.WriteLine("Testing [Variation_DataContractEvents_OnDeserialized] Method_OnDeserialized_out ");
            input = new ServiceContract.DataContractEvents_OnDeserialized();
            output = null;
            clientProxy.Method_OnDeserialized_out(input, out output);
            Assert.True(output != null, "DataContractEvents_OnDeserialized Method_OnDeserialized_out output should not be null ");
            Assert.True(ServiceContract.DataContractEvents_OnDeserialized.Validate(input, output), string.Format("Method_OnDeserialized_out Recived bad values input: {0} {2} output: {1} ", input.myCallBacksCalled, output.myCallBacksCalled, Environment.NewLine));

            _output.WriteLine("Testing [Variation_DataContractEvents_OnDeserialized] Method_OnDeserialized_ref ");
            input = new ServiceContract.DataContractEvents_OnDeserialized();
            output = null;
            output = clientProxy.Method_OnDeserialized_ref(ref input);
            _output.WriteLine("Method_OnDeserialized_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
            Assert.True(output != null, "DataContractEvents_OnDeserialized Method_OnDeserialized_ref output should not be null ");
            Assert.False((input.myCallBacksCalled != ServiceContract.CallBacksCalled.OnDeserialized || output.myCallBacksCalled != ServiceContract.CallBacksCalled.OnDeserialized), string.Format("Method_All_ref Recived bad values input: {0} {2} output: {1} ", input.myCallBacksCalled, output.myCallBacksCalled, Environment.NewLine));
        }

        private void Variation_TypeWithDCInheritingFromSer(ClientContract.IDataContractInheritanceService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_TypeWithDCInheritingFromSer] Method_TypeWithDCInheritingFromSer ");
            ServiceContract.TypeWithDCInheritingFromSer input = new ServiceContract.TypeWithDCInheritingFromSer();
            ServiceContract.TypeWithDCInheritingFromSer output = clientProxy.Method_TypeWithDCInheritingFromSer(input);
            Assert.True(output != null, "TypeWithDCInheritingFromSer Method_TypeWithDCInheritingFromSer output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithDCInheritingFromSer] Method_TypeWithDCInheritingFromSer_out ");
            input = new ServiceContract.TypeWithDCInheritingFromSer();
            output = null;
            clientProxy.Method_TypeWithDCInheritingFromSer_out(input, out output);
            Assert.True(output != null, "TypeWithDCInheritingFromSer Method_TypeWithDCInheritingFromSer_out output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithDCInheritingFromSer] Method_TypeWithDCInheritingFromSer_ref ");
            input = new ServiceContract.TypeWithDCInheritingFromSer();
            output = null;
            output = clientProxy.Method_TypeWithDCInheritingFromSer_ref(ref input);
            Assert.True(output != null, "TypeWithDCInheritingFromSer Method_TypeWithDCInheritingFromSer_ref output should not be null ");

        }

        private void Variation_TypeWithSerInheritingFromDC(ClientContract.IDataContractInheritanceService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_TypeWithSerInheritingFromDC] Method_TypeWithSerInheritingFromDC ");
            ServiceContract.TypeWithSerInheritingFromDC input = new ServiceContract.TypeWithSerInheritingFromDC();
            ServiceContract.TypeWithSerInheritingFromDC output = clientProxy.Method_TypeWithSerInheritingFromDC(input);
            Assert.True(output != null, "TypeWithSerInheritingFromDC Method_TypeWithSerInheritingFromDC output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithSerInheritingFromDC] Method_TypeWithSerInheritingFromDC_out ");
            input = new ServiceContract.TypeWithSerInheritingFromDC();
            output = null;
            clientProxy.Method_TypeWithSerInheritingFromDC_out(input, out output);
            Assert.True(output != null, "TypeWithSerInheritingFromDC Method_TypeWithSerInheritingFromDC_out output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithSerInheritingFromDC] Method_TypeWithSerInheritingFromDC_ref ");
            input = new ServiceContract.TypeWithSerInheritingFromDC();
            output = null;
            output = clientProxy.Method_TypeWithSerInheritingFromDC_ref(ref input);
            _output.WriteLine("Method_TypeWithSerInheritingFromDC_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
            Assert.True(output != null, "TypeWithSerInheritingFromDC Method_TypeWithSerInheritingFromDC_ref output should not be null ");
        }

        private void Variation_BaseDC(ClientContract.IDataContractInheritanceService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_BaseDC] Method_BaseDC ");
            ServiceContract.BaseDC input = new ServiceContract.BaseDC();
            ServiceContract.BaseDC output = clientProxy.Method_BaseDC(input);
            Assert.True(output != null, "BaseDC Method_BaseDC output should not be null ");

            _output.WriteLine("Testing [Variation_BaseDC] Method_BaseDC_out ");
            input = new ServiceContract.BaseDC();
            output = null;
            clientProxy.Method_BaseDC_out(input, out output);
            Assert.True(output != null, "BaseDC Method_BaseDC_out output should not be null ");

            _output.WriteLine("Testing [Variation_BaseDC] Method_BaseDC_ref ");
            input = new ServiceContract.BaseDC();
            output = null;
            output = clientProxy.Method_BaseDC_ref(ref input);
            _output.WriteLine("Method_BaseDC_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
            Assert.True(output != null, "BaseDC Method_BaseDC_ref output should not be null ");
        }

        private void Variation_TypeWithRelativeNamespace(ClientContract.IDataContractNamespaceService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_TypeWithRelativeNamespace] Method_TypeWithRelativeNamespace ");
            ServiceContract.TypeWithRelativeNamespace input = new ServiceContract.TypeWithRelativeNamespace();
            ServiceContract.TypeWithRelativeNamespace output = clientProxy.Method_TypeWithRelativeNamespace(input);
            Assert.True(output != null, "TypeWithRelativeNamespace Method_TypeWithRelativeNamespace output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithRelativeNamespace] Method_TypeWithRelativeNamespace_out ");
            input = new ServiceContract.TypeWithRelativeNamespace();
            output = null;
            clientProxy.Method_TypeWithRelativeNamespace_out(input, out output);
            Assert.True(output != null, "TypeWithRelativeNamespace Method_TypeWithRelativeNamespace_out output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithRelativeNamespace] Method_TypeWithRelativeNamespace_ref ");
            input = new ServiceContract.TypeWithRelativeNamespace();
            output = null;
            output = clientProxy.Method_TypeWithRelativeNamespace_ref(ref input);
            Assert.True(output != null, "TypeWithRelativeNamespace Method_TypeWithRelativeNamespace_ref output should not be null ");
        }

        private void Variation_TypeWithNumberInNamespace(ClientContract.IDataContractNamespaceService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_TypeWithNumberInNamespace] Method_TypeWithNumberInNamespace ");
            ServiceContract.TypeWithNumberInNamespace input = new ServiceContract.TypeWithNumberInNamespace();
            ServiceContract.TypeWithNumberInNamespace output = clientProxy.Method_TypeWithNumberInNamespace(input);
            Assert.True(output != null, "TypeWithNumberInNamespace Method_TypeWithNumberInNamespace output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithNumberInNamespace] Method_TypeWithNumberInNamespace_out ");
            input = new ServiceContract.TypeWithNumberInNamespace();
            output = null;
            clientProxy.Method_TypeWithNumberInNamespace_out(input, out output);
            Assert.True(output != null, "TypeWithNumberInNamespace Method_TypeWithNumberInNamespace_out output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithNumberInNamespace] Method_TypeWithNumberInNamespace_ref ");
            input = new ServiceContract.TypeWithNumberInNamespace();
            output = null;
            output = clientProxy.Method_TypeWithNumberInNamespace_ref(ref input);
            _output.WriteLine("Method_TypeWithNumberInNamespace_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
            Assert.True(output != null, "TypeWithNumberInNamespace Method_TypeWithNumberInNamespace_ref output should not be null ");
        }

        private void Variation_TypeWithEmptyNamespace(ClientContract.IDataContractNamespaceService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_TypeWithEmptyNamespace] Method_TypeWithEmptyNamespace ");
            ServiceContract.TypeWithEmptyNamespace input = new ServiceContract.TypeWithEmptyNamespace();
            ServiceContract.TypeWithEmptyNamespace output = clientProxy.Method_TypeWithEmptyNamespace(input);
            Assert.True(output != null, "TypeWithEmptyNamespace Method_TypeWithEmptyNamespace output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithEmptyNamespace] Method_TypeWithEmptyNamespace_out ");
            input = new ServiceContract.TypeWithEmptyNamespace();
            output = null;
            clientProxy.Method_TypeWithEmptyNamespace_out(input, out output);
            Assert.True(output != null, "TypeWithEmptyNamespace Method_TypeWithEmptyNamespace_out output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithEmptyNamespace] Method_TypeWithEmptyNamespace_ref ");
            input = new ServiceContract.TypeWithEmptyNamespace();
            output = null;
            output = clientProxy.Method_TypeWithEmptyNamespace_ref(ref input);
            _output.WriteLine("Method_TypeWithEmptyNamespace_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
            Assert.True(output != null, "TypeWithEmptyNamespace Method_TypeWithEmptyNamespace_ref output should not be null ");
        }

        private void Variation_TypeWithLongNamespace(ClientContract.IDataContractNamespaceService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_TypeWithLongNamespace] Method_TypeWithLongNamespace ");
            ServiceContract.TypeWithLongNamespace input = new ServiceContract.TypeWithLongNamespace();
            ServiceContract.TypeWithLongNamespace output = clientProxy.Method_TypeWithLongNamespace(input);
            Assert.True(output != null, "TypeWithLongNamespace Method_TypeWithLongNamespace output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithLongNamespace] Method_TypeWithLongNamespace_out ");
            input = new ServiceContract.TypeWithLongNamespace();
            output = null;
            clientProxy.Method_TypeWithLongNamespace_out(input, out output);
            Assert.True(output != null, "TypeWithLongNamespace Method_TypeWithLongNamespace_out output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithLongNamespace] Method_TypeWithLongNamespace_ref ");

            input = new ServiceContract.TypeWithLongNamespace();
            output = null;
            output = clientProxy.Method_TypeWithLongNamespace_ref(ref input);
            _output.WriteLine("Method_TypeWithLongNamespace_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
            Assert.True(output != null, "TypeWithLongNamespace Method_TypeWithLongNamespace_ref output should not be null ");
        }

        private void Variation_TypeWithDefaultNamespace(ClientContract.IDataContractNamespaceService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_TypeWithDefaultNamespace] Method_TypeWithDefaultNamespace ");
            ServiceContract.TypeWithDefaultNamespace input = new ServiceContract.TypeWithDefaultNamespace();
            ServiceContract.TypeWithDefaultNamespace output = clientProxy.Method_TypeWithDefaultNamespace(input);
            Assert.True(output != null, "TypeWithDefaultNamespace Method_TypeWithDefaultNamespace output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithDefaultNamespace] Method_TypeWithDefaultNamespace_out ");
            input = new ServiceContract.TypeWithDefaultNamespace();
            output = null;
            clientProxy.Method_TypeWithDefaultNamespace_out(input, out output);
            Assert.True(output != null, "TypeWithDefaultNamespace Method_TypeWithDefaultNamespace_out output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithDefaultNamespace] Method_TypeWithDefaultNamespace_ref ");
            input = new ServiceContract.TypeWithDefaultNamespace();
            output = null;
            output = clientProxy.Method_TypeWithDefaultNamespace_ref(ref input);
            _output.WriteLine("Method_TypeWithDefaultNamespace_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
            Assert.True(output != null, "TypeWithDefaultNamespace Method_TypeWithDefaultNamespace_ref output should not be null ");
        }

        private void Variation_TypeWithKeywordsInNamespace(ClientContract.IDataContractNamespaceService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_TypeWithKeywordsInNamespace] Method_TypeWithKeywordsInNamespace ");
            ServiceContract.TypeWithKeywordsInNamespace input = new ServiceContract.TypeWithKeywordsInNamespace();
            ServiceContract.TypeWithKeywordsInNamespace output = clientProxy.Method_TypeWithKeywordsInNamespace(input);
            Assert.True(output != null, "TypeWithKeywordsInNamespace Method_TypeWithKeywordsInNamespace output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithKeywordsInNamespace] Method_TypeWithKeywordsInNamespace_out ");
            input = new ServiceContract.TypeWithKeywordsInNamespace();
            output = null;
            clientProxy.Method_TypeWithKeywordsInNamespace_out(input, out output);
            Assert.True(output != null, "TypeWithKeywordsInNamespace Method_TypeWithKeywordsInNamespace_out output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithKeywordsInNamespace] Method_TypeWithKeywordsInNamespace_ref ");
            input = new ServiceContract.TypeWithKeywordsInNamespace();
            output = null;
            output = clientProxy.Method_TypeWithKeywordsInNamespace_ref(ref input);
            _output.WriteLine("Method_TypeWithKeywordsInNamespace_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
            Assert.True(output != null, "TypeWithKeywordsInNamespace Method_TypeWithKeywordsInNamespace_ref output should not be null ");
        }

        private void Variation_TypeWithUnicodeInNamespace(ClientContract.IDataContractNamespaceService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_TypeWithUnicodeInNamespace] Method_TypeWithUnicodeInNamespace ");
            ServiceContract.TypeWithUnicodeInNamespace input = new ServiceContract.TypeWithUnicodeInNamespace();
            ServiceContract.TypeWithUnicodeInNamespace output = clientProxy.Method_TypeWithUnicodeInNamespace(input);
            Assert.True(output != null, "TypeWithUnicodeInNamespace Method_TypeWithUnicodeInNamespace output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithUnicodeInNamespace] Method_TypeWithUnicodeInNamespace_out ");
            input = new ServiceContract.TypeWithUnicodeInNamespace();
            output = null;
            clientProxy.Method_TypeWithUnicodeInNamespace_out(input, out output);
            Assert.True(output != null, "TypeWithUnicodeInNamespace Method_TypeWithUnicodeInNamespace_out output should not be null ");

            _output.WriteLine("Testing [Variation_TypeWithUnicodeInNamespace] Method_TypeWithUnicodeInNamespace_ref ");
            input = new ServiceContract.TypeWithUnicodeInNamespace();
            output = null;
            output = clientProxy.Method_TypeWithUnicodeInNamespace_ref(ref input);
            _output.WriteLine("Method_TypeWithUnicodeInNamespace_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
            Assert.True(output != null, "TypeWithUnicodeInNamespace Method_TypeWithUnicodeInNamespace_ref output should not be null ");
        }

        private void Variation_MyISerClass(ClientContract.IISerializableService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_MyISerClass] Method_MyISerClass ");
            ServiceContract.MyISerClass input = new ServiceContract.MyISerClass();
            ServiceContract.MyISerClass output = clientProxy.Method_MyISerClass(input);
            Assert.True(output != null, "MyISerClass Method_MyISerClass output should not be null ");

            _output.WriteLine("Testing [Variation_MyISerClass] Method_MyISerClass_out ");
            input = new ServiceContract.MyISerClass();
            output = null;
            clientProxy.Method_MyISerClass_out(input, out output);
            Assert.True(output != null, "MyISerClass Method_MyISerClass_out output should not be null ");

            _output.WriteLine("Testing [Variation_MyISerClass] Method_MyISerClass_ref ");
            input = new ServiceContract.MyISerClass();
            output = null;
            output = clientProxy.Method_MyISerClass_ref(ref input);
            Assert.True(output != null, "MyISerClass Method_MyISerClass_ref output should not be null ");
        }

        private void Variation_MyISerStruct(ClientContract.IISerializableService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_MyISerStruct] Method_MyISerStruct ");
            ServiceContract.MyISerStruct input = new ServiceContract.MyISerStruct();
            ServiceContract.MyISerStruct output = clientProxy.Method_MyISerStruct(input);
            _output.WriteLine("Testing [Variation_MyISerStruct] Method_MyISerStruct_out ");
            input = new ServiceContract.MyISerStruct();
            // output = null;
            clientProxy.Method_MyISerStruct_out(input, out output);

            _output.WriteLine("Testing [Variation_MyISerStruct] Method_MyISerStruct_ref ");
            input = new ServiceContract.MyISerStruct();
            // output = null;
            output = clientProxy.Method_MyISerStruct_ref(ref input);
            _output.WriteLine("Method_MyISerStruct_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
        }

        private void Variation_MyISerClassFromClass(ClientContract.IISerializableService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_MyISerClassFromClass] Method_MyISerClassFromClass ");
            ServiceContract.MyISerClassFromClass input = new ServiceContract.MyISerClassFromClass();
            ServiceContract.MyISerClassFromClass output = clientProxy.Method_MyISerClassFromClass(input);
            Assert.True(output != null, "MyISerClassFromClass Method_MyISerClassFromClass output should not be null ");

            _output.WriteLine("Testing [Variation_MyISerClassFromClass] Method_MyISerClassFromClass_out ");
            input = new ServiceContract.MyISerClassFromClass();
            output = null;
            clientProxy.Method_MyISerClassFromClass_out(input, out output);
            Assert.True(output != null, "MyISerClassFromClass Method_MyISerClassFromClass_out output should not be null ");

            _output.WriteLine("Testing [Variation_MyISerClassFromClass] Method_MyISerClassFromClass_ref ");
            input = new ServiceContract.MyISerClassFromClass();
            output = null;
            output = clientProxy.Method_MyISerClassFromClass_ref(ref input);
            _output.WriteLine("Method_MyISerClassFromClass_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
            Assert.True(output != null, "MyISerClassFromClass Method_MyISerClassFromClass_ref output should not be null ");
        }

        private void Variation_BoxedStructHolder(ClientContract.IISerializableService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_BoxedStructHolder] Method_BoxedStructHolder ");
            ServiceContract.BoxedStructHolder input = new ServiceContract.BoxedStructHolder();
            ServiceContract.BoxedStructHolder output = clientProxy.Method_BoxedStructHolder(input);
            Assert.True(output != null, "BoxedStructHolder Method_BoxedStructHolder output should not be null ");

            _output.WriteLine("Testing [Variation_BoxedStructHolder] Method_BoxedStructHolder_out ");
            input = new ServiceContract.BoxedStructHolder();
            output = null;
            clientProxy.Method_BoxedStructHolder_out(input, out output);
            Assert.True(output != null, "BoxedStructHolder Method_BoxedStructHolder_out output should not be null ");

            _output.WriteLine("Testing [Variation_BoxedStructHolder] Method_BoxedStructHolder_ref ");
            input = new ServiceContract.BoxedStructHolder();
            output = null;
            output = clientProxy.Method_BoxedStructHolder_ref(ref input);
            _output.WriteLine("Method_BoxedStructHolder_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
            Assert.True(output != null, "BoxedStructHolder Method_BoxedStructHolder_ref output should not be null ");
        }

        private void Variation_MyISerClassFromSerializable(ClientContract.IISerializableService clientProxy)
        {
            // Send the two way message
            _output.WriteLine("Testing [Variation_MyISerClassFromSerializable] Method_MyISerClassFromSerializable ");
            ServiceContract.MyISerClassFromSerializable input = new ServiceContract.MyISerClassFromSerializable();
            ServiceContract.MyISerClassFromSerializable output = clientProxy.Method_MyISerClassFromSerializable(input);
            Assert.True(output != null, "MyISerClassFromSerializable Method_MyISerClassFromSerializable output should not be null ");

            _output.WriteLine("Testing [Variation_MyISerClassFromSerializable] Method_MyISerClassFromSerializable_out ");
            input = new ServiceContract.MyISerClassFromSerializable();
            output = null;
            clientProxy.Method_MyISerClassFromSerializable_out(input, out output);
            Assert.True(output != null, "MyISerClassFromSerializable Method_MyISerClassFromSerializable_out output should not be null ");

            _output.WriteLine("Testing [Variation_MyISerClassFromSerializable] Method_MyISerClassFromSerializable_ref ");
            input = new ServiceContract.MyISerClassFromSerializable();
            output = null;
            output = clientProxy.Method_MyISerClassFromSerializable_ref(ref input);
            _output.WriteLine("Method_MyISerClassFromSerializable_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
            Assert.True(output != null, "MyISerClassFromSerializable Method_MyISerClassFromSerializable_ref output should not be null ");
        }

        private void Variation_IReadWriteXmlLotsOfData(ClientContract.IIXMLSerializableService clientProxy)
        {
            try
            {
                // Send the two way message
                _output.WriteLine("Testing [Variation_IReadWriteXmlLotsOfData] Method_IReadWriteXmlLotsOfData ");
                ServiceContract.IReadWriteXmlLotsOfData input = new ServiceContract.IReadWriteXmlLotsOfData();
                ServiceContract.IReadWriteXmlLotsOfData output = clientProxy.Method_IReadWriteXmlLotsOfData(input);
                Assert.True(output != null, "IReadWriteXmlLotsOfData Method_IReadWriteXmlLotsOfData output should not be null ");

                _output.WriteLine("Testing [Variation_IReadWriteXmlLotsOfData] Method_IReadWriteXmlLotsOfData_out ");
                input = new ServiceContract.IReadWriteXmlLotsOfData();
                output = null;
                clientProxy.Method_IReadWriteXmlLotsOfData_out(input, out output);
                Assert.True(output != null, "IReadWriteXmlLotsOfData Method_IReadWriteXmlLotsOfData_out output should not be null ");

                _output.WriteLine("Testing [Variation_IReadWriteXmlLotsOfData] Method_IReadWriteXmlLotsOfData_ref ");
                input = new ServiceContract.IReadWriteXmlLotsOfData();
                output = null;
                output = clientProxy.Method_IReadWriteXmlLotsOfData_ref(ref input);
                Assert.True(output != null, "IReadWriteXmlLotsOfData Method_IReadWriteXmlLotsOfData_ref output should not be null ");
            }
            finally
            {
                CloseProxy(clientProxy);
            }
        }

        private void Variation_IReadWriteXmlNestedWriteString(ClientContract.IIXMLSerializableService clientProxy)
        {
            try
            {
                // Send the two way message
                _output.WriteLine("Testing [Variation_IReadWriteXmlNestedWriteString] Method_IReadWriteXmlNestedWriteString ");
                ServiceContract.IReadWriteXmlNestedWriteString input = new ServiceContract.IReadWriteXmlNestedWriteString();
                ServiceContract.IReadWriteXmlNestedWriteString output = clientProxy.Method_IReadWriteXmlNestedWriteString(input);
                Assert.True(output != null, "IReadWriteXmlNestedWriteString Method_IReadWriteXmlNestedWriteString output should not be null ");

                _output.WriteLine("Testing [Variation_IReadWriteXmlNestedWriteString] Method_IReadWriteXmlNestedWriteString_out ");
                input = new ServiceContract.IReadWriteXmlNestedWriteString();
                output = null;
                clientProxy.Method_IReadWriteXmlNestedWriteString_out(input, out output);
                Assert.True(output != null, "IReadWriteXmlNestedWriteString Method_IReadWriteXmlNestedWriteString_out output should not be null ");

                _output.WriteLine("Testing [Variation_IReadWriteXmlNestedWriteString] Method_IReadWriteXmlNestedWriteString_ref ");
                input = new ServiceContract.IReadWriteXmlNestedWriteString();
                output = null;
                output = clientProxy.Method_IReadWriteXmlNestedWriteString_ref(ref input);
                _output.WriteLine("Method_IReadWriteXmlNestedWriteString_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
                Assert.True(output != null, "IReadWriteXmlNestedWriteString Method_IReadWriteXmlNestedWriteString_ref output should not be null ");
            }
            finally
            {
                CloseProxy(clientProxy);
            }
        }

        private void Variation_IReadWriteXmlWriteAttributesFromReader(ClientContract.IIXMLSerializableService clientProxy)
        {
            try
            {
                // Send the two way message
                _output.WriteLine("Testing [Variation_IReadWriteXmlWriteAttributesFromReader] Method_IReadWriteXmlWriteAttributesFromReader ");
                ServiceContract.IReadWriteXmlWriteAttributesFromReader input = new ServiceContract.IReadWriteXmlWriteAttributesFromReader();
                ServiceContract.IReadWriteXmlWriteAttributesFromReader output = clientProxy.Method_IReadWriteXmlWriteAttributesFromReader(input);
                Assert.True(output != null, "IReadWriteXmlWriteAttributesFromReader Method_IReadWriteXmlWriteAttributesFromReader output should not be null ");

                _output.WriteLine("Testing [Variation_IReadWriteXmlWriteAttributesFromReader] Method_IReadWriteXmlWriteAttributesFromReader_out ");
                input = new ServiceContract.IReadWriteXmlWriteAttributesFromReader();
                output = null;
                clientProxy.Method_IReadWriteXmlWriteAttributesFromReader_out(input, out output);
                Assert.True(output != null, "IReadWriteXmlWriteAttributesFromReader Method_IReadWriteXmlWriteAttributesFromReader_out output should not be null ");

                _output.WriteLine("Testing [Variation_IReadWriteXmlWriteAttributesFromReader] Method_IReadWriteXmlWriteAttributesFromReader_ref ");
                input = new ServiceContract.IReadWriteXmlWriteAttributesFromReader();
                output = null;
                output = clientProxy.Method_IReadWriteXmlWriteAttributesFromReader_ref(ref input);
                _output.WriteLine("Method_IReadWriteXmlWriteAttributesFromReader_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
                Assert.True(output != null, "IReadWriteXmlWriteAttributesFromReader Method_IReadWriteXmlWriteAttributesFromReader_ref output should not be null ");
            }
            finally
            {
                CloseProxy(clientProxy);
            }
        }

        private void Variation_IReadWriteXmlWriteName(ClientContract.IIXMLSerializableService clientProxy)
        {
            try
            {
                // Send the two way message
                _output.WriteLine("Testing [Variation_IReadWriteXmlWriteName] Method_IReadWriteXmlWriteName ");
                ServiceContract.IReadWriteXmlWriteName input = new ServiceContract.IReadWriteXmlWriteName();
                ServiceContract.IReadWriteXmlWriteName output = clientProxy.Method_IReadWriteXmlWriteName(input);
                Assert.True(output != null, "IReadWriteXmlWriteName Method_IReadWriteXmlWriteName output should not be null ");

                _output.WriteLine("Testing [Variation_IReadWriteXmlWriteName] Method_IReadWriteXmlWriteName_out ");
                input = new ServiceContract.IReadWriteXmlWriteName();
                output = null;
                clientProxy.Method_IReadWriteXmlWriteName_out(input, out output);
                Assert.True(output != null, "IReadWriteXmlWriteName Method_IReadWriteXmlWriteName_out output should not be null ");

                _output.WriteLine("Testing [Variation_IReadWriteXmlWriteName] Method_IReadWriteXmlWriteName_ref ");
                input = new ServiceContract.IReadWriteXmlWriteName();
                output = null;
                output = clientProxy.Method_IReadWriteXmlWriteName_ref(ref input);
                _output.WriteLine("Method_IReadWriteXmlWriteName_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
                Assert.True(output != null, "IReadWriteXmlWriteName Method_IReadWriteXmlWriteName_ref output should not be null ");
            }
            finally
            {
                CloseProxy(clientProxy);
            }
        }

        private void Variation_IReadWriteXmlWriteStartAttribute(ClientContract.IIXMLSerializableService clientProxy)
        {
            try
            {
                // Send the two way message
                _output.WriteLine("Testing [Variation_IReadWriteXmlWriteStartAttribute] Method_IReadWriteXmlWriteStartAttribute ");
                ServiceContract.IReadWriteXmlWriteStartAttribute input = new ServiceContract.IReadWriteXmlWriteStartAttribute();
                ServiceContract.IReadWriteXmlWriteStartAttribute output = clientProxy.Method_IReadWriteXmlWriteStartAttribute(input);
                Assert.True(output != null, "IReadWriteXmlWriteStartAttribute Method_IReadWriteXmlWriteStartAttribute output should not be null ");

                _output.WriteLine("Testing [Variation_IReadWriteXmlWriteStartAttribute] Method_IReadWriteXmlWriteStartAttribute_out ");
                input = new ServiceContract.IReadWriteXmlWriteStartAttribute();
                output = null;
                clientProxy.Method_IReadWriteXmlWriteStartAttribute_out(input, out output);
                Assert.True(output != null, "IReadWriteXmlWriteStartAttribute Method_IReadWriteXmlWriteStartAttribute_out output should not be null ");

                _output.WriteLine("Testing [Variation_IReadWriteXmlWriteStartAttribute] Method_IReadWriteXmlWriteStartAttribute_ref ");
                input = new ServiceContract.IReadWriteXmlWriteStartAttribute();
                output = null;
                output = clientProxy.Method_IReadWriteXmlWriteStartAttribute_ref(ref input);
                _output.WriteLine("Method_IReadWriteXmlWriteStartAttribute_ref objects {0} {1}", input.GetHashCode(), output.GetHashCode());
                Assert.True(output != null, "IReadWriteXmlWriteStartAttribute Method_IReadWriteXmlWriteStartAttribute_ref output should not be null ");
            }
            finally
            {
                CloseProxy(clientProxy);
            }
        }

        private void CloseProxy(ClientContract.IIXMLSerializableService clientProxy)
        {
            if (clientProxy != null)
            {
                _output.WriteLine("In CloseProxy");
                if (clientProxy == null)
                {
                    throw new ArgumentNullException("Proxy");
                }

                if (clientProxy is System.ServiceModel.IClientChannel)
                {
                    System.ServiceModel.IClientChannel clientChannel = clientProxy as System.ServiceModel.IClientChannel;
                    _output.WriteLine("proxy is IClientChannel, state = " + clientChannel.State);
                    if (clientChannel != null && clientChannel.State == System.ServiceModel.CommunicationState.Opened)
                    {
                        _output.WriteLine("Going to close Channel, state is Opened");
                        clientChannel.Close();
                        _output.WriteLine("Going to Dispose, after Close()");
                        clientChannel.Dispose();
                    }
                    else
                    {
                        _output.WriteLine("NOT CLOSING. Already CLOSED proxy is IClientChannel");
                    }
                }
            }
        }

        internal class StartupDataContractEvents
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.DataContractEvents_789910_Service>();
                    builder.AddServiceEndpoint<Services.DataContractEvents_789910_Service, ServiceContract.IDataContractEventsService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }

        internal class StartupDataContractInheritance
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.DataContractInheritance_790064_Service>();
                    builder.AddServiceEndpoint<Services.DataContractInheritance_790064_Service, ServiceContract.IDataContractInheritanceService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }

        internal class StartupDataContractNameSpace
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.DataContractNamespace_789916_Service>();
                    builder.AddServiceEndpoint<Services.DataContractNamespace_789916_Service, ServiceContract.IDataContractNamespaceService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }

        internal class StartupISerializable
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.ISerializable_789870_Service>();
                    builder.AddServiceEndpoint<Services.ISerializable_789870_Service, ServiceContract.IISerializableService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }

        internal class StartupIXMLSerializable
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.IXMLSerializable_789868_Service>();
                    builder.AddServiceEndpoint<Services.IXMLSerializable_789868_Service, ServiceContract.IIXMLSerializableService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }

        internal class StartupVersioning
        {
            public static string _method;
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    switch (_method)
                    {
                        case "Versioning_789896_Service_New":
                            builder.AddService<Services.Versioning_789896_Service_New>();
                            builder.AddServiceEndpoint<Services.Versioning_789896_Service_New, ServiceContract.IVersioningServiceNew>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                            break;
                        case "Versioning_789896_Service_Old":
                            builder.AddService<Services.Versioning_789896_Service_Old>();
                            builder.AddServiceEndpoint<Services.Versioning_789896_Service_Old, ServiceContract.IVersioningServiceOld>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                            break;
                        default:
                            throw new Exception("Cannot find the service to host");
                    }
                });
            }
        }
    }
}
