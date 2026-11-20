// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using System.Xml;
using CoreWCF.DataContractSerialization.TestCorpus;
using CoreWCF.DataContractSerialization.Tests.Harness;
using CoreWCF.Runtime.Serialization;
using Xunit;

namespace CoreWCF.DataContractSerialization.Tests
{
    /// <summary>
    /// Asserts that a generated reader recovers the graph the fixture was recorded from.
    /// </summary>
    /// <remarks>
    /// See <see cref="FixtureReader"/> for why the assertion is shaped this way: the fixture is read
    /// with the generated serializer and written back with the reflection one, so a read is measured
    /// against the real implementation rather than against its own writer. A mistake shared by the
    /// generated reader and the generated writer would cancel out in a plain round trip and pass.
    /// </remarks>
    public class ReadObjectTests
    {
        private readonly GeneratedCorpusContext _context = new GeneratedCorpusContext();

        [Theory]
        [MemberData(nameof(CorpusTheoryData.CaseIds), MemberType = typeof(CorpusTheoryData))]
        public void ReadObject_RecoversTheRecordedGraph(string caseId)
        {
            CorpusCase corpusCase = CorpusCatalog.GetById(caseId);

            AotXmlObjectSerializer generated = CreateGenerated(corpusCase);
            if (generated == null)
            {
                Assert.Skip("No serializer was generated for " + corpusCase.ContractType.FullName + ".");
                return;
            }

            if (!generated.CanReadObject)
            {
                Assert.Skip("The generated serializer for " + corpusCase.ContractType.FullName + " cannot read yet.");
                return;
            }

            byte[] expected;
            string resolvedPath;
            if (!FixtureStore.TryRead(corpusCase.FixtureFileName, out expected, out resolvedPath))
            {
                Assert.Fail("No golden fixture for case '" + caseId + "'.");
            }

            object graph = FixtureReader.Read(generated, expected);
            byte[] actual = FixtureWriter.Capture(CreateReflection(corpusCase), graph);

            FixtureAssert.Equal(expected, actual, corpusCase, resolvedPath);
        }

        /// <summary>
        /// The reference implementation read back through itself, which must reproduce the fixture.
        /// </summary>
        /// <remarks>
        /// Not a tautology: it proves the fixture is round-trippable at all, so a failure of the
        /// generated case above is a defect in the generated reader rather than a case whose value
        /// the real serializer never recorded in the first place.
        /// </remarks>
        [Theory]
        [MemberData(nameof(CorpusTheoryData.CaseIds), MemberType = typeof(CorpusTheoryData))]
        public void ReflectionReadObject_RecoversTheRecordedGraph(string caseId)
        {
            CorpusCase corpusCase = CorpusCatalog.GetById(caseId);

            byte[] expected;
            string resolvedPath;
            if (!FixtureStore.TryRead(corpusCase.FixtureFileName, out expected, out resolvedPath))
            {
                Assert.Fail("No golden fixture for case '" + caseId + "'.");
            }

            object graph = FixtureReader.Read(CreateReflection(corpusCase), expected);
            byte[] actual = FixtureWriter.Capture(CreateReflection(corpusCase), graph);

            FixtureAssert.Equal(expected, actual, corpusCase, resolvedPath);
        }

        private AotXmlObjectSerializer CreateGenerated(CorpusCase corpusCase)
        {
            XmlDictionary dictionary = new XmlDictionary(2);

            return _context.GetSerializer(
                corpusCase.ContractType,
                dictionary.Add(corpusCase.RootName),
                dictionary.Add(corpusCase.RootNamespace));
        }

        private static DataContractSerializer CreateReflection(CorpusCase corpusCase) =>
            new DataContractSerializer(
                corpusCase.ContractType,
                corpusCase.RootName,
                corpusCase.RootNamespace,
                corpusCase.KnownTypes);
    }
}
