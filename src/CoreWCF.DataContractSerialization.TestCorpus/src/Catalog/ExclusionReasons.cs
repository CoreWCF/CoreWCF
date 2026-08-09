// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.DataContractSerialization.TestCorpus
{
    /// <summary>
    /// Canonical reasons for excluding a contract type from the corpus. Free-form strings are
    /// allowed, but reusing these keeps the exclusion list groupable.
    /// </summary>
    public static class ExclusionReasons
    {
        public const string DataContractResolverOutOfScope =
            "DataContractResolver is out of scope for v1 of the source generator.";

        public const string SurrogateOutOfScope =
            "ISerializationSurrogateProvider is out of scope for v1 of the source generator.";

        public const string ObjectReferenceOutOfScope =
            "IObjectReference / object graph preservation is out of scope for v1 of the source generator.";

        public const string SerializableOutOfScope =
            "[Serializable]/ISerializable is out of scope for v1 of the source generator.";

        public const string CollectionDataContractOutOfScope =
            "[CollectionDataContract] is deferred to a later milestone.";

        public const string NonDeterministicInstance =
            "No deterministic instance can be constructed for this type (time, randomness or machine state).";
    }
}
