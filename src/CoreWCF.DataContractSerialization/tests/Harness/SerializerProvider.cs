// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using System.Xml;
using CoreWCF.DataContractSerialization.TestCorpus;
using CoreWCF.Description;

namespace CoreWCF.DataContractSerialization.Tests.Harness
{
    /// <summary>
    /// Supplies the serializer under test for one corpus case.
    /// </summary>
    /// <remarks>
    /// Deliberately routed through the real production extension point,
    /// <see cref="DataContractSerializerOperationBehavior"/>, rather than a bespoke test interface.
    /// "Passes the harness" then literally means "drops into CoreWCF". When the source generator
    /// lands, adding it is one new subclass here plus one new test subclass - nothing else changes.
    /// </remarks>
    public abstract class SerializerProvider
    {
        /// <summary>Stable identifier, used in test output and skip messages.</summary>
        public abstract string Id { get; }

        /// <summary>
        /// Whether this provider is allowed to author golden fixtures. Only the reflection-based
        /// reference implementation may: if the generated serializer authored its own baselines the
        /// conformance suite would be a tautology.
        /// </summary>
        public virtual bool CanProduceFixtures => false;

        protected abstract DataContractSerializerOperationBehavior CreateBehavior(OperationDescription operation);

        public XmlObjectSerializer CreateSerializer(CorpusCase corpusCase)
        {
            // A null OperationDescription is safe: CreateSerializer never reads it. CoreWCF's own
            // SerializationTests already construct the behavior this way.
            DataContractSerializerOperationBehavior behavior = CreateBehavior(null);

            // Use the XmlDictionaryString overload, which is the one the hot body path calls from
            // DataContractSerializerOperationFormatter.PartInfo.Serializer.
            XmlDictionary dictionary = new XmlDictionary(2);
            return behavior.CreateSerializer(
                corpusCase.ContractType,
                dictionary.Add(corpusCase.RootName),
                dictionary.Add(corpusCase.RootNamespace),
                corpusCase.KnownTypes);
        }

        /// <summary>
        /// Serializes one corpus case and returns the exact bytes written.
        /// </summary>
        /// <remarks>
        /// Virtual because the two producers reach their serializer through different contracts -
        /// the reflection one through <see cref="XmlObjectSerializer"/>, the generated one through
        /// the attribute-free <c>AotXmlObjectSerializer</c>. The writer configuration is identical
        /// either way, so the bytes remain directly comparable.
        /// </remarks>
        public virtual byte[] Capture(CorpusCase corpusCase) =>
            FixtureWriter.Capture(CreateSerializer(corpusCase), corpusCase.CreateInstance());

        /// <summary>
        /// Lets a provider declare that it does not support a case yet, without deleting the case.
        /// The unsupported list is then visible in test output and shrinks as the generator matures.
        /// </summary>
        public virtual bool TryGetUnsupportedReason(CorpusCase corpusCase, out string reason)
        {
            reason = null;
            return false;
        }
    }
}
