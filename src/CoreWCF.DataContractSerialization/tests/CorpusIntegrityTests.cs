// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using CoreWCF.DataContractSerialization.TestCorpus;
using CoreWCF.DataContractSerialization.Tests.Harness;
using Xunit;

namespace CoreWCF.DataContractSerialization.Tests
{
    /// <summary>
    /// Keeps the corpus, the catalog and the fixtures on disk in agreement.
    /// </summary>
    /// <remarks>
    /// Reflection is fine here - the tests need not be ahead-of-time safe, only the corpus does.
    /// </remarks>
    public class CorpusIntegrityTests
    {
        private static IEnumerable<Type> DataContractTypes()
        {
            Assembly corpus = typeof(CorpusCatalog).Assembly;
            foreach (Type type in corpus.GetTypes())
            {
                // IsPublic is false for nested types however visible they are, so a public nested
                // contract would otherwise slip through this check unnoticed.
                if (!(type.IsPublic || type.IsNestedPublic) || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                if (type.GetCustomAttribute<DataContractAttribute>(inherit: false) != null)
                {
                    yield return type;
                }
            }
        }

        [Fact]
        public void EveryDataContractTypeIsRegisteredOrExplicitlySkipped()
        {
            HashSet<Type> covered = new HashSet<Type>(CorpusCatalog.Cases.Select(c => c.ContractType));
            HashSet<Type> excluded = new HashSet<Type>(CorpusCatalog.Exclusions.Select(e => e.ContractType));

            List<string> unaccounted = DataContractTypes()
                .Where(t => !covered.Contains(t) && !excluded.Contains(t))
                .Select(t => t.FullName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            Assert.True(
                unaccounted.Count == 0,
                "Every [DataContract] type in the corpus must either have a registered case or an explicit " +
                "builder.Skip<T>(reason). Unaccounted for:" + Environment.NewLine + "  " +
                string.Join(Environment.NewLine + "  ", unaccounted));
        }

        [Fact]
        public void EveryCasesContractTypeIsListedInTheGeneratedContext()
        {
            // Registering a case in the catalog is only half of adding it: unless the type is also
            // listed on GeneratedCorpusContext, GetSerializer returns null for it and the generated
            // half of the harness skips it - with a reason indistinguishable from a legitimately
            // unsupported case. SanityPrimitiveArrays sat like that for a whole milestone, so the
            // collections work read as verified when the generator had never run on it.
            HashSet<Type> listed = new HashSet<Type>(
                typeof(GeneratedCorpusContext)
                    .GetCustomAttributes<DataContractSerializableAttribute>(inherit: false)
                    .Select(a => a.Type));

            List<string> missing = CorpusCatalog.Cases
                .Select(c => c.ContractType)
                .Distinct()
                .Where(t => !listed.Contains(t))
                .Select(t => t.FullName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            Assert.True(
                missing.Count == 0,
                "Every catalogued case's contract type must be listed on GeneratedCorpusContext, or the " +
                "generated serializer is never exercised for it. Missing:" + Environment.NewLine + "  " +
                string.Join(Environment.NewLine + "  ", missing));
        }

        [Fact]
        public void CaseIdsAreUnique()
        {
            List<string> duplicates = CorpusCatalog.Cases
                .GroupBy(c => c.Id, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.True(duplicates.Count == 0, "Duplicate case ids: " + string.Join(", ", duplicates));
        }

        [Fact]
        public void FixtureFileNamesAreUniqueIgnoringCase()
        {
            // Windows file systems are case-insensitive, so two ids differing only in case would
            // silently collapse onto one fixture and each would overwrite the other.
            List<string> duplicates = CorpusCatalog.Cases
                .GroupBy(c => c.FixtureFileName, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key + " <- " + string.Join(", ", g.Select(c => c.Id)))
                .ToList();

            Assert.True(duplicates.Count == 0, "Fixture file name collisions:" + Environment.NewLine + string.Join(Environment.NewLine, duplicates));
        }

        [Fact]
        public void EveryCaseHasAFixture()
        {
            List<string> missing = new List<string>();

            foreach (CorpusCase corpusCase in CorpusCatalog.Cases)
            {
                byte[] ignored;
                string resolvedPath;
                if (!FixtureStore.TryRead(corpusCase.FixtureFileName, out ignored, out resolvedPath))
                {
                    missing.Add(corpusCase.Id);
                }
            }

            Assert.True(
                missing.Count == 0,
                "Cases without a golden fixture (regenerate with " + FixtureStore.RegenerateEnvironmentVariable + "=1):" +
                Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", missing));
        }

        [Fact]
        public void NoOrphanFixtures()
        {
            // Catches fixture rot: a renamed or deleted case leaves a file recording nothing.
            //
            // Only meaningful on the baseline framework. Some cases are compiled conditionally -
            // DateTimeOnlyWrapper needs DateOnly/TimeOnly, which .NET Framework lacks - so on any
            // other framework the catalog is legitimately a subset of the fixtures on disk.
            if (!TargetFrameworkInfo.IsBaseline)
            {
                Assert.Skip(
                    "Orphan detection runs on " + TargetFrameworkInfo.BaselineTargetFramework +
                    ", where the catalog is complete; elsewhere conditionally-compiled cases make it a subset.");
            }

            if (!Directory.Exists(FixtureStore.OutputFixtureDirectory))
            {
                return;
            }

            HashSet<string> expected = new HashSet<string>(
                CorpusCatalog.Cases.Select(c => c.FixtureFileName),
                StringComparer.OrdinalIgnoreCase);

            List<string> orphans = Directory
                .GetFiles(FixtureStore.OutputFixtureDirectory, "*" + FixtureNaming.Extension, SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Where(name => !expected.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            Assert.True(
                orphans.Count == 0,
                "Fixture files with no corresponding corpus case:" + Environment.NewLine + "  " +
                string.Join(Environment.NewLine + "  ", orphans));
        }

        [Fact]
        public void EveryExclusionStatesAReason()
        {
            foreach (CorpusExclusion exclusion in CorpusCatalog.Exclusions)
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(exclusion.Reason),
                    "Exclusion of " + exclusion.ContractType.FullName + " must state why.");
            }
        }

        [Fact]
        public void CorpusDoesNotReferenceTheHostingStack()
        {
            // The corpus references CoreWCF.DataContractSerialization deliberately: the generated
            // serializer context must live in the same assembly as the contracts so the generator
            // can see their non-public data members. That package is netstandard2.0 and free of
            // reflection, so it does not compromise what this test exists to protect - the corpus
            // staying publishable ahead-of-time.
            //
            // The hosting stack is a different matter. Pulling any of it in would drag in the
            // reflection-heavy dispatch machinery and make the corpus unusable as an AOT smoke app.
            string[] forbidden = { "CoreWCF.Http", "CoreWCF.NetTcp", "CoreWCF.WebHttp", "CoreWCF.Queue", "CoreWCF.Metadata" };

            List<string> found = typeof(CorpusCatalog).Assembly
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(n => forbidden.Contains(n, StringComparer.Ordinal))
                .ToList();

            Assert.True(
                found.Count == 0,
                "The test corpus must not reference the CoreWCF hosting stack. Found: " + string.Join(", ", found));
        }
    }
}
