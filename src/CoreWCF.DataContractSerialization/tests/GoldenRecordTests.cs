// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using CoreWCF.DataContractSerialization.TestCorpus;
using CoreWCF.DataContractSerialization.Tests.Harness;
using Xunit;

namespace CoreWCF.DataContractSerialization.Tests
{
    /// <summary>
    /// Asserts that a serializer reproduces the recorded reference output byte for byte.
    /// </summary>
    /// <remarks>
    /// Written as an abstract base with one sealed subclass per provider. When the source generator
    /// lands, wiring it in is a single subclass supplying a GeneratedSerializerProvider - the test
    /// body, the corpus and the fixtures are all reused unchanged.
    /// </remarks>
    public abstract class GoldenRecordTestsBase
    {
        protected abstract SerializerProvider Provider { get; }

        [Theory]
        [MemberData(nameof(CorpusTheoryData.CaseIds), MemberType = typeof(CorpusTheoryData))]
        public void WriteObject_MatchesGoldenRecord(string caseId)
        {
            CorpusCase corpusCase = CorpusCatalog.GetById(caseId);

            string reason;
            if (Provider.TryGetUnsupportedReason(corpusCase, out reason))
            {
                Assert.Skip(Provider.Id + " does not support '" + caseId + "' yet: " + reason);
            }

            byte[] actual = Provider.Capture(corpusCase);

            byte[] expected;
            string resolvedPath;
            if (!FixtureStore.TryRead(corpusCase.FixtureFileName, out expected, out resolvedPath))
            {
                // Never auto-create: a fixture that appears as a side effect of running the tests
                // records nothing, because no human ever reviewed it.
                Assert.Fail(
                    "No golden fixture for case '" + caseId + "' (expected file '" + corpusCase.FixtureFileName +
                    "' under " + FixtureStore.OutputFixtureDirectory + "). Regenerate with " +
                    FixtureStore.RegenerateEnvironmentVariable + "=1 and review the diff before committing.");
            }

            FixtureAssert.Equal(expected, actual, corpusCase, resolvedPath);
        }
    }

    /// <summary>The reference implementation must, by construction, match its own recorded output.</summary>
    public sealed class ReflectionGoldenRecordTests : GoldenRecordTestsBase
    {
        protected override SerializerProvider Provider { get; } = new ReflectionSerializerProvider();
    }

    /// <summary>
    /// The real test: source-generated output measured byte for byte against bytes the generator
    /// had no part in producing.
    /// </summary>
    /// <remarks>
    /// Cases the generator cannot yet handle report a reason and skip. That skip list, not this
    /// class, is the coverage report - and a fixture is never regenerated to make a case pass,
    /// because a mismatch means the generator is wrong.
    /// </remarks>
    public sealed class GeneratedGoldenRecordTests : GoldenRecordTestsBase
    {
        protected override SerializerProvider Provider { get; } = new GeneratedSerializerProvider();
    }
}
