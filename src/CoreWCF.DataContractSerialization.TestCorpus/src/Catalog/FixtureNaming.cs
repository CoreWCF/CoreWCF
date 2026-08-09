// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CoreWCF.DataContractSerialization.TestCorpus
{
    /// <summary>
    /// Maps a corpus case id onto the name of the golden fixture file that records its output.
    /// </summary>
    /// <remarks>
    /// The mapping must be deterministic and stable: renaming a fixture silently orphans the
    /// baseline it was recording. It lives in the corpus rather than the test project so the
    /// catalog can compute <see cref="CorpusCase.FixtureFileName"/> exactly once.
    /// </remarks>
    public static class FixtureNaming
    {
        /// <summary>
        /// Fixtures live under bin/&lt;Config&gt;/&lt;Project&gt;/&lt;TFM&gt;/Fixtures/, which is already deep;
        /// generic type names get long enough to threaten MAX_PATH on Windows. Names longer than
        /// this are truncated and disambiguated with a hash suffix.
        /// </summary>
        private const int MaxStemLength = 110;

        private const int HashSuffixLength = 8;

        public const string Extension = ".xml";

        public static string ToFileName(string caseId)
        {
            if (caseId == null)
            {
                throw new ArgumentNullException(nameof(caseId));
            }

            if (caseId.Length == 0)
            {
                throw new ArgumentException("Corpus case id must not be empty.", nameof(caseId));
            }

            string stem = Sanitize(caseId);
            if (stem.Length > MaxStemLength)
            {
                stem = stem.Substring(0, MaxStemLength) + "_" + ShortHash(caseId);
            }

            return stem + Extension;
        }

        private static string Sanitize(string caseId)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(caseId.Length);
            bool lastWasUnderscore = false;

            foreach (char c in caseId)
            {
                bool replace = Array.IndexOf(invalid, c) >= 0;
                if (!replace)
                {
                    // Generic arity/argument punctuation and nested-type separators are legal in a
                    // file name on some platforms but not others. Normalise them everywhere so the
                    // same catalog produces the same file names on Windows and Linux.
                    switch (c)
                    {
                        case '`':
                        case '[':
                        case ']':
                        case '<':
                        case '>':
                        case ',':
                        case '+':
                        case '&':
                        case '*':
                        case '|':
                        case ' ':
                            replace = true;
                            break;
                    }
                }

                if (replace)
                {
                    if (!lastWasUnderscore)
                    {
                        builder.Append('_');
                        lastWasUnderscore = true;
                    }
                }
                else
                {
                    builder.Append(c);
                    lastWasUnderscore = false;
                }
            }

            return builder.ToString().Trim('_');
        }

        /// <summary>
        /// A stable, non-cryptographic hash. Deliberately not <see cref="string.GetHashCode"/>,
        /// which is randomised per process on .NET Core and would change fixture names between runs.
        /// </summary>
        private static string ShortHash(string value)
        {
            // FNV-1a, 32 bit.
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in value)
                {
                    hash ^= c;
                    hash *= 16777619;
                }

                return hash.ToString("x" + HashSuffixLength.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            }
        }
    }
}
