// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace CoreWCF.DataContractSerialization.Tests.Harness
{
    /// <summary>
    /// Locates, reads and writes golden fixture files.
    /// </summary>
    /// <remarks>
    /// Reads come from the <em>output</em> directory, populated by CopyToOutputDirectory, so a
    /// no-build CI run works against a prebuilt bin/ artifact. Writes go to the <em>source</em>
    /// tree, so regeneration produces a reviewable diff.
    /// </remarks>
    public static class FixtureStore
    {
        public const string RegenerateEnvironmentVariable = "COREWCF_REGENERATE_DCS_FIXTURES";

        public const string FixturesDirectoryName = "Fixtures";

        /// <summary>Referenced by [Fact(SkipUnless = ...)] on the regeneration test.</summary>
        public static bool IsRegenerationEnabled =>
            string.Equals(
                Environment.GetEnvironmentVariable(RegenerateEnvironmentVariable),
                "1",
                StringComparison.Ordinal);

        public static bool IsContinuousIntegration =>
            string.Equals(Environment.GetEnvironmentVariable("TF_BUILD"), "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

        public static string OutputFixtureDirectory =>
            Path.Combine(AppContext.BaseDirectory, FixturesDirectoryName);

        /// <summary>
        /// The Fixtures directory in the source tree. Resolved from this file's compile-time path,
        /// the same idiom CoreWCF.Metadata's WsdlHelper uses to find its baselines. Regeneration is
        /// therefore a developer-machine operation: it only works where the assembly was built.
        /// </summary>
        public static string GetSourceFixtureDirectory()
        {
            // <repo>/src/CoreWCF.DataContractSerialization/tests/Harness/FixtureStore.cs -> ../Fixtures
            string harnessDirectory = Path.GetDirectoryName(ThisFilePath());
            return Path.GetFullPath(Path.Combine(harnessDirectory, "..", FixturesDirectoryName));
        }

        // The [CallerFilePath] default must be captured here, not on the public method: otherwise
        // it resolves to whichever file called it, which is a different directory.
        private static string ThisFilePath([CallerFilePath] string path = null) => path;

        /// <summary>
        /// Resolves a fixture for the running target framework, preferring a framework-specific
        /// override over the baseline. Mirrors WsdlHelper's Wsdls/net10.0/ then Wsdls/ probe order.
        /// </summary>
        public static bool TryRead(string fixtureFileName, out byte[] content, out string resolvedPath)
        {
            foreach (string candidate in CandidateReadPaths(OutputFixtureDirectory, fixtureFileName))
            {
                if (File.Exists(candidate))
                {
                    content = File.ReadAllBytes(candidate);
                    resolvedPath = candidate;
                    return true;
                }
            }

            content = null;
            resolvedPath = null;
            return false;
        }

        public static IEnumerable<string> CandidateReadPaths(string fixtureDirectory, string fixtureFileName)
        {
            if (!TargetFrameworkInfo.IsBaseline)
            {
                yield return Path.Combine(fixtureDirectory, TargetFrameworkInfo.Current, fixtureFileName);
            }

            yield return Path.Combine(fixtureDirectory, fixtureFileName);
        }

        /// <summary>
        /// Records freshly captured bytes for the running target framework.
        /// </summary>
        /// <remarks>
        /// On the baseline framework this writes the canonical fixture. On any other framework it
        /// compares against the canonical fixture and writes an override <em>only</em> if the bytes
        /// genuinely differ - deleting any override that has become redundant. The override set
        /// therefore always equals the true divergence set, and prunes itself as the corpus and the
        /// runtimes change.
        /// </remarks>
        public static FixtureWriteResult Write(string sourceFixtureDirectory, string fixtureFileName, byte[] content)
        {
            string baselinePath = Path.Combine(sourceFixtureDirectory, fixtureFileName);

            if (TargetFrameworkInfo.IsBaseline)
            {
                return WriteIfChanged(baselinePath, content, FixtureWriteResult.BaselineCreated, FixtureWriteResult.BaselineUpdated);
            }

            if (!File.Exists(baselinePath))
            {
                throw new InvalidOperationException(
                    "No baseline fixture at '" + baselinePath + "'. Regenerate on " +
                    TargetFrameworkInfo.BaselineTargetFramework + " first, so framework-specific files only ever record real divergence.");
            }

            string overridePath = Path.Combine(sourceFixtureDirectory, TargetFrameworkInfo.Current, fixtureFileName);

            if (BytesEqual(File.ReadAllBytes(baselinePath), content))
            {
                if (File.Exists(overridePath))
                {
                    File.Delete(overridePath);
                    return FixtureWriteResult.OverrideRemoved;
                }

                return FixtureWriteResult.MatchesBaseline;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(overridePath));
            return WriteIfChanged(overridePath, content, FixtureWriteResult.OverrideCreated, FixtureWriteResult.OverrideUpdated);
        }

        private static FixtureWriteResult WriteIfChanged(string path, byte[] content, FixtureWriteResult created, FixtureWriteResult updated)
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, content);
                return created;
            }

            if (BytesEqual(File.ReadAllBytes(path), content))
            {
                return FixtureWriteResult.Unchanged;
            }

            File.WriteAllBytes(path, content);
            return updated;
        }

        public static bool BytesEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }
    }

    public enum FixtureWriteResult
    {
        Unchanged,
        BaselineCreated,
        BaselineUpdated,
        OverrideCreated,
        OverrideUpdated,
        OverrideRemoved,
        MatchesBaseline
    }
}
