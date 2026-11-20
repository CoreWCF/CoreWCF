// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.DataContractSerialization.TestCorpus
{
    /// <summary>
    /// Collects corpus cases and exclusions. One registrar method per source file of contract
    /// types, so importing or re-syncing an upstream file touches a single registrar.
    /// </summary>
    public sealed class CorpusBuilder
    {
        /// <summary>
        /// The root element name and namespace CoreWCF supplies come from the message part, not
        /// from the data contract, so a fixed root is faithful to the wire. Contract naming is
        /// still fully exercised: it appears in every nested member element and every i:type.
        /// </summary>
        public const string DefaultRootName = "Root";

        public const string DefaultRootNamespace = "http://tempuri.org/";

        private readonly List<PendingCase> _cases = new List<PendingCase>();
        private readonly List<CorpusExclusion> _exclusions = new List<CorpusExclusion>();

        /// <summary>
        /// Registers one (type, variation) pair. <paramref name="createInstance"/> must be
        /// deterministic: no DateTime.Now, Guid.NewGuid, Random, or DateTimeKind.Local, all of
        /// which produce a fixture that cannot be reproduced on another run or another machine.
        /// </summary>
        public CorpusCaseBuilder Add<T>(string variation, Func<T> createInstance)
        {
            if (string.IsNullOrEmpty(variation))
            {
                throw new ArgumentException("Variation must not be empty.", nameof(variation));
            }

            if (createInstance == null)
            {
                throw new ArgumentNullException(nameof(createInstance));
            }

            PendingCase pending = new PendingCase(
                typeof(T).FullName + "." + variation,
                typeof(T),
                () => createInstance());

            _cases.Add(pending);
            return new CorpusCaseBuilder(pending);
        }

        /// <summary>
        /// Records that a contract type is deliberately uncovered. Keeps CorpusIntegrityTests
        /// green while leaving the gap visible.
        /// </summary>
        public void Skip<T>(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                throw new ArgumentException("An exclusion must state a reason.", nameof(reason));
            }

            _exclusions.Add(new CorpusExclusion(typeof(T), reason));
        }

        internal IReadOnlyList<CorpusCase> BuildCases()
        {
            List<CorpusCase> built = new List<CorpusCase>(_cases.Count);
            foreach (PendingCase pending in _cases)
            {
                built.Add(pending.Build());
            }

            return built;
        }

        internal IReadOnlyList<CorpusExclusion> BuildExclusions() => _exclusions;

        internal sealed class PendingCase
        {
            private readonly string _id;
            private readonly Type _contractType;
            private readonly Func<object> _createInstance;
            private readonly List<Type> _knownTypes = new List<Type>();
            private readonly List<string> _tags = new List<string>();
            private string _rootName = DefaultRootName;
            private string _rootNamespace = DefaultRootNamespace;

            internal PendingCase(string id, Type contractType, Func<object> createInstance)
            {
                _id = id;
                _contractType = contractType;
                _createInstance = createInstance;
            }

            internal void AddKnownTypes(Type[] knownTypes) => _knownTypes.AddRange(knownTypes);

            internal void AddTags(string[] tags) => _tags.AddRange(tags);

            internal void SetRoot(string name, string ns)
            {
                _rootName = name;
                _rootNamespace = ns;
            }

            internal CorpusCase Build() => new CorpusCase(
                _id,
                _contractType,
                _createInstance,
                _rootName,
                _rootNamespace,
                _knownTypes.ToArray(),
                _tags.ToArray());
        }
    }

    /// <summary>Fluent configuration of a single corpus case.</summary>
    public sealed class CorpusCaseBuilder
    {
        private readonly CorpusBuilder.PendingCase _pending;

        internal CorpusCaseBuilder(CorpusBuilder.PendingCase pending) => _pending = pending;

        /// <summary>Known types, as CoreWCF would pass them from the operation description.</summary>
        public CorpusCaseBuilder WithKnownTypes(params Type[] knownTypes)
        {
            _pending.AddKnownTypes(knownTypes ?? new Type[0]);
            return this;
        }

        /// <summary>Overrides the default root element name and namespace for this case.</summary>
        public CorpusCaseBuilder WithRoot(string name, string ns)
        {
            _pending.SetRoot(name, ns);
            return this;
        }

        public CorpusCaseBuilder WithTags(params string[] tags)
        {
            _pending.AddTags(tags ?? new string[0]);
            return this;
        }
    }
}
