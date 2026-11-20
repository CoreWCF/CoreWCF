// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using CoreWCF.DataContractSerialization.TestCorpus;
using CoreWCF.DataContractSerialization.Tests.Harness;
using Xunit;

namespace CoreWCF.DataContractSerialization.Tests
{
    /// <summary>
    /// Pins the on-disk fixture format.
    /// </summary>
    /// <remarks>
    /// The whole corpus rests on the assumption that XmlDictionaryWriter.CreateTextWriter emits no
    /// byte-order mark, no XML declaration and no gratuitous whitespace. If that were wrong, every
    /// fixture would be silently wrong in the same way and nothing else would notice. These tests
    /// exist so the assumption fails loudly instead.
    /// </remarks>
    public class FixtureWriterTests
    {
        private static byte[] CaptureFirstCase()
        {
            ReflectionSerializerProvider provider = new ReflectionSerializerProvider();
            CorpusCase corpusCase = CorpusCatalog.Cases[0];
            XmlObjectSerializer serializer = provider.CreateSerializer(corpusCase);
            return FixtureWriter.Capture(serializer, corpusCase.CreateInstance());
        }

        [Fact]
        public void Capture_EmitsNoByteOrderMark()
        {
            byte[] captured = CaptureFirstCase();

            Assert.NotEmpty(captured);
            Assert.Equal((byte)'<', captured[0]);
        }

        [Fact]
        public void Capture_EmitsNoXmlDeclaration()
        {
            string text = new UTF8Encoding(false).GetString(CaptureFirstCase());

            Assert.DoesNotContain("<?xml", text);
        }

        [Fact]
        public void Capture_EmitsNoLineBreaks()
        {
            // A single line keeps the fixture byte-exact: indentation is a lossy transform that
            // would mask exactly the whitespace bugs a generated serializer is likely to produce.
            byte[] captured = CaptureFirstCase();

            Assert.DoesNotContain((byte)'\r', captured);
            Assert.DoesNotContain((byte)'\n', captured);
        }

        [Fact]
        public void Capture_ProducesWellFormedXml()
        {
            using (MemoryStream stream = new MemoryStream(CaptureFirstCase()))
            using (XmlReader reader = XmlReader.Create(stream))
            {
                while (reader.Read())
                {
                }
            }
        }

        [Fact]
        public void Capture_IsRepeatable()
        {
            // Guards against non-deterministic corpus instances leaking in: a fixture that differs
            // between two runs of the same process could never be recorded.
            Assert.Equal(CaptureFirstCase(), CaptureFirstCase());
        }

        [Fact]
        public void EveryCase_CapturesRepeatably()
        {
            ReflectionSerializerProvider provider = new ReflectionSerializerProvider();

            foreach (CorpusCase corpusCase in CorpusCatalog.Cases)
            {
                byte[] first = FixtureWriter.Capture(provider.CreateSerializer(corpusCase), corpusCase.CreateInstance());
                byte[] second = FixtureWriter.Capture(provider.CreateSerializer(corpusCase), corpusCase.CreateInstance());

                Assert.True(
                    FixtureStore.BytesEqual(first, second),
                    "Corpus case '" + corpusCase.Id + "' is not deterministic: two captures in the same process differ. " +
                    "Check its factory for DateTime.Now, Guid.NewGuid, Random or unordered collections.");
            }
        }
    }
}
