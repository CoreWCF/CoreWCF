// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.DataContractSerialization.TestCorpus
{
    /// <summary>
    /// The golden-record corpus: every contract instance whose serialized XML is pinned by a
    /// fixture. Declared with explicit lambdas and no reflection, so the catalog stays usable from
    /// an ahead-of-time-compiled smoke application.
    /// </summary>
    public static partial class CorpusCatalog
    {
        private static readonly IReadOnlyList<CorpusCase> s_cases;
        private static readonly IReadOnlyList<CorpusExclusion> s_exclusions;
        private static readonly Dictionary<string, CorpusCase> s_byId;

        static CorpusCatalog()
        {
            CorpusBuilder builder = new CorpusBuilder();

            // One registrar per source file of contract types. Unimplemented partial methods are
            // erased by the compiler, so a registrar can be declared before its file is imported.
            RegisterSanity(builder);
            RegisterPrimitives(builder);
            RegisterInheritance(builder);

            s_cases = builder.BuildCases();
            s_exclusions = builder.BuildExclusions();

            s_byId = new Dictionary<string, CorpusCase>(StringComparer.Ordinal);
            foreach (CorpusCase corpusCase in s_cases)
            {
                if (s_byId.ContainsKey(corpusCase.Id))
                {
                    throw new InvalidOperationException(
                        "Duplicate corpus case id '" + corpusCase.Id + "'. Ids must be unique: they are the xUnit theory key.");
                }

                s_byId.Add(corpusCase.Id, corpusCase);
            }
        }

        public static IReadOnlyList<CorpusCase> Cases => s_cases;

        public static IReadOnlyList<CorpusExclusion> Exclusions => s_exclusions;

        public static CorpusCase GetById(string id)
        {
            CorpusCase corpusCase;
            if (!s_byId.TryGetValue(id, out corpusCase))
            {
                throw new KeyNotFoundException("No corpus case with id '" + id + "'.");
            }

            return corpusCase;
        }

        public static IEnumerable<string> Ids
        {
            get
            {
                foreach (CorpusCase corpusCase in s_cases)
                {
                    yield return corpusCase.Id;
                }
            }
        }

        static partial void RegisterSanity(CorpusBuilder builder);

        static partial void RegisterPrimitives(CorpusBuilder builder);

        static partial void RegisterInheritance(CorpusBuilder builder);
    }
}
