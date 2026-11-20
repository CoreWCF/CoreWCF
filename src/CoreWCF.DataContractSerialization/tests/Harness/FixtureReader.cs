// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace CoreWCF.DataContractSerialization.Tests.Harness
{
    /// <summary>
    /// Reads a recorded fixture back into an object graph.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The write side has an obvious oracle: the exact bytes the real serializer produced. The read
    /// side has no equivalent, because comparing two object graphs would mean writing an equality
    /// rule for all eighty corpus types, and a wrong rule reports success just as readily as a right
    /// one does.
    /// </para>
    /// <para>
    /// So the fixture is the oracle here too, used the other way round: read it with the serializer
    /// under test, write the result back out with the <em>reflection</em> serializer, and compare
    /// those bytes to the fixture. That composition only reproduces the fixture if the graph that
    /// came out of the read is the graph that went into the recording, and it reuses the byte
    /// comparison that is already trusted rather than inventing a second notion of correctness.
    /// </para>
    /// <para>
    /// It is not a round trip through the generated writer on purpose. Read and write sharing a
    /// mistake - a member skipped by both, a name misspelled the same way twice - would cancel out
    /// and pass. Writing back through reflection means the read is measured against the real
    /// implementation rather than against its own counterpart.
    /// </para>
    /// </remarks>
    internal static class FixtureReader
    {
        public static object Read(CoreWCF.Runtime.Serialization.AotXmlObjectSerializer serializer, byte[] document)
        {
            using (MemoryStream stream = new MemoryStream(document, writable: false))
            using (XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(stream, XmlDictionaryReaderQuotas.Max))
            {
                return serializer.ReadObject(reader, verifyObjectName: true);
            }
        }

        public static object Read(XmlObjectSerializer serializer, byte[] document)
        {
            using (MemoryStream stream = new MemoryStream(document, writable: false))
            using (XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(stream, XmlDictionaryReaderQuotas.Max))
            {
                return serializer.ReadObject(reader, verifyObjectName: true);
            }
        }
    }
}
