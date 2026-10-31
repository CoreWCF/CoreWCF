// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.DataContractSerialization.TestCorpus
{
    /// <summary>
    /// A contract type that is deliberately not covered by a fixture, with the reason why.
    /// </summary>
    /// <remarks>
    /// Paired with CorpusIntegrityTests, this turns the corpus into a checklist: every
    /// [DataContract] type in the assembly must be either registered or explicitly excluded, so
    /// "what does the generator not support yet" is greppable rather than tribal knowledge.
    /// </remarks>
    public sealed class CorpusExclusion
    {
        internal CorpusExclusion(Type contractType, string reason)
        {
            ContractType = contractType;
            Reason = reason;
        }

        public Type ContractType { get; }

        public string Reason { get; }

        public override string ToString() => ContractType.FullName + ": " + Reason;
    }
}
