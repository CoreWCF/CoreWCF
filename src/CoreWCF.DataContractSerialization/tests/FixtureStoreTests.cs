// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CoreWCF.DataContractSerialization.TestCorpus;
using CoreWCF.DataContractSerialization.Tests.Harness;
using Xunit;

namespace CoreWCF.DataContractSerialization.Tests
{
    /// <summary>
    /// Covers fixture naming and the per-target-framework baseline/override rules.
    /// </summary>
    /// <remarks>
    /// The override branches depend on whether the running framework is the baseline, so the write
    /// tests assert the behaviour appropriate to the current framework. Running the suite across
    /// the whole matrix therefore covers every branch, which matters because the corpus currently
    /// shows no genuine divergence between frameworks - so the override path is otherwise unproven.
    /// </remarks>
    public class FixtureStoreTests : IDisposable
    {
        private readonly string _directory;

        public FixtureStoreTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "corewcf-dcs-fixtures-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private static byte[] Bytes(string text) => new UTF8Encoding(false).GetBytes(text);

        private string BaselinePath(string name) => Path.Combine(_directory, name);

        private string OverridePath(string name) => Path.Combine(_directory, TargetFrameworkInfo.Current, name);

        [Fact]
        public void ToFileName_AppendsXmlExtension()
        {
            Assert.Equal("Some.Type.variation.xml", FixtureNaming.ToFileName("Some.Type.variation"));
        }

        [Theory]
        [InlineData("Ns.Generic`1[System.Int32].v", "Ns.Generic_1_System.Int32_.v.xml")]
        [InlineData("Ns.Outer+Inner.v", "Ns.Outer_Inner.v.xml")]
        [InlineData("Ns.T<A,B>.v", "Ns.T_A_B_.v.xml")]
        public void ToFileName_ReplacesCharactersThatAreNotPortableInFileNames(string id, string expected)
        {
            Assert.Equal(expected, FixtureNaming.ToFileName(id));
        }

        [Fact]
        public void ToFileName_CollapsesRunsAndTrimsSeparators()
        {
            // `[  ]` would otherwise produce a run of underscores and a trailing one.
            Assert.Equal("A_B.xml", FixtureNaming.ToFileName("A`[ ]B"));
        }

        [Fact]
        public void ToFileName_TruncatesLongIdsButKeepsThemDistinct()
        {
            string first = FixtureNaming.ToFileName(new string('a', 300) + "one");
            string second = FixtureNaming.ToFileName(new string('a', 300) + "two");

            Assert.True(first.Length < 130, "Long names must be truncated to stay clear of MAX_PATH; got " + first.Length);
            Assert.NotEqual(first, second);
        }

        [Fact]
        public void ToFileName_IsStableAcrossCalls()
        {
            // Guards against string.GetHashCode, which is randomised per process on .NET Core and
            // would rename fixtures between runs.
            Assert.Equal(
                FixtureNaming.ToFileName(new string('z', 300)),
                FixtureNaming.ToFileName(new string('z', 300)));
        }

        [Fact]
        public void CandidateReadPaths_PrefersTheFrameworkSpecificOverride()
        {
            List<string> candidates = FixtureStore.CandidateReadPaths(_directory, "case.xml").ToList();

            if (TargetFrameworkInfo.IsBaseline)
            {
                Assert.Equal(new string[] { BaselinePath("case.xml") }, candidates);
            }
            else
            {
                Assert.Equal(new string[] { OverridePath("case.xml"), BaselinePath("case.xml") }, candidates);
            }
        }

        [Fact]
        public void Write_OnBaselineFramework_CreatesThenLeavesTheCanonicalFixtureAlone()
        {
            if (!TargetFrameworkInfo.IsBaseline)
            {
                Assert.Skip("Baseline write behaviour is only observable on " + TargetFrameworkInfo.BaselineTargetFramework + ".");
            }

            Assert.Equal(FixtureWriteResult.BaselineCreated, FixtureStore.Write(_directory, "case.xml", Bytes("<a/>")));
            Assert.Equal(FixtureWriteResult.Unchanged, FixtureStore.Write(_directory, "case.xml", Bytes("<a/>")));
            Assert.Equal(FixtureWriteResult.BaselineUpdated, FixtureStore.Write(_directory, "case.xml", Bytes("<b/>")));
            Assert.Equal("<b/>", File.ReadAllText(BaselinePath("case.xml")));
        }

        [Fact]
        public void Write_OnOtherFrameworks_RequiresABaselineFirst()
        {
            if (TargetFrameworkInfo.IsBaseline)
            {
                Assert.Skip("Only meaningful off the baseline framework.");
            }

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => FixtureStore.Write(_directory, "case.xml", Bytes("<a/>")));

            Assert.Contains(TargetFrameworkInfo.BaselineTargetFramework, error.Message);
        }

        [Fact]
        public void Write_OnOtherFrameworks_WritesAnOverrideOnlyWhenBytesDiffer()
        {
            if (TargetFrameworkInfo.IsBaseline)
            {
                Assert.Skip("Only meaningful off the baseline framework.");
            }

            File.WriteAllBytes(BaselinePath("case.xml"), Bytes("<a/>"));

            // Identical to the baseline: no override file at all.
            Assert.Equal(FixtureWriteResult.MatchesBaseline, FixtureStore.Write(_directory, "case.xml", Bytes("<a/>")));
            Assert.False(File.Exists(OverridePath("case.xml")));

            // Genuinely divergent: record it.
            Assert.Equal(FixtureWriteResult.OverrideCreated, FixtureStore.Write(_directory, "case.xml", Bytes("<b/>")));
            Assert.Equal("<b/>", File.ReadAllText(OverridePath("case.xml")));

            Assert.Equal(FixtureWriteResult.OverrideUpdated, FixtureStore.Write(_directory, "case.xml", Bytes("<c/>")));

            // Divergence went away: the override must be pruned, not left to rot.
            Assert.Equal(FixtureWriteResult.OverrideRemoved, FixtureStore.Write(_directory, "case.xml", Bytes("<a/>")));
            Assert.False(File.Exists(OverridePath("case.xml")));
        }

        [Fact]
        public void GetSourceFixtureDirectory_ResolvesToTheFixturesFolderBesideTheHarness()
        {
            string directory = FixtureStore.GetSourceFixtureDirectory();

            Assert.True(
                Directory.Exists(directory),
                "Expected the source fixture directory to exist at '" + directory + "'.");
            Assert.Equal(FixtureStore.FixturesDirectoryName, new DirectoryInfo(directory).Name);
        }
    }
}
