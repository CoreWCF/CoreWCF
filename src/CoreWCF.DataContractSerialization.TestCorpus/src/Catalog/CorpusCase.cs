// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.DataContractSerialization.TestCorpus
{
    /// <summary>
    /// One entry in the golden-record corpus: a contract type, a specific instance of it, and the
    /// serializer settings to use. Exactly one fixture file records the XML this produces.
    /// </summary>
    /// <remarks>
    /// The instance is part of the golden record's identity, which is why it is supplied as an
    /// explicit factory rather than discovered by convention. A convention-based factory that
    /// silently changed - because an upstream type tweaked what its populating constructor fills in
    /// - would invalidate every affected fixture without producing a reviewable diff.
    /// </remarks>
    public sealed class CorpusCase
    {
        internal CorpusCase(
            string id,
            Type contractType,
            Func<object> createInstance,
            string rootName,
            string rootNamespace,
            IList<Type> knownTypes,
            IReadOnlyList<string> tags)
        {
            Id = id;
            ContractType = contractType;
            CreateInstance = createInstance;
            RootName = rootName;
            RootNamespace = rootNamespace;
            KnownTypes = knownTypes;
            Tags = tags;
            FixtureFileName = FixtureNaming.ToFileName(id);
        }

        /// <summary>
        /// Stable identity of the case, of the form &lt;type full name&gt;.&lt;variation&gt;. Used as the
        /// xUnit theory key, so it must not change when the catalog is reordered.
        /// </summary>
        public string Id { get; }

        /// <summary>The declared type handed to CreateSerializer - not necessarily the runtime type.</summary>
        public Type ContractType { get; }

        public Func<object> CreateInstance { get; }

        public string RootName { get; }

        public string RootNamespace { get; }

        public IList<Type> KnownTypes { get; }

        /// <summary>Free-form labels, e.g. "floating-point", "knowntype", "inheritance".</summary>
        public IReadOnlyList<string> Tags { get; }

        public string FixtureFileName { get; }

        public override string ToString() => Id;
    }
}
