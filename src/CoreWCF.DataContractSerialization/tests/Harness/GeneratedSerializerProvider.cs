// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using CoreWCF.DataContractSerialization.TestCorpus;
using CoreWCF.Description;
using CoreWCF.Runtime.Serialization;

namespace CoreWCF.DataContractSerialization.Tests.Harness
{
    /// <summary>
    /// The source-generated serializers, measured against the same fixtures as the reflection-based
    /// reference implementation.
    /// </summary>
    /// <remarks>
    /// Deliberately does not override <c>CanProduceFixtures</c>. A serializer that recorded its own
    /// expected output would make the whole corpus a tautology; the baselines must keep coming from
    /// the reflection implementation alone.
    /// </remarks>
    public sealed class GeneratedSerializerProvider : SerializerProvider
    {
        // Declared in the corpus assembly, alongside the types it serializes, so the generator can
        // see their non-public data members.
        private readonly GeneratedCorpusContext _context = new GeneratedCorpusContext();

        public override string Id => "Generated";

        protected override DataContractSerializerOperationBehavior CreateBehavior(OperationDescription operation) =>
            new GeneratedDataContractSerializerOperationBehavior(operation, _context);

        /// <summary>
        /// Whether the generator produced a serializer for this case.
        /// </summary>
        /// <remarks>
        /// Asks the generated context rather than keeping a hand-maintained list, so the skip
        /// reasons in test output are the generator's actual coverage and cannot drift from it.
        /// On target frameworks where the generator does not run, nothing is generated and every
        /// case reports unsupported - which is the intended behaviour, not a silent pass.
        /// </remarks>
        public override bool TryGetUnsupportedReason(CorpusCase corpusCase, out string reason)
        {
            if (CreateAotSerializer(corpusCase) == null)
            {
                reason = "no serializer was generated for " + corpusCase.ContractType.FullName;
                return true;
            }

            reason = null;
            return false;
        }

        public override byte[] Capture(CorpusCase corpusCase)
        {
            AotXmlObjectSerializer serializer = CreateAotSerializer(corpusCase);
            return FixtureWriter.Capture(serializer, corpusCase.CreateInstance());
        }

        private AotXmlObjectSerializer CreateAotSerializer(CorpusCase corpusCase)
        {
            // Goes through the production behavior rather than the context directly, so the seam
            // CoreWCF actually uses is the seam under test.
            DataContractSerializerOperationBehavior behavior = CreateBehavior(null);

            XmlDictionary dictionary = new XmlDictionary(2);
            return behavior.CreateAotSerializer(
                corpusCase.ContractType,
                dictionary.Add(corpusCase.RootName),
                dictionary.Add(corpusCase.RootNamespace),
                corpusCase.KnownTypes);
        }
    }
}
