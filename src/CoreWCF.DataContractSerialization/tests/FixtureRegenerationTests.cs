// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using CoreWCF.DataContractSerialization.TestCorpus;
using CoreWCF.DataContractSerialization.Tests.Harness;
using Xunit;

namespace CoreWCF.DataContractSerialization.Tests
{
    /// <summary>
    /// Re-records every golden fixture from the reflection-based reference serializer.
    /// </summary>
    /// <remarks>
    /// Opt-in only, and it always fails. A run that authors baselines must never be green:
    /// otherwise leaving the environment variable set in CI would silently rewrite the oracle and
    /// report success. The reviewable artifact is the resulting `git diff`, not the test result.
    /// </remarks>
    public class FixtureRegenerationTests
    {
        [Fact(
            Skip = "Opt in by setting COREWCF_REGENERATE_DCS_FIXTURES=1. See Fixtures/README.md.",
            SkipUnless = nameof(FixtureStore.IsRegenerationEnabled),
            SkipType = typeof(FixtureStore))]
        public void RegenerateGoldenFixtures()
        {
            Assert.False(
                FixtureStore.IsContinuousIntegration,
                "Fixture regeneration must not run in CI. " + FixtureStore.RegenerateEnvironmentVariable +
                " is set on a build agent, which would rewrite the baselines the build is supposed to be checking.");

            ReflectionSerializerProvider provider = new ReflectionSerializerProvider();
            Assert.True(
                provider.CanProduceFixtures,
                "Only a reference provider may author golden fixtures; a generated serializer authoring its own " +
                "baselines would make the conformance suite a tautology.");

            string sourceDirectory = FixtureStore.GetSourceFixtureDirectory();
            Assert.True(
                Directory.Exists(sourceDirectory),
                "Fixture source directory '" + sourceDirectory + "' not found. Regeneration writes to the source " +
                "tree and therefore only works on the machine that compiled this assembly.");

            Dictionary<FixtureWriteResult, int> counts = new Dictionary<FixtureWriteResult, int>();
            List<string> notable = new List<string>();

            List<string> failures = new List<string>();

            foreach (CorpusCase corpusCase in CorpusCatalog.Cases)
            {
                FixtureWriteResult result;
                try
                {
                    XmlObjectSerializer serializer = provider.CreateSerializer(corpusCase);
                    byte[] captured = FixtureWriter.Capture(serializer, corpusCase.CreateInstance());
                    result = FixtureStore.Write(sourceDirectory, corpusCase.FixtureFileName, captured);
                }
                catch (Exception error)
                {
                    // Keep going. Aborting on the first bad case leaves the fixture set half
                    // rewritten and hides every other problem behind one stack trace - and a case
                    // that cannot be serialized at all usually means it needs a Skip, which is
                    // easier to decide with the full list in hand.
                    failures.Add("  " + corpusCase.Id + ": " + error.GetType().Name + ": " + error.Message);
                    continue;
                }

                int count;
                counts.TryGetValue(result, out count);
                counts[result] = count + 1;

                if (result == FixtureWriteResult.OverrideCreated
                    || result == FixtureWriteResult.OverrideUpdated
                    || result == FixtureWriteResult.OverrideRemoved)
                {
                    notable.Add("  " + result + ": " + corpusCase.Id);
                }
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine("Regenerated golden fixtures for " + TargetFrameworkInfo.Current + ".");
            report.AppendLine("  directory: " + sourceDirectory);
            report.AppendLine("  cases    : " + CorpusCatalog.Cases.Count.ToString(CultureInfo.InvariantCulture));

            foreach (KeyValuePair<FixtureWriteResult, int> entry in counts)
            {
                report.AppendLine("  " + entry.Key + ": " + entry.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (failures.Count > 0)
            {
                report.AppendLine();
                report.AppendLine(failures.Count.ToString(CultureInfo.InvariantCulture) +
                                  " case(s) could not be serialized at all. Each needs either a fix or a " +
                                  "builder.Skip<T>(reason):");
                foreach (string line in failures)
                {
                    report.AppendLine(line);
                }
            }

            if (notable.Count > 0)
            {
                report.AppendLine("Framework-specific overrides changed:");
                foreach (string line in notable)
                {
                    report.AppendLine(line);
                }
            }

            if (TargetFrameworkInfo.IsBaseline)
            {
                report.AppendLine();
                report.AppendLine("This is the baseline framework. To record framework-specific divergence, rerun " +
                                  "regeneration on the other target frameworks; overrides are written only where the " +
                                  "bytes genuinely differ.");
            }

            report.AppendLine();
            report.AppendLine("Review the diff, then rerun without " + FixtureStore.RegenerateEnvironmentVariable + ".");

            Assert.Fail(report.ToString());
        }
    }
}
