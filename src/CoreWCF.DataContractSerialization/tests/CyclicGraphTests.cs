// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using System.Xml;
using CoreWCF.DataContractSerialization.TestCorpus;
using CoreWCF.DataContractSerialization.Tests.Harness;
using CoreWCF.Runtime.Serialization;
using CoreWCF.DataContractSerialization.TestCorpus.Sanity;
using SerializationTestTypes;
using Xunit;

namespace CoreWCF.DataContractSerialization.Tests
{
    /// <summary>
    /// A cycle in a graph that does not preserve references must fail the same way on both paths.
    /// </summary>
    /// <remarks>
    /// This is the one behaviour the golden-record corpus cannot cover: DataContractSerializer
    /// refuses such a graph, so there is no output to record and no fixture to compare against. What
    /// matters is that the generated writer refuses it too, and refuses it with an exception rather
    /// than by exhausting the stack - a StackOverflowException cannot be caught and takes the
    /// process with it.
    /// </remarks>
    public class CyclicGraphTests
    {
        private const string RootName = "Root";
        private const string RootNamespace = "http://tempuri.org/";

        /// <summary>A self-referencing contract with no IsReference, wired into a cycle.</summary>
        private static List Cycle()
        {
            List first = new List { value = 1 };
            List second = new List { value = 2 };
            first.next = second;
            second.next = first;
            return first;
        }

        /// <summary>
        /// A cycle read back through IsReference is a cycle, not a copy of one.
        /// </summary>
        /// <remarks>
        /// The golden-record test already proves this indirectly - writing the recovered graph back
        /// out reproduces the same z:Id and z:Ref pattern, which only happens if the identities
        /// match - but that is a byte comparison standing in for an object-identity claim. This
        /// states the claim directly, and it is the property IsReference exists for.
        /// </remarks>
        [Fact]
        public void GeneratedReader_RecoversACycleRatherThanACopy()
        {
            CorpusCase corpusCase = CorpusCatalog.GetById(
                "CoreWCF.DataContractSerialization.TestCorpus.Sanity.SanityReferenceNode.cycle");

            XmlDictionary dictionary = new XmlDictionary(2);
            AotXmlObjectSerializer serializer = new GeneratedCorpusContext().GetSerializer(
                corpusCase.ContractType,
                dictionary.Add(corpusCase.RootName),
                dictionary.Add(corpusCase.RootNamespace));

            if (serializer == null)
            {
                Assert.Skip(
                    "The generator does not run on " + TargetFrameworkInfo.Current +
                    ", so there is no generated serializer to exercise.");
                return;
            }

            Assert.True(serializer.CanReadObject);

            byte[] recorded;
            string resolvedPath;
            Assert.True(FixtureStore.TryRead(corpusCase.FixtureFileName, out recorded, out resolvedPath));

            SanityReferenceNode first = Assert.IsType<SanityReferenceNode>(
                FixtureReader.Read(serializer, recorded));

            Assert.Equal("first", first.Name);
            Assert.Same(first, first.Self);

            SanityReferenceNode second = first.Next;
            Assert.Equal("second", second.Name);
            Assert.Same(second, second.Self);

            // The one that could only come from the id table: second.Next points back at a node the
            // reader was still filling in when it read the reference.
            Assert.Same(first, second.Next);
        }

        [Fact]
        public void ReflectionSerializer_RefusesACycleWithoutIsReference()
        {
            DataContractSerializer serializer = new DataContractSerializer(typeof(List), RootName, RootNamespace);

            Assert.Throws<SerializationException>(() => FixtureWriter.Capture(serializer, Cycle()));
        }

        [Fact]
        public void GeneratedSerializer_RefusesACycleWithoutIsReference()
        {
            AotXmlObjectSerializer serializer = CreateGenerated();
            if (serializer == null)
            {
                Assert.Skip(
                    "The generator does not run on " + TargetFrameworkInfo.Current +
                    ", so there is no generated serializer to exercise.");
                return;
            }

            Assert.Throws<SerializationException>(() => FixtureWriter.Capture(serializer, Cycle()));
        }

        [Fact]
        public void GeneratedSerializer_StillWritesADeepChain()
        {
            // The guard must not mistake depth for a cycle. A chain longer than the depth at which
            // tracking starts is legal and has to come out whole.
            AotXmlObjectSerializer serializer = CreateGenerated();
            if (serializer == null)
            {
                Assert.Skip(
                    "The generator does not run on " + TargetFrameworkInfo.Current +
                    ", so there is no generated serializer to exercise.");
                return;
            }

            List head = new List { value = 0 };
            List tail = head;
            for (int i = 1; i < 600; i++)
            {
                tail.next = new List { value = i };
                tail = tail.next;
            }

            byte[] generated = FixtureWriter.Capture(serializer, head);
            byte[] reflection = FixtureWriter.Capture(
                new DataContractSerializer(typeof(List), RootName, RootNamespace), head);

            Assert.Equal(reflection, generated);
        }

        private static AotXmlObjectSerializer CreateGenerated()
        {
            GeneratedCorpusContext context = new GeneratedCorpusContext();
            XmlDictionary dictionary = new XmlDictionary(2);

            return context.GetSerializer(typeof(List), dictionary.Add(RootName), dictionary.Add(RootNamespace));
        }
    }
}
