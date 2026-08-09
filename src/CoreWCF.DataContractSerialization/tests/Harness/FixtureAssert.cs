// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using CoreWCF.DataContractSerialization.TestCorpus;
using Xunit;

namespace CoreWCF.DataContractSerialization.Tests.Harness
{
    /// <summary>
    /// Compares captured bytes against a golden fixture.
    /// </summary>
    /// <remarks>
    /// The verdict is byte equality and nothing else - the whole point of the corpus is that a
    /// source-generated serializer must reproduce the reference output exactly, including attribute
    /// order and namespace prefix assignment. Deliberately no C14N canonicalisation here:
    /// CoreWCF.Metadata's WsdlHelper canonicalises because WSDL attribute order is genuinely
    /// unstable, but relaxing the comparison here would hide precisely the bugs being hunted.
    /// Pretty-printing happens only in the failure message.
    /// </remarks>
    internal static class FixtureAssert
    {
        private const int ContextBytes = 40;

        public static void Equal(byte[] expected, byte[] actual, CorpusCase corpusCase, string fixturePath)
        {
            if (FixtureStore.BytesEqual(expected, actual))
            {
                return;
            }

            Assert.Fail(BuildFailureMessage(expected, actual, corpusCase, fixturePath));
        }

        private static string BuildFailureMessage(byte[] expected, byte[] actual, CorpusCase corpusCase, string fixturePath)
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("Serialized output does not match the golden fixture.");
            message.AppendLine("  case    : " + corpusCase.Id);
            message.AppendLine("  fixture : " + fixturePath);
            message.AppendLine("  tfm     : " + TargetFrameworkInfo.Current);
            message.AppendLine("  bytes   : expected " + expected.Length.ToString(CultureInfo.InvariantCulture) +
                               ", actual " + actual.Length.ToString(CultureInfo.InvariantCulture));

            int offset = FirstDifference(expected, actual);
            message.AppendLine("  first difference at byte " + offset.ToString(CultureInfo.InvariantCulture) + ":");
            message.AppendLine("    expected: " + Excerpt(expected, offset));
            message.AppendLine("    actual  : " + Excerpt(actual, offset));
            message.AppendLine();
            message.AppendLine("Expected (formatted for reading only; the fixture on disk is a single line):");
            message.AppendLine(PrettyPrint(expected));
            message.AppendLine();
            message.AppendLine("Actual (formatted for reading only):");
            message.AppendLine(PrettyPrint(actual));
            message.AppendLine();
            message.AppendLine("If the new output is correct, regenerate with " +
                               FixtureStore.RegenerateEnvironmentVariable + "=1 and review the diff.");
            return message.ToString();
        }

        private static int FirstDifference(byte[] expected, byte[] actual)
        {
            int shared = Math.Min(expected.Length, actual.Length);
            for (int i = 0; i < shared; i++)
            {
                if (expected[i] != actual[i])
                {
                    return i;
                }
            }

            return shared;
        }

        private static string Excerpt(byte[] content, int offset)
        {
            if (offset >= content.Length)
            {
                return "<end of content>";
            }

            int start = Math.Max(0, offset - ContextBytes);
            int length = Math.Min(content.Length - start, ContextBytes * 2);
            string text = new UTF8Encoding(false).GetString(content, start, length);
            return Escape(text);
        }

        private static string Escape(string text) =>
            text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

        private static string PrettyPrint(byte[] content)
        {
            try
            {
                string text = new UTF8Encoding(false).GetString(content);
                StringBuilder formatted = new StringBuilder();

                XmlReaderSettings readerSettings = new XmlReaderSettings
                {
                    ConformanceLevel = ConformanceLevel.Fragment
                };
                XmlWriterSettings writerSettings = new XmlWriterSettings
                {
                    Indent = true,
                    OmitXmlDeclaration = true,
                    ConformanceLevel = ConformanceLevel.Fragment
                };

                using (StringReader stringReader = new StringReader(text))
                using (XmlReader reader = XmlReader.Create(stringReader, readerSettings))
                using (XmlWriter writer = XmlWriter.Create(formatted, writerSettings))
                {
                    writer.WriteNode(reader, defattr: true);
                }

                return formatted.ToString();
            }
            catch (XmlException)
            {
                // Never let formatting failure mask the real assertion.
                return "<not well-formed XML> " + Escape(new UTF8Encoding(false).GetString(content));
            }
        }
    }
}
