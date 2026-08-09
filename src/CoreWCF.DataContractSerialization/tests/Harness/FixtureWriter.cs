// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace CoreWCF.DataContractSerialization.Tests.Harness
{
    /// <summary>
    /// Captures the exact bytes a serializer writes, so they can be recorded as a golden fixture.
    /// </summary>
    internal static class FixtureWriter
    {
        /// <summary>
        /// Captures a source-generated serializer's output. Byte-for-byte comparable with the
        /// reflection-based overload below, which is the entire point: both go through the same
        /// writer, configured identically.
        /// </summary>
        public static byte[] Capture(CoreWCF.Runtime.Serialization.AotXmlObjectSerializer serializer, object graph)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (XmlDictionaryWriter writer = XmlDictionaryWriter.CreateTextWriter(
                    stream, new UTF8Encoding(false), ownsStream: false))
                {
                    serializer.WriteObject(writer, graph);
                    writer.Flush();
                }

                return stream.ToArray();
            }
        }

        public static byte[] Capture(XmlObjectSerializer serializer, object graph)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                // XmlDictionaryWriter.CreateTextWriter is the writer CoreWCF's real body path uses
                // (Dispatcher/DataContractSerializerOperationFormatter), so a fixture is the actual
                // on-the-wire byte sequence rather than a re-encoding of it. XmlWriter.Create with
                // default settings would add an XML declaration and possibly indentation, neither
                // of which appears on the wire.
                //
                // UTF8Encoding(false) rather than Encoding.UTF8: the latter carries a preamble.
                using (XmlDictionaryWriter writer = XmlDictionaryWriter.CreateTextWriter(
                    stream, new UTF8Encoding(false), ownsStream: false))
                {
                    serializer.WriteObject(writer, graph);
                    writer.Flush();
                }

                return stream.ToArray();
            }
        }
    }
}
