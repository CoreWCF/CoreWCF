// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using CoreWCF.DataContractSerialization.TestCorpus;
using CoreWCF.DataContractSerialization.Tests.Harness;
using CoreWCF.Description;
using Xunit;

namespace CoreWCF.DataContractSerialization.Tests
{
    public class SerializerProviderTests
    {
        [Fact]
        public void ReflectionProvider_MayAuthorFixtures()
        {
            Assert.True(new ReflectionSerializerProvider().CanProduceFixtures);
        }

        [Fact]
        public void ProvidersMayNotAuthorFixturesByDefault()
        {
            // The default must be "no". When the generated provider is added in the next milestone,
            // it must not opt in: a serializer that records its own expected output proves nothing.
            Assert.False(new StubProvider().CanProduceFixtures);
        }

        [Fact]
        public void ReflectionProvider_ProducesARealDataContractSerializer()
        {
            CorpusCase corpusCase = CorpusCatalog.Cases[0];

            XmlObjectSerializer serializer = new ReflectionSerializerProvider().CreateSerializer(corpusCase);

            Assert.IsType<DataContractSerializer>(serializer);
        }

        [Fact]
        public void CreateSerializer_HonoursTheCaseRootNameAndNamespace()
        {
            CorpusCase corpusCase = CorpusCatalog.Cases[0];

            XmlObjectSerializer serializer = new ReflectionSerializerProvider().CreateSerializer(corpusCase);
            string xml = new System.Text.UTF8Encoding(false)
                .GetString(FixtureWriter.Capture(serializer, corpusCase.CreateInstance()));

            Assert.StartsWith("<" + corpusCase.RootName, xml);
            Assert.Contains("xmlns=\"" + corpusCase.RootNamespace + "\"", xml);
        }

        [Fact]
        public void ProvidersReportEveryCaseSupportedByDefault()
        {
            StubProvider provider = new StubProvider();

            foreach (CorpusCase corpusCase in CorpusCatalog.Cases)
            {
                string reason;
                Assert.False(provider.TryGetUnsupportedReason(corpusCase, out reason));
                Assert.Null(reason);
            }
        }

        private sealed class StubProvider : SerializerProvider
        {
            public override string Id => "Stub";

            protected override DataContractSerializerOperationBehavior CreateBehavior(OperationDescription operation) =>
                new DataContractSerializerOperationBehavior(operation);
        }
    }
}
